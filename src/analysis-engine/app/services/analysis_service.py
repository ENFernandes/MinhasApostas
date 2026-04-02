"""Analysis service that coordinates data and LLM for recommendations."""

import hashlib
import json
import os
import re
from datetime import datetime
from math import isfinite
from uuid import UUID

import redis.asyncio as aioredis

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
from app.services.stats.tennis_context import get_tennis_h2h, get_tennis_recent_results
from app.services.stats.poisson import calculate_lambda, calculate_poisson_probabilities
from app.services.stats.recommendation_gate import football_recommendation_allowed

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

    async def analyze_match(self, match_id: str, force_refresh: bool = False) -> AnalysisResponse | None:
        """Analyze a match and generate recommendation.

        Args:
            match_id: Match UUID string.
            force_refresh: If True, bypass Redis cache for the optional LLM summary segment.
        """
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
                result = await self._perform_analysis(request, db, force_refresh=force_refresh)

                if result and result.recommended_market:
                    await self._save_recommendation(match.id, result)

                return result

        except Exception as e:
            logger.error("Error analyzing match", match_id=match_id, error=str(e))
            return None

    async def _perform_analysis(
        self,
        request: AnalysisRequest,
        db: AsyncSession,
        *,
        force_refresh: bool = False,
    ) -> AnalysisResponse:
        """Perform the actual analysis using LLM and statistical models."""
        match = request.match

        # Calculate model probabilities (async — tries Poisson/ELO, falls back to implied)
        probs = await self._calculate_probabilities(match, request.odds, db)

        # Build extra context (forma, H2H)
        context_flags = await self._build_context(match, db)
        context_flags["data_source"] = probs.data_source

        odds_status, llm_summary_allowed = self._odds_data_quality(match, request.odds)
        context_flags["odds_status"] = odds_status

        # Find best value bet
        recommended = self._find_best_value_bet(request.odds, probs, match.sport)
        alternatives = self._find_alternative_bets(request.odds, probs, match.sport, recommended)
        recommended, alternatives, gate_note = self._apply_football_recommendation_gates(
            match.sport, probs, recommended, alternatives
        )
        if gate_note:
            context_flags["recommendation_gate"] = gate_note

        # Deterministic, auditable reasoning (stats -> odds -> calculations -> decision)
        reasoning = self._build_reasoning(
            match,
            probs,
            request.odds,
            context_flags,
            recommended,
            alternatives,
            odds_status,
        )
        if gate_note:
            reasoning = f"{reasoning}\n\n[Gates] {gate_note}"

        # Optional LLM summary (never ground truth; constrained to computed numbers)
        llm_provider_name = "none"
        llm_model = "none"
        llm_enabled = os.getenv("LLM_ANALYSIS_SUMMARY", "1").lower() in ("1", "true", "yes")
        if self.llm_provider and llm_enabled and llm_summary_allowed:
            try:
                cache_key = self._llm_cache_key(match.id, request.odds) + ":summary"
                cached = None if force_refresh else await self._get_llm_cache(cache_key)
                if cached:
                    llm_provider_name = "cache"
                    llm_model = "cached"
                    llm_summary = cached
                else:
                    prompt = self._build_llm_summary_prompt(
                        match, probs, context_flags, recommended, alternatives, odds_status
                    )
                    llm_response = await self.llm_provider.complete(
                        prompt=prompt,
                        temperature=0.2,
                        max_tokens=250,
                    )
                    llm_provider_name = self.llm_provider.name
                    llm_model = self.llm_provider.model
                    llm_summary = self._sanitize_llm_summary(llm_response.get("content", ""))
                    await self._set_llm_cache(cache_key, llm_summary)

                if llm_summary:
                    reasoning = f"{reasoning}\n\nResumo (LLM): {llm_summary}"
            except Exception as e:
                logger.warning("LLM summary failed; keeping deterministic reasoning", error=str(e))

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

    def _llm_cache_key(self, match_id: str, odds: list) -> str:
        """Gera uma chave de cache única baseada no match_id e snapshot das odds."""
        odds_signature = hashlib.md5(
            json.dumps(
                sorted(
                    [
                        {"b": o.bookmaker, "m": o.market, "o": o.outcome, "v": round(o.odd, 3)}
                        for o in odds
                    ],
                    key=lambda x: f"{x['b']}{x['m']}{x['o']}",
                )
            ).encode()
        ).hexdigest()[:8]
        return f"llm:{match_id}:{odds_signature}"

    async def _get_llm_cache(self, key: str) -> str | None:
        """Tenta obter resultado cacheado do Redis. Retorna None se não existir ou Redis indisponível."""
        try:
            redis_url = os.getenv("REDIS_URL", "redis://localhost:6379")
            client = aioredis.from_url(redis_url, decode_responses=True)
            value = await client.get(key)
            await client.aclose()
            return value
        except Exception:
            return None

    async def _set_llm_cache(self, key: str, value: str, ttl: int = 3600) -> None:
        """Guarda resultado LLM no Redis com TTL de 1h. Falha silenciosa."""
        try:
            redis_url = os.getenv("REDIS_URL", "redis://localhost:6379")
            client = aioredis.from_url(redis_url, decode_responses=True)
            await client.setex(key, ttl, value)
            await client.aclose()
        except Exception:
            pass

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

        # ── Futebol: ML (XGBoost) → Poisson → implied probability ─────────────────
        if match.sport == "football":
            # 1. Tentar modelo ML se disponível
            try:
                from app.services.stats.feature_engineering import (
                    build_features_from_context,
                    strength_ratings_from_recent_games,
                )
                from app.services.stats.ml_model import MLModelService

                implied = self._calculate_from_implied_probability(match, odds)
                home_games_ml = await get_recent_games_for_poisson(
                    db, match.home_team, is_home=True, n_jogos=10
                )
                away_games_ml = await get_recent_games_for_poisson(
                    db, match.away_team, is_home=False, n_jogos=10
                )

                home_elo, away_elo = strength_ratings_from_recent_games(
                    home_games_ml, away_games_ml
                )

                features = build_features_from_context(
                    home_games=home_games_ml,
                    away_games=away_games_ml,
                    h2h_games=[],
                    home_elo=home_elo,
                    away_elo=away_elo,
                    implied_home=implied.home,
                    implied_draw=implied.draw,
                    implied_away=implied.away,
                )

                ml_service = MLModelService.get()
                ml_probs = ml_service.predict(features)

                if ml_probs.data_source not in ("fallback_uniform", "cold_start", "cold_start_implied"):
                    logger.info(
                        "ML probabilities calculated",
                        home=match.home_team,
                        away=match.away_team,
                        source=ml_probs.data_source,
                    )
                    # Poisson para over/btts (ML não prediz mercados secundários)
                    poisson_sup = None
                    try:
                        if home_games_ml and away_games_ml:
                            lh = calculate_lambda(match.home_team, True, home_games_ml)
                            la = calculate_lambda(match.away_team, False, away_games_ml)
                            poisson_sup = calculate_poisson_probabilities(lh, la)
                    except Exception:
                        pass

                    return ModelProbabilities(
                        home=round(ml_probs.home, 4),
                        draw=round(ml_probs.draw, 4),
                        away=round(ml_probs.away, 4),
                        over_2_5=poisson_sup["over_2_5"] if poisson_sup else 0.5,
                        btts=poisson_sup["btts"] if poisson_sup else 0.4,
                        data_source=ml_probs.data_source,
                    )
            except Exception as e:
                logger.warning("ML model failed, falling back to Poisson", error=str(e))

            # 2. Fallback: Poisson com dados históricos
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
        # ── Ténis: 2-way (h2h), sem empate ─────────────────────────────────────
        if match.sport == "tennis":
            home_odds = [
                o.odd for o in odds
                if o.market == "h2h" and o.outcome in ("1", match.home_team)
            ]
            away_odds = [
                o.odd for o in odds
                if o.market == "h2h" and o.outcome in ("2", match.away_team)
            ]

            if home_odds and away_odds:
                avg_home = sum(home_odds) / len(home_odds)
                avg_away = sum(away_odds) / len(away_odds)
                total = (1 / avg_home) + (1 / avg_away)
                home_prob = (1 / avg_home) / total
                away_prob = (1 / avg_away) / total
            else:
                home_prob, away_prob = 0.5, 0.5

            return ModelProbabilities(
                home=round(home_prob, 3),
                draw=0.0,
                away=round(away_prob, 3),
                over_2_5=0.0,
                btts=0.0,
                data_source="implied_probability",
            )

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
            prob_source = "implied_probability"
        else:
            home_prob, draw_prob, away_prob = 0.40, 0.30, 0.30
            prob_source = "placeholder_1x2"

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
            data_source=prob_source,
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

            # Recent form (W/L) + H2H from local matches table (if available)
            try:
                home_recent = await get_tennis_recent_results(db, match.home_team, n_matches=5)
                away_recent = await get_tennis_recent_results(db, match.away_team, n_matches=5)

                context["home_recent_form"] = " ".join([r.result for r in home_recent])
                context["away_recent_form"] = " ".join([r.result for r in away_recent])
                context["home_recent_win_rate"] = round(
                    (sum(1 for r in home_recent if r.result == "W") / len(home_recent)) if home_recent else 0.0,
                    2,
                )
                context["away_recent_win_rate"] = round(
                    (sum(1 for r in away_recent if r.result == "W") / len(away_recent)) if away_recent else 0.0,
                    2,
                )
            except Exception as e:
                logger.warning("Failed to build recent form context for tennis", error=str(e))

            try:
                h2h = await get_tennis_h2h(db, match.home_team, match.away_team, n_matches=8)
                context["h2h_games"] = h2h.get("games", 0)
                context["h2h_home_wins"] = h2h.get("a_wins", 0)
                context["h2h_away_wins"] = h2h.get("b_wins", 0)
                context["h2h_recent"] = h2h.get("recent", [])
            except Exception as e:
                logger.warning("Failed to build H2H context for tennis", error=str(e))
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

        form_section = ""
        if context.get("home_recent_form") or context.get("away_recent_form"):
            form_section = (
                f"Forma recente (últimos 5):\n"
                f"- {match.home_team}: {context.get('home_recent_form', '')} (win rate {context.get('home_recent_win_rate', 0):.0%})\n"
                f"- {match.away_team}: {context.get('away_recent_form', '')} (win rate {context.get('away_recent_win_rate', 0):.0%})"
            )
        else:
            form_section = "Forma recente: sem dados locais suficientes"

        h2h_section = ""
        if context.get("h2h_games", 0) > 0:
            h2h_section = (
                f"H2H (últimos {context.get('h2h_games')}): "
                f"{match.home_team} {context.get('h2h_home_wins', 0)}V — "
                f"{match.away_team} {context.get('h2h_away_wins', 0)}V"
            )
        else:
            h2h_section = "H2H: sem dados locais suficientes"

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
{form_section}
{h2h_section}
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

    def _sanitize_llm_summary(self, content: str) -> str:
        """Sanitize any LLM output to a short, safe summary string."""
        text = (content or "").strip()
        if not text:
            return ""

        # Remove fenced code blocks and JSON-like dumps (regex must use \s \S, not \\s)
        text = re.sub(r"```[\s\S]*?```", "", text).strip()
        text = re.sub(r"\{[\s\S]*\}", "", text).strip()

        # Drop common chain-of-thought / meta prefixes
        for pat in (
            r"(?i)^\*\*analysis:\*\*\s*",
            r"(?i)^analysis:\s*",
            r"(?i)^wait[.,]?\s+",
            r"(?i)^let'?s\s+",
        ):
            text = re.sub(pat, "", text).strip()

        # Reject obvious JSON / schema dumps
        if text.startswith("{") or "analysis_reasoning" in text.lower():
            return ""

        # Collapse whitespace and keep it short
        text = re.sub(r"\s+", " ", text).strip()
        return text[:240]

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

    def _apply_football_recommendation_gates(
        self,
        sport: str,
        probs: ModelProbabilities,
        recommended: RecommendedMarket | None,
        alternatives: list[AlternativeMarket],
    ) -> tuple[RecommendedMarket | None, list[AlternativeMarket], str | None]:
        """Suprime recomendações espúrias (placeholder 1X2, underdog longo sem modelo fiável)."""
        if sport != "football":
            return recommended, alternatives, None

        notes: list[str] = []
        if recommended:
            ok, reason = football_recommendation_allowed(
                probs.data_source, recommended.market, recommended.odd
            )
            if not ok:
                if reason:
                    notes.append(reason)
                recommended = None

        filtered_alts: list[AlternativeMarket] = []
        dropped = 0
        for alt in alternatives:
            ok, _ = football_recommendation_allowed(probs.data_source, alt.market, alt.odd)
            if ok:
                filtered_alts.append(alt)
            else:
                dropped += 1
        if dropped:
            notes.append(
                f"{dropped} alternativa(s) removida(s) pelos gates de qualidade (fonte={probs.data_source})."
            )

        return recommended, filtered_alts, "\n".join(notes) if notes else None

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
        sport: str,
    ) -> RecommendedMarket | None:
        """Find the best value bet based on odds and probabilities, respecting sport constraints."""
        best_value = -999
        best_bet = None
        prob_map = self._build_prob_map(odds, probs)

        for odd in odds:
            if not self._is_supported_market(sport, odd.market, odd.outcome):
                continue
            if not isfinite(odd.odd) or odd.odd <= 1.0:
                continue
            if not self._is_odd_in_range(sport, odd.odd):
                continue

            key = (odd.market, odd.outcome)
            if key not in prob_map:
                continue

            model_prob = prob_map[key]
            if not isfinite(model_prob) or model_prob <= 0:
                continue
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
        sport: str,
        exclude: RecommendedMarket | None,
    ) -> list[AlternativeMarket]:
        """Find alternative value bets."""
        alternatives = []
        prob_map = self._build_prob_map(odds, probs)

        for odd in odds:
            if exclude and odd.market == exclude.market and odd.outcome == exclude.outcome:
                continue
            if not self._is_supported_market(sport, odd.market, odd.outcome):
                continue
            if not isfinite(odd.odd) or odd.odd <= 1.0:
                continue
            if not self._is_odd_in_range(sport, odd.odd):
                continue

            key = (odd.market, odd.outcome)
            if key not in prob_map:
                continue

            model_prob = prob_map[key]
            if not isfinite(model_prob) or model_prob <= 0:
                continue
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

    def _is_odd_in_range(self, sport: str, odd: float) -> bool:
        if sport == "tennis":
            return 1.20 <= odd <= 6.00
        return 1.50 <= odd <= 8.00

    def _is_supported_market(self, sport: str, market: str, outcome: str) -> bool:
        if sport == "tennis":
            # Ténis: winner (h2h) apenas. Sem empate.
            if market != "h2h":
                return False
            return outcome not in ("X", "Draw")

        # Futebol: principais + mercados secundários suportados
        if market == "1X2":
            return outcome in ("1", "X", "2")
        if market in ("totals", "over_under"):
            return "2.5" in str(outcome)
        if market == "btts":
            return str(outcome).lower() in ("yes", "sim", "no", "não", "nao")
        return False

    def _evaluate_supported_odds(self, sport: str, odds: list, probs: ModelProbabilities) -> list[dict]:
        prob_map = self._build_prob_map(odds, probs)
        out: list[dict] = []
        for o in odds:
            if not self._is_supported_market(sport, o.market, o.outcome):
                continue
            if not self._is_odd_in_range(sport, o.odd):
                continue
            key = (o.market, o.outcome)
            if key not in prob_map:
                continue
            model_prob = prob_map[key]
            if not isfinite(model_prob) or model_prob <= 0:
                continue
            implied_prob = 1 / o.odd
            value = model_prob - implied_prob
            out.append(
                {
                    "market": o.market,
                    "outcome": o.outcome,
                    "odd": o.odd,
                    "bookmaker": o.bookmaker,
                    "model_prob": model_prob,
                    "implied_prob": implied_prob,
                    "value": value,
                }
            )
        return out

    def _odds_data_quality(self, match: MatchData, odds: list) -> tuple[str, bool]:
        """Return (status_code, llm_summary_allowed).

        When odds are missing or incomplete, we must not call the LLM for a "summary" — it tends
        to invent decimal prices and chain-of-thought (see user-reported bad outputs).
        """
        if not odds:
            return ("no_odds", False)

        sport = match.sport
        if sport == "tennis":
            h2h = [o for o in odds if o.market == "h2h"]
            if not h2h:
                return ("tennis_no_h2h", False)
            has_home = any(o.outcome in ("1", match.home_team) for o in h2h)
            has_away = any(o.outcome in ("2", match.away_team) for o in h2h)
            if not (has_home and has_away):
                return ("tennis_h2h_incomplete", False)
            return ("ok", True)

        if any(self._is_supported_market(sport, o.market, o.outcome) for o in odds):
            return ("ok", True)
        return ("football_no_supported_markets", False)

    def _odds_status_message(self, code: str) -> str:
        messages = {
            "no_odds": (
                "ODDS: sem registos na base de dados — não é possível calcular implied probability "
                "nem value. Qualquer preço não listado é desconhecido; não inventar odds."
            ),
            "tennis_no_h2h": (
                "ODDS: não há mercado h2h (vencedor) — não é possível avaliar value face ao modelo."
            ),
            "tennis_h2h_incomplete": (
                "ODDS: o mercado h2h não tem cotação para ambos os jogadores — não é possível "
                "comparar Pmodelo com o mercado."
            ),
            "football_no_supported_markets": (
                "ODDS: não há mercados suportados (1X2 / Over2.5 / BTTS) — não é possível avaliar value."
            ),
            "ok": (
                "ODDS: dados suficientes para comparar probabilidade do modelo com odds do mercado."
            ),
        }
        return messages.get(code, f"ODDS: estado={code}")

    def _build_reasoning(
        self,
        match: MatchData,
        probs: ModelProbabilities,
        odds: list,
        context: dict,
        recommended: RecommendedMarket | None,
        alternatives: list[AlternativeMarket],
        odds_status: str,
    ) -> str:
        """Clear, auditable explanation based on stats and computed betting math."""
        sport = match.sport
        odds_line = self._odds_status_message(odds_status)

        if sport == "tennis":
            stats_bits = [
                f"Fonte={probs.data_source}",
                f"P({match.home_team})={probs.home:.1%}",
                f"P({match.away_team})={probs.away:.1%}",
            ]
            if context.get("player1_elo") and context.get("player2_elo"):
                stats_bits.append(
                    f"ELO({context.get('surface','Hard')}): {match.home_team} {context['player1_elo']:.0f} vs {match.away_team} {context['player2_elo']:.0f}"
                )
            if context.get("home_recent_form") or context.get("away_recent_form"):
                stats_bits.append(
                    f"Forma(5): {match.home_team} {context.get('home_recent_form','')} | {match.away_team} {context.get('away_recent_form','')}"
                )
            if context.get("h2h_games", 0) > 0:
                stats_bits.append(
                    f"H2H({context.get('h2h_games')}): {match.home_team} {context.get('h2h_home_wins',0)}–{context.get('h2h_away_wins',0)} {match.away_team}"
                )
            stats_line = " | ".join(stats_bits)
        else:
            stats_bits = [
                f"Fonte={probs.data_source}",
                f"1={probs.home:.1%}",
                f"X={probs.draw:.1%}",
                f"2={probs.away:.1%}",
                f"Over2.5={probs.over_2_5:.1%}",
                f"BTTS={probs.btts:.1%}",
            ]
            if context.get("home_form") or context.get("away_form"):
                stats_bits.append(
                    f"Forma: {match.home_team}({context.get('home_form','')}) vs {match.away_team}({context.get('away_form','')})"
                )
            if context.get("h2h_games", 0) > 0:
                stats_bits.append(
                    f"H2H({context.get('h2h_games')}): {context.get('h2h_home_wins',0)}-{context.get('h2h_draws',0)}-{context.get('h2h_away_wins',0)}"
                )
            stats_line = " | ".join(stats_bits)

        if odds_status != "ok":
            rec_line = (
                "Sem aposta (no bet): sem odds suficientes para calcular value — não recomendar apostas "
                "nem assumir preços hipotéticos."
            )
        elif recommended:
            rec_line = (
                f"Boa aposta: {recommended.market} {recommended.outcome} @ {recommended.odd:.2f} "
                f"({recommended.bookmaker}) | value=+{recommended.value:.1%} "
                f"(Pmodelo={recommended.model_probability:.1%} vs Pimplied={recommended.implied_probability:.1%}) "
                f"| Kelly={recommended.kelly_fraction:.1%} | stake≈€{recommended.stake_euros:.2f} | conf={recommended.confidence}/10"
            )
        else:
            rec_line = "Sem aposta (no bet): não há value ≥ 5% dentro das regras de odds/mercados."

        alt_lines: list[str] = []
        if alternatives:
            alt_lines.append("Alternativas (com algum value):")
            for a in alternatives[:3]:
                alt_lines.append(f"- {a.market} {a.outcome} | value=+{a.value:.1%} | conf={a.confidence}/10")

        bad_lines: list[str] = []
        evaluated = self._evaluate_supported_odds(match.sport, odds, probs)
        worst = sorted(evaluated, key=lambda x: x["value"])[:2]
        if worst:
            bad_lines.append("Más apostas (sem value):")
            for w in worst:
                bad_lines.append(
                    f"- {w['market']} {w['outcome']} @ {w['odd']:.2f} | value={w['value']:+.1%} "
                    f"(Pmodelo={w['model_prob']:.1%}, Pimplied={w['implied_prob']:.1%})"
                )

        return "\n".join([odds_line, f"Stats: {stats_line}", rec_line, *alt_lines, *bad_lines]).strip()

    def _build_llm_summary_prompt(
        self,
        match: MatchData,
        probs: ModelProbabilities,
        context: dict,
        recommended: RecommendedMarket | None,
        alternatives: list[AlternativeMarket],
        odds_status: str,
    ) -> str:
        rec = (
            f"{recommended.market} {recommended.outcome} @ {recommended.odd:.2f} (value +{recommended.value:.1%}, conf {recommended.confidence}/10)"
            if recommended
            else "NO BET"
        )
        alts = "; ".join([f"{a.market} {a.outcome} (+{a.value:.1%})" for a in alternatives[:3]]) or "none"
        return f"""Escreve um resumo muito curto (1-2 frases) em português.

Regras obrigatórias:
- Não inventes odds nem cenários ("suponhamos 1.80…").
- Não escrevas raciocínio em cadeia (proibido: "Wait", "Let's assume", "Hypothetical").
- Não uses Markdown, listas, JSON, nem blocos de código.
- Não contradigas a decisão já calculada abaixo.

Factos (única fonte):
- Estado das odds: {odds_status}
- Jogo: {match.home_team} vs {match.away_team} ({match.sport})
- Probabilidades modelo: home={probs.home:.3f} draw={probs.draw:.3f} away={probs.away:.3f} over2.5={probs.over_2_5:.3f} btts={probs.btts:.3f}
- Fonte: {probs.data_source}
- Decisão do sistema: {rec}
- Alternativas: {alts}

Responde só com texto corrido."""

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

        # Notifica o frontend via SSE e envia alerta Telegram se confiança >= threshold
        # Ambos falham silenciosamente para não bloquear o fluxo principal
        rec_payload = {
            "match_id": str(match_id),
            "market": rec.market,
            "outcome": rec.outcome,
            "odd": rec.odd,
            "value": rec.value,
            "confidence": rec.confidence,
            "kelly_fraction": rec.kelly_fraction,
            "stake_euros": rec.stake_euros,
        }
        try:
            from app.routers.events import publish_recommendation
            await publish_recommendation(rec_payload)
        except Exception:
            pass

        try:
            from app.services.alert_service import send_telegram_alert
            await send_telegram_alert(rec_payload)
        except Exception:
            pass
