"""Regras de qualidade para expor recomendações de apostas (evitar value espúrio)."""

from __future__ import annotations

import os

# Fontes em que as probabilidades 1X2 são estimadas por modelo / histórico, não placeholder.
FOOTBALL_TRUSTED_1X2_SOURCES = frozenset({"xgboost", "poisson_historical"})

# Probabilidades 1X2 não fiáveis — não recomendar apostas.
FOOTBALL_BLOCK_ALL_SOURCES = frozenset({"placeholder_1x2"})


def football_longshot_min_odd() -> float:
    """Odd mínima para tratar 1X2 como 'underdog longo' (requer fonte confiável)."""
    return float(os.getenv("FOOTBALL_LONGSHOT_MIN_ODD", "6.0"))


def is_football_1x2_market(market: str) -> bool:
    return market in ("1X2", "h2h")


def football_recommendation_allowed(
    data_source: str,
    market: str,
    odd: float,
) -> tuple[bool, str | None]:
    """Indica se uma recomendação de mercado suportado é permitida para futebol.

    Returns:
        (allowed, reason_if_blocked)
    """
    if data_source in FOOTBALL_BLOCK_ALL_SOURCES:
        return False, (
            "Probabilidades 1X2 incompletas (placeholder) — recomendação suprimida. "
            "Confirma que existem odds 1X2 (1, X, 2) na BD."
        )

    if not is_football_1x2_market(market):
        return True, None

    if odd >= football_longshot_min_odd() and data_source not in FOOTBALL_TRUSTED_1X2_SOURCES:
        return False, (
            f"Aposta 1X2 com odd ≥ {football_longshot_min_odd():.1f} só é recomendada quando "
            "as probabilidades vêm de Poisson histórico ou modelo XGBoost — "
            f"fonte actual: {data_source}."
        )

    return True, None
