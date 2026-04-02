"""Tests for football strength proxy from recent games."""

from app.services.stats.feature_engineering import strength_ratings_from_recent_games


def test_empty_games_neutral_baseline() -> None:
    h, a = strength_ratings_from_recent_games([], [])
    assert h == 1500.0 and a == 1500.0


def test_positive_net_raises_rating() -> None:
    home = [{"goals_scored": 3, "goals_conceded": 0}]
    away = [{"goals_scored": 0, "goals_conceded": 2}]
    h, a = strength_ratings_from_recent_games(home, away)
    assert h > 1500.0
    assert a < 1500.0
