"""Team form calculation and head-to-head analysis."""

from datetime import datetime, timedelta
from typing import TypedDict

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession


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


async def get_recent_games_for_poisson(
    db: AsyncSession,
    team_name: str,
    is_home: bool,
    n_jogos: int = 10,
) -> list[dict]:
    """Busca jogos recentes de historical_matches para calibrar o modelo Poisson.

    Returns:
        Lista de dicts com goals_scored, goals_conceded, is_home.
    """
    if is_home:
        query = text("""
            SELECT home_goals AS goals_scored, away_goals AS goals_conceded, TRUE AS is_home
            FROM historical_matches
            WHERE home_team ILIKE :team AND home_goals IS NOT NULL
            ORDER BY match_date DESC NULLS LAST
            LIMIT :n
        """)
    else:
        query = text("""
            SELECT away_goals AS goals_scored, home_goals AS goals_conceded, FALSE AS is_home
            FROM historical_matches
            WHERE away_team ILIKE :team AND away_goals IS NOT NULL
            ORDER BY match_date DESC NULLS LAST
            LIMIT :n
        """)

    rows = (await db.execute(query, {"team": f"%{team_name}%", "n": n_jogos})).fetchall()
    return [
        {
            "goals_scored": r.goals_scored,
            "goals_conceded": r.goals_conceded,
            "is_home": r.is_home,
        }
        for r in rows
    ]


async def calcular_forma_recente(
    db: AsyncSession,
    team_name: str,
    is_home: bool,
    n_jogos: int = 5,
) -> dict:
    """Calcula a forma recente de uma equipa para contexto do LLM.

    Fonte primária: historical_matches (football-data.co.uk)
    Resultado normalizado para a perspectiva da equipa: V/E/D (Vitória/Empate/Derrota).
    """
    if is_home:
        query = text("""
            SELECT home_goals AS goals_scored, away_goals AS goals_conceded,
                   result, match_date, away_team AS opponent
            FROM historical_matches
            WHERE home_team ILIKE :team AND home_goals IS NOT NULL
            ORDER BY match_date DESC NULLS LAST
            LIMIT :n
        """)
    else:
        query = text("""
            SELECT away_goals AS goals_scored, home_goals AS goals_conceded,
                   result, match_date, home_team AS opponent
            FROM historical_matches
            WHERE away_team ILIKE :team AND away_goals IS NOT NULL
            ORDER BY match_date DESC NULLS LAST
            LIMIT :n
        """)

    rows = (await db.execute(query, {"team": f"%{team_name}%", "n": n_jogos})).fetchall()

    if not rows:
        return {
            "team": team_name,
            "venue": "home" if is_home else "away",
            "games": 0,
            "avg_goals_scored": 0.0,
            "avg_goals_conceded": 0.0,
            "form_string": "",
        }

    form_chars: list[str] = []
    for r in rows:
        # result: 'H' = home win, 'D' = draw, 'A' = away win
        if is_home:
            char = "V" if r.result == "H" else ("E" if r.result == "D" else "D")
        else:
            char = "V" if r.result == "A" else ("E" if r.result == "D" else "D")
        form_chars.append(char)

    avg_scored = sum(r.goals_scored for r in rows) / len(rows)
    avg_conceded = sum(r.goals_conceded for r in rows) / len(rows)

    return {
        "team": team_name,
        "venue": "home" if is_home else "away",
        "games": len(rows),
        "avg_goals_scored": round(avg_scored, 2),
        "avg_goals_conceded": round(avg_conceded, 2),
        "form_string": " ".join(form_chars),
    }


async def calcular_h2h(
    db: AsyncSession,
    home_team: str,
    away_team: str,
    n_jogos: int = 5,
) -> dict:
    """Calcula o histórico de confrontos directos entre duas equipas.

    Fonte: historical_matches
    Resultados normalizados para a perspectiva de home_team vs away_team.
    """
    query = text("""
        SELECT home_team, away_team, home_goals, away_goals, result, match_date
        FROM historical_matches
        WHERE (home_team ILIKE :home AND away_team ILIKE :away)
           OR (home_team ILIKE :away AND away_team ILIKE :home)
        ORDER BY match_date DESC NULLS LAST
        LIMIT :n
    """)

    rows = (await db.execute(query, {
        "home": f"%{home_team}%",
        "away": f"%{away_team}%",
        "n": n_jogos,
    })).fetchall()

    if not rows:
        return {
            "home_team": home_team,
            "away_team": away_team,
            "games": 0,
            "home_wins": 0,
            "draws": 0,
            "away_wins": 0,
            "avg_total_goals": 0.0,
        }

    home_wins = 0
    draws = 0
    away_wins = 0
    total_goals = 0

    for r in rows:
        total_goals += (r.home_goals or 0) + (r.away_goals or 0)
        home_is_requested_home = home_team.lower() in r.home_team.lower()
        if r.result == "D":
            draws += 1
        elif r.result == "H":
            if home_is_requested_home:
                home_wins += 1
            else:
                away_wins += 1
        else:  # 'A'
            if home_is_requested_home:
                away_wins += 1
            else:
                home_wins += 1

    return {
        "home_team": home_team,
        "away_team": away_team,
        "games": len(rows),
        "home_wins": home_wins,
        "draws": draws,
        "away_wins": away_wins,
        "avg_total_goals": round(total_goals / len(rows), 2),
    }


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
