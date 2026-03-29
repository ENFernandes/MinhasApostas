"""Team form calculation and head-to-head analysis."""

from datetime import datetime, timedelta
from typing import TypedDict


class MatchResult(TypedDict):
    """Type definition for a match result."""

    date: datetime
    opponent: str
    is_home: bool
    goals_scored: int
    goals_conceded: int
    result: str  # W, D, L


class FormData(TypedDict):
    """Type definition for team form data."""

    team_id: str
    team_name: str
    last_10_games: list[MatchResult]
    avg_goals_scored_home: float
    avg_goals_scored_away: float
    avg_goals_conceded_home: float
    avg_goals_conceded_away: float
    win_rate: float
    form_score: float  # Weighted recent performance


def calculate_form(
    team_id: str,
    team_name: str,
    recent_matches: list[dict],
    n_games: int = 10,
) -> FormData:
    """Calculate recent form for a team.

    Args:
        team_id: Unique identifier for the team
        team_name: Name of the team
        recent_matches: List of recent match results
        n_games: Number of games to consider

    Returns:
        FormData with calculated statistics
    """
    # Sort by date descending and take last n games
    sorted_matches = sorted(
        recent_matches,
        key=lambda x: x.get("date", datetime.min),
        reverse=True,
    )[:n_games]

    results: list[MatchResult] = []
    home_goals_scored = []
    home_goals_conceded = []
    away_goals_scored = []
    away_goals_conceded = []
    wins = 0
    form_score = 0.0

    for i, match in enumerate(sorted_matches):
        is_home = match.get("is_home", False)
        goals_scored = match.get("goals_scored", 0)
        goals_conceded = match.get("goals_conceded", 0)

        # Determine result
        if goals_scored > goals_conceded:
            result = "W"
            wins += 1
        elif goals_scored < goals_conceded:
            result = "L"
        else:
            result = "D"

        # Weighted form score (recent games count more)
        weight = 1.0 - (i * 0.1)  # Decay factor
        if result == "W":
            form_score += 3 * weight
        elif result == "D":
            form_score += 1 * weight
        # Loss = 0 points

        results.append({
            "date": match.get("date", datetime.now()),
            "opponent": match.get("opponent", "Unknown"),
            "is_home": is_home,
            "goals_scored": goals_scored,
            "goals_conceded": goals_conceded,
            "result": result,
        })

        if is_home:
            home_goals_scored.append(goals_scored)
            home_goals_conceded.append(goals_conceded)
        else:
            away_goals_scored.append(goals_scored)
            away_goals_conceded.append(goals_conceded)

    n = len(sorted_matches) if sorted_matches else 1

    return {
        "team_id": team_id,
        "team_name": team_name,
        "last_10_games": results,
        "avg_goals_scored_home": (
            sum(home_goals_scored) / len(home_goals_scored) if home_goals_scored else 0.0
        ),
        "avg_goals_scored_away": (
            sum(away_goals_scored) / len(away_goals_scored) if away_goals_scored else 0.0
        ),
        "avg_goals_conceded_home": (
            sum(home_goals_conceded) / len(home_goals_conceded) if home_goals_conceded else 0.0
        ),
        "avg_goals_conceded_away": (
            sum(away_goals_conceded) / len(away_goals_conceded) if away_goals_conceded else 0.0
        ),
        "win_rate": wins / n,
        "form_score": form_score,
    }


class H2HData(TypedDict):
    """Type definition for head-to-head data."""

    team_a: str
    team_b: str
    total_matches: int
    wins_a: int
    wins_b: int
    draws: int
    avg_goals: float
    btts_rate: float
    recent_results: list[dict]


def calculate_h2h(
    team_a: str,
    team_b: str,
    h2h_matches: list[dict],
    n_games: int = 5,
) -> H2HData:
    """Calculate head-to-head statistics.

    Args:
        team_a: ID of team A
        team_b: ID of team B
        h2h_matches: List of head-to-head match results
        n_games: Number of recent games to include in details

    Returns:
        H2HData with calculated statistics
    """
    wins_a = 0
    wins_b = 0
    draws = 0
    total_goals = 0
    btts_count = 0

    for match in h2h_matches:
        goals_a = match.get("goals_a", 0)
        goals_b = match.get("goals_b", 0)

        total_goals += goals_a + goals_b

        if goals_a > 0 and goals_b > 0:
            btts_count += 1

        if goals_a > goals_b:
            wins_a += 1
        elif goals_b > goals_a:
            wins_b += 1
        else:
            draws += 1

    total = len(h2h_matches) if h2h_matches else 1

    # Get recent results (sorted by date)
    recent = sorted(
        h2h_matches,
        key=lambda x: x.get("date", datetime.min),
        reverse=True,
    )[:n_games]

    return {
        "team_a": team_a,
        "team_b": team_b,
        "total_matches": len(h2h_matches),
        "wins_a": wins_a,
        "wins_b": wins_b,
        "draws": draws,
        "avg_goals": total_goals / total,
        "btts_rate": btts_count / total,
        "recent_results": recent,
    }


def calculate_xg(
    shots: int,
    shots_on_target: int,
    big_chances: int = 0,
) -> float:
    """Calculate expected goals (xG) from shot statistics.

    This is a simplified xG model. In production, you'd use
    a more sophisticated model with shot location data.

    Args:
        shots: Total shots taken
        shots_on_target: Shots on target
        big_chances: Big chances created

    Returns:
        Expected goals value
    """
    # Simplified model: 0.1 per shot, 0.3 per shot on target, 0.4 per big chance
    xg = (shots * 0.05) + (shots_on_target * 0.15) + (big_chances * 0.35)
    return round(xg, 2)


def calculate_average_xg(
    matches: list[dict],
    team_id: str,
    is_home: bool | None = None,
) -> float:
    """Calculate average xG for a team across matches.

    Args:
        matches: List of matches with xG data
        team_id: Team identifier
        is_home: Filter by home/away (None for all)

    Returns:
        Average xG per match
    """
    xg_values = []

    for match in matches:
        match_is_home = match.get("home_team_id") == team_id

        if is_home is not None and match_is_home != is_home:
            continue

        if match_is_home:
            xg = match.get("home_xg", 0)
        else:
            xg = match.get("away_xg", 0)

        xg_values.append(xg)

    return sum(xg_values) / len(xg_values) if xg_values else 0.0
