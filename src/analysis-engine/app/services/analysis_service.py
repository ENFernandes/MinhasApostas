"""Analysis service that coordinates data and LLM for recommendations."""

import os
from datetime import datetime

from structlog import get_logger

from app.db.database import get_db_session
from app.db.repositories import MatchRepository, OddsRepository, RecommendationRepository
from app.models.schemas import (
    AnalysisRequest,
    AnalysisResponse,
    AlternativeMarket,
    MatchData,
    ModelProbabilities,
    RecommendedMarket,
)
from app.services.ai.factory import ProviderFactory

logger = get_logger()


class AnalysisService:
    """Service for analyzing matches and generating recommendations."""

    def __init__(self):
        self.llm_provider = None
        self._init_llm()

    def _init_llm(self):
        """Initialize LLM provider."""
        provider_name = os.getenv("LLM_PROVIDER", "ollama")
        try:
            self.llm_provider = ProviderFactory.create_provider(provider_name)
            logger.info("LLM provider initialized", provider=provider_name)
        except Exception as e:
            logger.error("Failed to initialize LLM provider", error=str(e))
            self.llm_provider = None

    async def analyze_match(self, match_id: str) -> AnalysisResponse | None:
        """Analyze a match and generate recommendation."""
        try:
            async with get_db_session() as db:
                # Get match data
                match_repo = MatchRepository(db)
                match = await match_repo.get_by_id(match_id)
                
                if not match:
                    logger.error("Match not found", match_id=match_id)
                    return None

                # Get odds for match
                odds_repo = OddsRepository(db)
                odds = await odds_repo.get_by_match(match_id)

                # Build match data object (match.home_team and away_team are Team objects)
                match_data = MatchData(
                    id=str(match.id),
                    sport=match.sport,
                    home_team=match.home_team.name if match.home_team else "Unknown",
                    away_team=match.away_team.name if match.away_team else "Unknown",
                    commence_time=match.commence_time,
                    is_live=match.status in ("LIVE", "IN_PLAY", "PAUSED"),
                    competition=match.competition.name if match.competition else None,
                )

                # Build analysis request
                from app.models.schemas import OddsData
                odds_data = [
                    OddsData(
                        bookmaker=o.bookmaker,
                        market=o.market,
                        outcome=o.outcome,
                        odd=o.odd_decimal
                    )
                    for o in odds
                ]

                request = AnalysisRequest(
                    match=match_data,
                    odds=odds_data,
                )

                # Perform analysis
                result = await self._perform_analysis(request)
                
                # Save recommendation
                if result and result.recommended_market:
                    await self._save_recommendation(match.id, result)

                return result

        except Exception as e:
            logger.error("Error analyzing match", match_id=match_id, error=str(e))
            return None

    async def _perform_analysis(self, request: AnalysisRequest) -> AnalysisResponse:
        """Perform the actual analysis using LLM and statistical models."""
        match = request.match
        
        # Build prompt for LLM
        prompt = self._build_analysis_prompt(match, request.odds)
        
        # Get LLM analysis
        if self.llm_provider:
            try:
                llm_response = await self.llm_provider.complete(
                    prompt=prompt,
                    temperature=0.3,
                    max_tokens=2000,
                )
                reasoning = llm_response["content"]
                llm_provider_name = self.llm_provider.name
                llm_model = self.llm_provider.model
            except Exception as e:
                logger.error("LLM analysis failed", error=str(e))
                reasoning = "Análise estatística apenas (LLM não disponível)"
                llm_provider_name = "none"
                llm_model = "none"
        else:
            reasoning = "Análise estatística apenas (LLM não configurado)"
            llm_provider_name = "none"
            llm_model = "none"

        # Calculate model probabilities (simplified)
        probs = self._calculate_probabilities(match, request.odds)
        
        # Find best value bet
        recommended = self._find_best_value_bet(request.odds, probs)
        alternatives = self._find_alternative_bets(request.odds, probs, recommended)

        return AnalysisResponse(
            match_id=match.id,
            sport=match.sport,
            home_team=match.home_team,
            away_team=match.away_team,
            commence_time=match.commence_time,
            is_live=match.is_live,
            model_probabilities=probs,
            recommended_market=recommended,
            alternative_markets=alternatives,
            reasoning=reasoning,
            context_flags={},
            generated_at=datetime.utcnow(),
            llm_provider=llm_provider_name,
            llm_model=llm_model,
        )

    def _build_analysis_prompt(self, match: MatchData, odds: list) -> str:
        """Build prompt for LLM analysis."""
        odds_text = "\n".join([
            f"- {o.bookmaker} | {o.market} - {o.outcome}: @{o.odd}"
            for o in odds[:20]  # Limit to 20 odds
        ])

        return f"""Analise esta partida de futebol e identifica oportunidades de valor (value bets).

PARTIDA:
{match.home_team} vs {match.away_team}
Competição: {match.competition or 'N/A'}
Data: {match.commence_time.strftime('%d/%m/%Y %H:%M')}

ODDS DISPONÍVEIS:
{odds_text}

Por favor, analise:
1. Força relativa das equipas (com base em nomes e competição)
2. Mercados com potencial valor
3. Fatores importantes a considerar
4. Recomendação principal com justificação

Forneça uma análise concisa em português."""

    def _calculate_probabilities(self, match: MatchData, odds: list) -> ModelProbabilities:
        """Calculate model probabilities based on real odds (implied probability without vig)."""
        # --- 1X2 ---
        home_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("1", match.home_team)]
        draw_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("X", "Draw")]
        away_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("2", match.away_team)]

        if home_odds and draw_odds and away_odds:
            avg_home = sum(home_odds) / len(home_odds)
            avg_draw = sum(draw_odds) / len(draw_odds)
            avg_away = sum(away_odds) / len(away_odds)
            # Remover margem do bookmaker (vig)
            total = (1 / avg_home) + (1 / avg_draw) + (1 / avg_away)
            home_prob = (1 / avg_home) / total
            draw_prob = (1 / avg_draw) / total
            away_prob = (1 / avg_away) / total
        else:
            home_prob, draw_prob, away_prob = 0.40, 0.30, 0.30

        # --- Over 2.5 ---
        # Tentar calcular a partir das odds reais de totals
        over25_odds = [
            o.odd for o in odds
            if o.market in ("totals", "over_under")
            and "2.5" in str(o.outcome)
            and "over" in str(o.outcome).lower()
        ]
        under25_odds = [
            o.odd for o in odds
            if o.market in ("totals", "over_under")
            and "2.5" in str(o.outcome)
            and "under" in str(o.outcome).lower()
        ]

        if over25_odds and under25_odds:
            avg_over = sum(over25_odds) / len(over25_odds)
            avg_under = sum(under25_odds) / len(under25_odds)
            total_ou = (1 / avg_over) + (1 / avg_under)
            over_2_5_prob = (1 / avg_over) / total_ou
        elif over25_odds:
            # Só temos over — usar implied probability direta (inclui vig, mas é melhor que 50%)
            over_2_5_prob = 1 / (sum(over25_odds) / len(over25_odds))
        else:
            # Sem odds de totals: heurística baseada no equilíbrio do jogo
            # Jogos equilibrados tendem a ter menos golos; favorito claro → mais golos esperados
            balance = 1 - abs(home_prob - away_prob)  # 1 = equilibrado, 0 = dominância total
            over_2_5_prob = 0.45 + (balance * 0.10)  # entre 45% e 55%

        # --- BTTS ---
        btts_yes_odds = [
            o.odd for o in odds
            if o.market == "btts"
            and o.outcome.lower() in ("yes", "sim")
        ]
        btts_no_odds = [
            o.odd for o in odds
            if o.market == "btts"
            and o.outcome.lower() in ("no", "não", "nao")
        ]

        if btts_yes_odds and btts_no_odds:
            avg_btts_yes = sum(btts_yes_odds) / len(btts_yes_odds)
            avg_btts_no = sum(btts_no_odds) / len(btts_no_odds)
            total_btts = (1 / avg_btts_yes) + (1 / avg_btts_no)
            btts_prob = (1 / avg_btts_yes) / total_btts
        elif btts_yes_odds:
            btts_prob = 1 / (sum(btts_yes_odds) / len(btts_yes_odds))
        else:
            # Heurística: BTTS correlaciona com força ofensiva da equipa mais fraca
            weaker = min(home_prob, away_prob)
            btts_prob = 0.38 + (weaker * 0.40)  # entre 38% e ~58%

        return ModelProbabilities(
            home=round(home_prob, 3),
            draw=round(draw_prob, 3),
            away=round(away_prob, 3),
            over_2_5=round(min(over_2_5_prob, 0.85), 3),
            btts=round(min(btts_prob, 0.80), 3),
        )

    def _find_best_value_bet(
        self, 
        odds: list, 
        probs: ModelProbabilities
    ) -> RecommendedMarket | None:
        """Find the best value bet based on odds and probabilities."""
        best_value = -999
        best_bet = None

        # Map market outcomes to probabilities
        prob_map = {
            ("1X2", "1"): probs.home,
            ("1X2", "X"): probs.draw,
            ("1X2", "2"): probs.away,
        }

        for odd in odds:
            key = (odd.market, odd.outcome)
            if key not in prob_map:
                continue

            model_prob = prob_map[key]
            implied_prob = 1 / odd.odd
            value = model_prob - implied_prob

            # Kelly criterion
            if value > 0:
                kelly = (model_prob * odd.odd - 1) / (odd.odd - 1)
                bankroll = float(os.getenv("DEFAULT_BANKROLL", "100"))
                max_stake_pct = float(os.getenv("MAX_STAKE_PCT", "0.05"))
                stake = min(kelly * bankroll, bankroll * max_stake_pct)
            else:
                kelly = 0
                stake = 0

            if value > best_value and value > 0.05:  # Min 5% value
                best_value = value
                confidence = min(int(value * 100), 10)
                
                best_bet = RecommendedMarket(
                    market=odd.market,
                    outcome=odd.outcome,
                    bookmaker=odd.bookmaker,
                    odd=odd.odd,
                    implied_probability=round(implied_prob, 3),
                    model_probability=model_prob,
                    value=round(value, 3),
                    kelly_fraction=round(kelly, 3),
                    stake_euros=round(stake, 2),
                    confidence=max(confidence, 1),
                )

        return best_bet

    def _find_alternative_bets(
        self,
        odds: list,
        probs: ModelProbabilities,
        exclude: RecommendedMarket | None,
    ) -> list[AlternativeMarket]:
        """Find alternative value bets."""
        alternatives = []
        
        prob_map = {
            ("1X2", "1"): probs.home,
            ("1X2", "X"): probs.draw,
            ("1X2", "2"): probs.away,
        }

        for odd in odds:
            # Skip the main recommendation
            if exclude and odd.market == exclude.market and odd.outcome == exclude.outcome:
                continue

            key = (odd.market, odd.outcome)
            if key not in prob_map:
                continue

            model_prob = prob_map[key]
            implied_prob = 1 / odd.odd
            value = model_prob - implied_prob

            if value > 0.03:  # Min 3% value for alternatives
                confidence = min(int(value * 100), 10)
                alternatives.append(AlternativeMarket(
                    market=odd.market,
                    outcome=odd.outcome,
                    odd=odd.odd,
                    value=round(value, 3),
                    confidence=max(confidence, 1),
                ))

        return alternatives[:3]  # Top 3 alternatives

    async def _save_recommendation(self, match_id, analysis: AnalysisResponse):
        """Save recommendation to database."""
        from app.models.schemas import RecommendationCreate
        
        if not analysis.recommended_market:
            return

        rec = analysis.recommended_market
        
        async with get_db_session() as db:
            repo = RecommendationRepository(db)
            await repo.create(RecommendationCreate(
                match_id=match_id,
                market=rec.market,
                outcome=rec.outcome,
                bookmaker=rec.bookmaker,
                odd_decimal=rec.odd,
                model_probability=rec.model_probability,
                implied_probability=rec.implied_probability,
                value=rec.value,
                kelly_fraction=rec.kelly_fraction,
                stake_euros=rec.stake_euros,
                confidence=rec.confidence,
                reasoning=analysis.reasoning[:1000],  # Limit size
                status="PENDING",
            ))
            
            logger.info(
                "Recommendation saved",
                match_id=str(match_id),
                market=rec.market,
                outcome=rec.outcome,
                value=rec.value,
            )
