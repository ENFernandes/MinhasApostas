"""Tennis context helpers (recent form + head-to-head) from local DB matches.

We intentionally derive this from the `matches` table so it works for both
live-collected matches and matches created from odds events.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession


@dataclass(frozen=True)
class TennisRecentResult:
    date: datetime
    opponent: str
    is_home: bool
    sets_for: int
    sets_against: int
    result: str  # W/L


async def get_tennis_recent_results(
    db: AsyncSession,
    player_name: str,
    n_matches: int = 5,
) -> list[TennisRecentResult]:
    """Return recent finished matches for a player (W/L), using fuzzy name matching."""
    rows = (
        await db.execute(
            text(
                """
                SELECT
                    m.commence_time,
                    ht.name AS home_name,
                    at.name AS away_name,
                    m.home_score,
                    m.away_score
                FROM matches m
                JOIN teams ht ON ht.id = m.home_id
                JOIN teams at ON at.id = m.away_id
                WHERE m.sport = 'tennis'
                  AND m.status = 'FINISHED'
                  AND (
                        ht.name ILIKE :p
                     OR at.name ILIKE :p
                  )
                  AND m.home_score IS NOT NULL
                  AND m.away_score IS NOT NULL
                ORDER BY m.commence_time DESC
                LIMIT :n
                """
            ),
            {"p": f"%{player_name}%", "n": n_matches},
        )
    ).fetchall()

    results: list[TennisRecentResult] = []
    for r in rows:
        home = str(r.home_name)
        away = str(r.away_name)
        is_home = player_name.lower() in home.lower()

        sets_home = int(r.home_score or 0)
        sets_away = int(r.away_score or 0)

        if is_home:
            opponent = away
            sets_for, sets_against = sets_home, sets_away
        else:
            opponent = home
            sets_for, sets_against = sets_away, sets_home

        result = "W" if sets_for > sets_against else "L"
        results.append(
            TennisRecentResult(
                date=r.commence_time,
                opponent=opponent,
                is_home=is_home,
                sets_for=sets_for,
                sets_against=sets_against,
                result=result,
            )
        )

    return results


async def get_tennis_h2h(
    db: AsyncSession,
    player_a: str,
    player_b: str,
    n_matches: int = 5,
) -> dict:
    """Head-to-head stats for two players from finished matches."""
    rows = (
        await db.execute(
            text(
                """
                SELECT
                    m.commence_time,
                    ht.name AS home_name,
                    at.name AS away_name,
                    m.home_score,
                    m.away_score
                FROM matches m
                JOIN teams ht ON ht.id = m.home_id
                JOIN teams at ON at.id = m.away_id
                WHERE m.sport = 'tennis'
                  AND m.status = 'FINISHED'
                  AND (
                        (ht.name ILIKE :a AND at.name ILIKE :b)
                     OR (ht.name ILIKE :b AND at.name ILIKE :a)
                  )
                  AND m.home_score IS NOT NULL
                  AND m.away_score IS NOT NULL
                ORDER BY m.commence_time DESC
                LIMIT :n
                """
            ),
            {"a": f"%{player_a}%", "b": f"%{player_b}%", "n": n_matches},
        )
    ).fetchall()

    a_wins = 0
    b_wins = 0
    total = 0

    for r in rows:
        total += 1
        home = str(r.home_name)
        sets_home = int(r.home_score or 0)
        sets_away = int(r.away_score or 0)

        home_won = sets_home > sets_away
        a_is_home = player_a.lower() in home.lower()

        if home_won:
            if a_is_home:
                a_wins += 1
            else:
                b_wins += 1
        else:
            if a_is_home:
                b_wins += 1
            else:
                a_wins += 1

    return {
        "games": total,
        "a_wins": a_wins,
        "b_wins": b_wins,
        "recent": [
            {
                "date": r.commence_time.isoformat() if r.commence_time else None,
                "home": str(r.home_name),
                "away": str(r.away_name),
                "score": f"{int(r.home_score)}-{int(r.away_score)}",
            }
            for r in rows
        ],
    }

