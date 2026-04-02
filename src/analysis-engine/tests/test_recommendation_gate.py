"""Tests for football recommendation quality gates."""

from app.services.stats.recommendation_gate import football_recommendation_allowed


def test_placeholder_blocks_all_markets() -> None:
    ok, reason = football_recommendation_allowed("placeholder_1x2", "1X2", 2.1)
    assert ok is False
    assert reason and "placeholder" in reason.lower()

    ok2, _ = football_recommendation_allowed("placeholder_1x2", "totals", 1.9)
    assert ok2 is False


def test_longshot_requires_trusted_source() -> None:
    ok, reason = football_recommendation_allowed("implied_probability", "1X2", 7.0)
    assert ok is False
    assert reason and "xgboost" in reason.lower()

    ok2, _ = football_recommendation_allowed("xgboost", "1X2", 7.0)
    assert ok2 is True

    ok3, _ = football_recommendation_allowed("poisson_historical", "1X2", 8.5)
    assert ok3 is True


def test_short_odds_allowed_with_implied() -> None:
    ok, _ = football_recommendation_allowed("implied_probability", "1X2", 2.5)
    assert ok is True


def test_non_1x2_allowed_when_implied() -> None:
    ok, _ = football_recommendation_allowed("implied_probability", "totals", 7.0)
    assert ok is True
