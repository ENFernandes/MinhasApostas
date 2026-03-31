"""Analysis service that coordinates data and LLM for recommendations."""

import json
import os
import re
from datetime import datetime
from uuid import UUID

from sqlalchemy.ext.asyncio import AsyncSession
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
from app.services.stats.elo import get_player_elo, probabilidade_vitoria_elo
from app.services.stats.form import (
    calcular_forma_recente,
    calcular_h2h,
    get_recent_games_for_poisson,
)
from app.services.stats.poisson import calculate_lambda, calculate_poisson_probabilities

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
                match = await match_repo.get_by_id(UUID(match_id))

                if not match:
                    logger.error("Match not found", match_id=match_id)
                    return None

                # Get odds for match
                odds_repo = OddsRepository(db)
                odds = await odds_repo.get_by_match(match_id)

                # Build match data object
                match_data = MatchData(
                    id=str(match.id),
                    sport=match.sport,
                    home_team=match.home_team.name if match.home_team else "Unknown",
                    away_team=match.away_team.name if match.away_team else "Unknown",
                    commence_time=match.commence_time,
                    is_live=match.status in ("LIVE", "IN_PLAY", "PAUSED"),
                    competition=match.competition.name if match.competition else None,
                )

                from app.models.schemas import OddsData
                odds_data = [
                    OddsData(
                        bookmaker=o.bookmaker,
                        market=o.market,
                        outcome=o.outcome,
                        odd=o.odd_decimal,
                    )
                    for o in odds
                ]

                request = AnalysisRequest(
                    match=match_data,
                    odds=odds_data,
                )

                # Perform analysis (db session threaded through for Poisson/form/ELO)
                result = await self._perform_analysis(request, db)

                if result and result.recommended_market:
                    await self._save_recommendation(match.id, result)

                return result

        except Exception as e:
            logger.error("Error analyzing match", match_id=match_id, error=str(e))
            return None

    async def _perform_analysis(self, request: AnalysisRequest, db: AsyncSession) -> AnalysisResponse:
        """Perform the actual analysis using LLM and statistical models."""
        match = request.match

        # Calculate model probabilities (async — tries Poisson/ELO, falls back to implied)
        probs = await self._calculate_probabilities(match, request.odds, db)

        # Build extra context (forma, H2H)
        context_flags = await self._build_context(match, db)
        context_flags["data_source"] = probs.data_source

        # Build prompt for LLM (inclui forma e H2H)
        prompt = self._build_analysis_prompt(match, request.odds, probs, context_flags)

        # Get LLM analysis
        if self.llm_provider:
            try:
                llm_response = await self.llm_provider.complete(
                    prompt=prompt,
                    temperature=0.3,
                    max_tokens=1000,
                )
                reasoning = self._parse_llm_reasoning(llm_response["content"])
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
            context_flags=context_flags,
            generated_at=datetime.utcnow(),
            llm_provider=llm_provider_name,
            llm_model=llm_model,
        )

    async def _calculate_probabilities(
        self,
        match: MatchData,
        odds: list,
        db: AsyncSession,
    ) -> ModelProbabilities:
        """Calcula probabilidades — tenta Poisson/ELO, cai para implied probability."""

        # ── Ténis: usar ELO da view latest_player_elo ──────────────────────────
        if match.sport == "tennis":
            try:
                surface = "Hard"  # default; idealmente viria do match
                elo_home = await get_player_elo(db, match.home_team, surface)
                elo_away = await get_player_elo(db, match.away_team, surface)
                prob_home = probabilidade_vitoria_elo(elo_home, elo_away)
                logger.info(
                    "ELO probabilities calculated",
                    home=match.home_team,
                    away=match.away_team,
                    elo_home=elo_home,
                    elo_away=elo_away,
                )
                return ModelProbabilities(
                    home=round(prob_home, 4),
                    draw=0.0,
                    away=round(1.0 - prob_home, 4),
                    over_2_5=0.0,
                    btts=0.0,
                    data_source=f"elo_{surface.lower()}",
                )
            except Exception as e:
                logger.warning("ELO calculation failed, using implied probability", error=str(e))

        # ── Futebol: tentar Poisson com dados históricos ────────────────────────
        if match.sport == "football":
            try:
                home_games = await get_recent_games_for_poisson(
                    db, match.home_team, is_home=True, n_jogos=10
                )
                away_games = await get_recent_games_for_poisson(
                    db, match.away_team, is_home=False, n_jogos=10
                )

                if home_games and away_games:
                    lambda_home = calculate_lambda(
                        match.home_team, is_home=True, recent_games=home_games
                    )
                    lambda_away = calculate_lambda(
                        match.away_team, is_home=False, recent_games=away_games
                    )
                    poisson_probs = calculate_poisson_probabilities(lambda_home, lambda_away)

                    logger.info(
                        "Poisson probabilities calculated",
                        home=match.home_team,
                        away=match.away_team,
                        lambda_home=lambda_home,
                        lambda_away=lambda_away,
                    )
                    return ModelProbabilities(
                        home=poisson_probs["1"],
                        draw=poisson_probs["X"],
                        away=poisson_probs["2"],
                        over_2_5=poisson_probs["over_2_5"],
                        btts=poisson_probs["btts"],
                        data_source="poisson_historical",
                    )
            except Exception as e:
                logger.warning("Poisson calculation failed, using implied probability", error=str(e))

        # ── Fallback: implied probability das odds ──────────────────────────────
        return self._calculate_from_implied_probability(match, odds)

    def _calculate_from_implied_probability(
        self, match: MatchData, odds: list
    ) -> ModelProbabilities:
        """Calcula probabilidades a partir da implied probability das odds (sem vig)."""
        home_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("1", match.home_team)]
        draw_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("X", "Draw")]
        away_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("2", match.away_team)]

        if home_odds and draw_odds and away_odds:
            avg_home = sum(home_odds) / len(home_odds)
            avg_draw = sum(draw_odds) / len(draw_odds)
            avg_away = sum(away_odds) / len(away_odds)
            total = (1 / avg_home) + (1 / avg_draw) + (1 / avg_away)
            home_prob = (1 / avg_home) / total
            draw_prob = (1 / avg_draw) / total
            away_prob = (1 / avg_away) / total
        else:
            home_prob, draw_prob, away_prob = 0.40, 0.30, 0.30

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
            over_2_5_prob = 1 / (sum(over25_odds) / len(over25_odds))
        else:
            balance = 1 - abs(home_prob - away_prob)
            over_2_5_prob = 0.45 + (balance * 0.10)

        btts_yes_odds = [
            o.odd for o in odds
            if o.market == "btts" and o.outcome.lower() in ("yes", "sim")
        ]
        btts_no_odds = [
            o.odd for o in odds
            if o.market == "btts" and o.outcome.lower() in ("no", "não", "nao")
        ]

        if btts_yes_odds and btts_no_odds:
            avg_btts_yes = sum(btts_yes_odds) / len(btts_yes_odds)
            avg_btts_no = sum(btts_no_odds) / len(btts_no_odds)
            total_btts = (1 / avg_btts_yes) + (1 / avg_btts_no)
            btts_prob = (1 / avg_btts_yes) / total_btts
        elif btts_yes_odds:
            btts_prob = 1 / (sum(btts_yes_odds) / len(btts_yes_odds))
        else:
            weaker = min(home_prob, away_prob)
            btts_prob = 0.38 + (weaker * 0.40)

        return ModelProbabilities(
            home=round(home_prob, 3),
            draw=round(draw_prob, 3),
            away=round(away_prob, 3),
            over_2_5=round(min(over_2_5_prob, 0.85), 3),
            btts=round(min(btts_prob, 0.80), 3),
            data_source="implied_probability",
        )

    async def _build_context(self, match: MatchData, db: AsyncSession) -> dict:
        """Constrói contexto adicional (forma recente, H2H e ELO) para enviar ao LLM."""
        context: dict = {}

        if match.sport == "tennis":
            try:
                surface = "Hard"  # default; idealmente viria do match
                elo_home = await get_player_elo(db, match.home_team, surface)
                elo_away = await get_player_elo(db, match.away_team, surface)
                context["player1_elo"] = round(elo_home, 0)
                context["player2_elo"] = round(elo_away, 0)
                context["surface"] = surface
                context["elo_diff"] = round(elo_home - elo_away, 1)
            except Exception as e:
                logger.warning("Failed to build ELO context for tennis", error=str(e))
            return context

        if match.sport != "football":
            return context

        try:
            home_form = await calcular_forma_recente(db, match.home_team, is_home=True)
            away_form = await calcular_forma_recente(db, match.away_team, is_home=False)
            h2h = await calcular_h2h(db, match.home_team, match.away_team)

            context["home_form"] = home_form.get("form_string", "")
            context["home_avg_scored"] = home_form.get("avg_goals_scored", 0.0)
            context["home_avg_conceded"] = home_form.get("avg_goals_conceded", 0.0)
            context["away_form"] = away_form.get("form_string", "")
            context["away_avg_scored"] = away_form.get("avg_goals_scored", 0.0)
            context["away_avg_conceded"] = away_form.get("avg_goals_conceded", 0.0)
            context["h2h_games"] = h2h.get("games", 0)
            context["h2h_home_wins"] = h2h.get("home_wins", 0)
            context["h2h_draws"] = h2h.get("draws", 0)
            context["h2h_away_wins"] = h2h.get("away_wins", 0)
            context["h2h_avg_goals"] = h2h.get("avg_total_goals", 0.0)
        except Exception as e:
            logger.warning("Failed to build form/H2H context", error=str(e))

        return context

    def _build_analysis_prompt(
        self,
        match: MatchData,
        odds: list,
        probs: ModelProbabilities,
        context: dict,
    ) -> str:
        """Build prompt for LLM analysis — sport-specific with explicit JSON schema."""
        odds_text = "\n".join([
            f"- {o.bookmaker} | {o.market} - {o.outcome}: @{o.odd}"
            for o in odds[:20]
        ])

        if match.sport == "tennis":
            return self._build_tennis_prompt(match, odds_text, probs, context)

        # Football prompt
        probs_text = (
            f"home={probs.home:.3f}  draw={probs.draw:.3f}  away={probs.away:.3f}\n"
            f"Over2.5={probs.over_2_5:.3f}  BTTS={probs.btts:.3f}\n"
            f"Fonte: {probs.data_source}"
        )

        home_form_text = ""
        away_form_text = ""
        h2h_text = ""

        if context.get("home_form"):
            home_form_text = (
                f"Forma recente {match.home_team} (casa, últimos 5): {context['home_form']}\n"
                f"Médias: {context.get('home_avg_scored', 0):.1f} golos marcados, "
                f"{context.get('home_avg_conceded', 0):.1f} sofridos"
            )
        if context.get("away_form"):
            away_form_text = (
                f"Forma recente {match.away_team} (fora, últimos 5): {context['away_form']}\n"
                f"Médias: {context.get('away_avg_scored', 0):.1f} golos marcados, "
                f"{context.get('away_avg_conceded', 0):.1f} sofridos"
            )
        if context.get("h2h_games", 0) > 0:
            h2h_text = (
                f"H2H últimos {context['h2h_games']} encontros: "
                f"{match.home_team} {context['h2h_home_wins']}V "
                f"{context['h2h_draws']}E "
                f"{context['h2h_away_wins']}D — "
                f"média {context.get('h2h_avg_goals', 0):.1f} golos/jogo"
            )

        context_section = "\n".join(
            filter(None, [home_form_text, away_form_text, h2h_text])
        ) or "Sem dados históricos disponíveis"

        return f"""Você é um analista especializado em apostas desportivas com foco em value betting.

PARTIDA:
{match.home_team} vs {match.away_team}
Competição: {match.competition or 'N/A'}
Data: {match.commence_time.strftime('%d/%m/%Y %H:%M')}

PROBABILIDADES DO MODELO ESTATÍSTICO:
{probs_text}

CONTEXTO HISTÓRICO:
{context_section}

ODDS DISPONÍVEIS:
{odds_text}

Regras:
- Só recomendar apostas com value ≥ 5%
- Odds entre 1.50 e 8.00 apenas
- Confiança mínima de 6/10 para emitir recomendação
- Se não há value claro, use "recommended_market": null

Responda APENAS com este JSON exato, sem texto adicional:
{{
  "reasoning": "Análise em português, máximo 3 frases.",
  "recommended_market": {{
    "market": "1X2",
    "outcome": "1",
    "confidence": 7
  }},
  "alternative_markets": []
}}"""

    def _build_tennis_prompt(
        self,
        match: MatchData,
        odds_text: str,
        probs: ModelProbabilities,
        context: dict,
    ) -> str:
        """Build LLM prompt specifically for tennis matches."""
        elo_section = ""
        if context.get("player1_elo"):
            elo_diff = context.get("elo_diff", 0)
            elo_section = (
                f"ELO ({context.get('surface', 'Hard')}): "
                f"{match.home_team}={context['player1_elo']:.0f}  "
                f"{match.away_team}={context['player2_elo']:.0f}  "
                f"Diferença={elo_diff:+.0f}"
            )
        else:
            elo_section = "ELO: Dados não disponíveis"

        return f"""Você é um analista especializado em apostas de ténis com foco em value betting.

PARTIDA:
{match.home_team} vs {match.away_team}
Torneio: {match.competition or 'N/A'}
Data: {match.commence_time.strftime('%d/%m/%Y %H:%M')}

PROBABILIDADES DO MODELO (ELO):
{match.home_team}={probs.home:.3f}  {match.away_team}={probs.away:.3f}
Fonte: {probs.data_source}

CONTEXTO:
{elo_section}
Nota: Ténis não tem empate.

ODDS DISPONÍVEIS:
{odds_text}

Regras:
- Só recomendar apostas com value ≥ 5%
- Odds entre 1.20 e 6.00 apenas
- Confiança mínima de 6/10 para emitir recomendação
- Mercado principal é "1X2" com outcome "1" (vitória {match.home_team}) ou "2" (vitória {match.away_team})
- Se não há value claro, use "recommended_market": null

Responda APENAS com este JSON exato, sem texto adicional:
{{
  "reasoning": "Análise em português, máximo 3 frases.",
  "recommended_market": {{
    "market": "1X2",
    "outcome": "1",
    "confidence": 7
  }},
  "alternative_markets": []
}}"""

    def _parse_llm_reasoning(self, content: str) -> str:
        """Extract the reasoning field from the LLM JSON response, with fallback."""
        try:
            # Try to find JSON block (handles text before/after)
            json_match = re.search(r'\{.*\}', content, re.DOTALL)
            if json_match:
                parsed = json.loads(json_match.group())
                return parsed.get("reasoning", content)
        except (json.JSONDecodeError, AttributeError):
            pass
        return content[:500]  # fallback: truncate raw content

    def _build_prob_map(self, odds: list, probs: ModelProbabilities) -> dict:
        """Build probability map supporting both 1X2 (football) and h2h (tennis) markets."""
        # Collect team names from h2h odds to map them to home/away
        home_names: set[str] = set()
        away_names: set[str] = set()
        for o in odds:
            if o.market == "h2h":
                if o.outcome not in ("X", "Draw"):
                    # First unique non-draw outcome is home, second is away
                    if not home_names:
                        home_names.add(o.outcome)
                    elif o.outcome not in home_names:
                        away_names.add(o.outcome)

        prob_map: dict = {
            ("1X2", "1"): probs.home,
            ("1X2", "X"): probs.draw,
            ("1X2", "2"): probs.away,
        }

        # Add h2h entries with player names (normalized by OddsCollectorJob as "1"/"2")
        prob_map[("h2h", "1")] = probs.home
        prob_map[("h2h", "2")] = probs.away
        for name in home_names:
            prob_map[("h2h", name)] = probs.home
        for name in away_names:
            prob_map[("h2h", name)] = probs.away

        return prob_map

    def _calc_confidence(self, value: float, model_prob: float, odd: float) -> int:
        """Recalibrated confidence: edge + probability strength + safe odd range."""
        edge_score = min(value * 20, 5)           # 25% edge → 5 pts; 5% edge → 1 pt
        odd_score = 1.0 if 1.50 <= odd <= 3.50 else 0.0   # safe odds range
        prob_score = min(model_prob * 10, 4.0)    # high probability = more confidence
        return max(1, min(10, round(edge_score + odd_score + prob_score)))

    def _find_best_value_bet(
        self,
        odds: list,
        probs: ModelProbabilities,
    ) -> RecommendedMarket | None:
        """Find the best value bet based on odds and probabilities."""
        best_value = -999
        best_bet = None
        prob_map = self._build_prob_map(odds, probs)

        for odd in odds:
            key = (odd.market, odd.outcome)
            if key not in prob_map:
                continue

            model_prob = prob_map[key]
            implied_prob = 1 / odd.odd
            value = model_prob - implied_prob

            if value > 0:
                kelly = (model_prob * odd.odd - 1) / (odd.odd - 1)
                bankroll = float(os.getenv("DEFAULT_BANKROLL", "100"))
                max_stake_pct = float(os.getenv("MAX_STAKE_PCT", "0.05"))
                stake = min(kelly * bankroll, bankroll * max_stake_pct)
            else:
                kelly = 0
                stake = 0

            if value > best_value and value > 0.05:
                best_value = value
                confidence = self._calc_confidence(value, model_prob, odd.odd)

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
                    confidence=confidence,
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
        prob_map = self._build_prob_map(odds, probs)

        for odd in odds:
            if exclude and odd.market == exclude.market and odd.outcome == exclude.outcome:
                continue

            key = (odd.market, odd.outcome)
            if key not in prob_map:
                continue

            model_prob = prob_map[key]
            implied_prob = 1 / odd.odd
            value = model_prob - implied_prob

            if value > 0.03:
                confidence = self._calc_confidence(value, model_prob, odd.odd)
                alternatives.append(AlternativeMarket(
                    market=odd.market,
                    outcome=odd.outcome,
                    odd=odd.odd,
                    value=round(value, 3),
                    confidence=confidence,
                ))

        return alternatives[:3]

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
                reasoning=analysis.reasoning[:1000],
                status="PENDING",
            ))

            logger.info(
                "Recommendation saved",
                match_id=str(match_id),
                market=rec.market,
                outcome=rec.outcome,
                value=rec.value,
            )
