"""Poisson distribution model for football match outcome prediction."""

import numpy as np
from scipy.stats import poisson


def calculate_lambda(
    team_id: str,
    is_home: bool,
    recent_games: list[dict],
    league_avg_goals: float = 2.5,
) -> float:
    """Calculate expected goals (lambda) for a team based on recent form.

    Args:
        team_id: Unique identifier for the team
        is_home: Whether the team is playing at home
        recent_games: List of recent games with goals scored/conceded
        league_avg_goals: Average goals per game in the league

    Returns:
        Expected goals (lambda) for the team
    """
    if not recent_games:
        return league_avg_goals / 2  # Default to league average

    # Apply decay factor (0.9 per game going back)
    goals = []
    weights = []

    for i, game in enumerate(reversed(recent_games)):
        if is_home and game.get("is_home"):
            goals.append(game.get("goals_scored", 0))
        elif not is_home and not game.get("is_home"):
            goals.append(game.get("goals_scored", 0))
        else:
            goals.append(game.get("goals_conceded", 0))

        weight = 0.9**i
        weights.append(weight)

    if not goals:
        return league_avg_goals / 2

    weighted_avg = np.average(goals, weights=weights)

    # Adjust for opponent strength if data available
    return max(0.1, weighted_avg)  # Minimum 0.1 to avoid zero probabilities


def calculate_poisson_probabilities(
    lambda_home: float,
    lambda_away: float,
    max_goals: int = 6,
) -> dict[str, float]:
    """Calculate probabilities for match outcomes using Poisson distribution.

    Args:
        lambda_home: Expected goals for home team
        lambda_away: Expected goals for away team
        max_goals: Maximum goals to calculate (default 6)

    Returns:
        Dictionary with probabilities for 1, X, 2, over_2.5, btts
    """
    # Create probability matrix for all scorelines
    matrix = np.zeros((max_goals + 1, max_goals + 1))

    for i in range(max_goals + 1):
        for j in range(max_goals + 1):
            matrix[i][j] = poisson.pmf(i, lambda_home) * poisson.pmf(j, lambda_away)

    # Calculate outcome probabilities
    prob_home = float(np.sum(np.tril(matrix, -1)))  # Home team wins
    prob_draw = float(np.sum(np.diag(matrix)))  # Draw
    prob_away = float(np.sum(np.triu(matrix, 1)))  # Away team wins

    # Calculate over 2.5 goals probability
    prob_over_25 = float(
        sum(
            matrix[i][j]
            for i in range(max_goals + 1)
            for j in range(max_goals + 1)
            if i + j > 2
        )
    )

    # Calculate BTTS (both teams to score) probability
    prob_btts = float(
        sum(
            matrix[i][j]
            for i in range(1, max_goals + 1)
            for j in range(1, max_goals + 1)
        )
    )

    return {
        "1": round(prob_home, 4),
        "X": round(prob_draw, 4),
        "2": round(prob_away, 4),
        "over_2_5": round(prob_over_25, 4),
        "btts": round(prob_btts, 4),
    }


def calculate_scoreline_probabilities(
    lambda_home: float,
    lambda_away: float,
    max_goals: int = 4,
) -> dict[str, float]:
    """Calculate probability for each exact scoreline.

    Args:
        lambda_home: Expected goals for home team
        lambda_away: Expected goals for away team
        max_goals: Maximum goals to calculate

    Returns:
        Dictionary mapping "home-away" to probability
    """
    probabilities = {}

    for i in range(max_goals + 1):
        for j in range(max_goals + 1):
            prob = poisson.pmf(i, lambda_home) * poisson.pmf(j, lambda_away)
            probabilities[f"{i}-{j}"] = round(float(prob), 4)

    return probabilities
