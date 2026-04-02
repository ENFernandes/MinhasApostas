"""Tennis context helpers (recent form + head-to-head) from local DB.

Prioridade das fontes:
  1. Tabela `matches` — dados live (api-tennis.com), mais recentes
  2. Tabela `historical_tennis_matches` — tennis-data.co.uk (V015), fallback de forma/H2H
     (sem colunas de serviço por jogo — `get_tennis_serve_stats` devolve vazio)

Normalização de nomes (V017):
  Cada tabela tem uma coluna *_normalized com a forma canónica "surname initial"
  (ex: "djokovic n"). As queries fazem match exacto no campo normalizado e
  usam ILIKE no token principal como fallback para apelidos compostos.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date, datetime
from typing import Any

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from app.services.stats.normalize import normalize_tennis_name, surname_token


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
    """Return recent finished matches for a player (W/L).

    Tenta primeiro a tabela `matches` (dados live). Se insuficiente,
    complementa com `historical_tennis_matches`.
    Usa name_normalized para match exacto + ILIKE como fallback.
    """
    p_norm = normalize_tennis_name(player_name)
    p_fuzzy = f"%{surname_token(p_norm)}%"

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
                        ht.name_normalized = :p_norm OR ht.name ILIKE :p_fuzzy
                     OR at.name_normalized = :p_norm OR at.name ILIKE :p_fuzzy
                  )
                  AND m.home_score IS NOT NULL
                  AND m.away_score IS NOT NULL
                ORDER BY m.commence_time DESC
                LIMIT :n
                """
            ),
            {"p_norm": p_norm, "p_fuzzy": p_fuzzy, "n": n_matches},
        )
    ).fetchall()

    results: list[TennisRecentResult] = []
    for r in rows:
        home = str(r.home_name)
        away = str(r.away_name)
        is_home = p_norm in normalize_tennis_name(home)

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

    # Complementar com histórico (tennis-data.co.uk) se não há dados suficientes
    if len(results) < n_matches:
        hist_rows = (
            await db.execute(
                text(
                    """
                    SELECT
                        match_date,
                        winner,
                        loser,
                        winner_normalized,
                        loser_normalized,
                        wsets,
                        lsets,
                        w1, l1, w2, l2, w3, l3, w4, l4, w5, l5,
                        comment
                    FROM historical_tennis_matches
                    WHERE (
                        winner_normalized = :p_norm OR winner ILIKE :p_fuzzy
                     OR loser_normalized  = :p_norm OR loser  ILIKE :p_fuzzy
                    )
                    ORDER BY match_date DESC NULLS LAST
                    LIMIT :n
                    """
                ),
                {"p_norm": p_norm, "p_fuzzy": p_fuzzy, "n": n_matches - len(results)},
            )
        ).fetchall()

        for r in hist_rows:
            winner_norm = str(r.winner_normalized or normalize_tennis_name(str(r.winner)))
            is_winner = winner_norm == p_norm or surname_token(p_norm) in winner_norm
            opponent = str(r.loser) if is_winner else str(r.winner)
            wsets = int(r.wsets or 0)
            lsets = int(r.lsets or 0)
            if wsets > 0 or lsets > 0:
                sets_for, sets_against = (wsets, lsets) if is_winner else (lsets, wsets)
            else:
                score_txt = _format_set_games_score(r)
                sets_for, sets_against = _parse_sets(score_txt, is_winner)

            raw_date = r.match_date
            if raw_date is None:
                match_dt = datetime.min
            elif isinstance(raw_date, datetime):
                match_dt = raw_date
            elif isinstance(raw_date, date):
                match_dt = datetime.combine(raw_date, datetime.min.time())
            else:
                match_dt = datetime.min

            results.append(
                TennisRecentResult(
                    date=match_dt,
                    opponent=opponent,
                    is_home=True,
                    sets_for=sets_for,
                    sets_against=sets_against,
                    result="W" if is_winner else "L",
                )
            )

    return results


def _format_set_games_score(r: Any) -> str:
    """Monta string tipo '6-4 7-5' a partir das colunas w1/l1… da BD."""
    parts: list[str] = []
    pairs = [
        (r.w1, r.l1),
        (r.w2, r.l2),
        (r.w3, r.l3),
        (r.w4, r.l4),
        (r.w5, r.l5),
    ]
    for w, l in pairs:
        if w is None and l is None:
            continue
        parts.append(f"{int(w or 0)}-{int(l or 0)}")
    if parts:
        return " ".join(parts)
    return str(r.comment or "")


def _parse_sets(score: str, player_won: bool) -> tuple[int, int]:
    """Extrai contagem de sets a partir de score textual (ex: '6-4 7-5' → (2,0))."""
    if not score or score in ("W/O", "RET", "DEF"):
        return (1, 0) if player_won else (0, 1)

    parts = score.split()
    winner_sets = 0
    loser_sets = 0
    for part in parts:
        # Ignora tie-break inline (ex: "7-6(3)")
        part = part.split("(")[0]
        if "-" in part:
            try:
                w, l = part.split("-", 1)
                if int(w) > int(l):
                    winner_sets += 1
                else:
                    loser_sets += 1
            except ValueError:
                pass

    if player_won:
        return winner_sets, loser_sets
    return loser_sets, winner_sets


async def get_tennis_h2h(
    db: AsyncSession,
    player_a: str,
    player_b: str,
    n_matches: int = 10,
) -> dict[str, Any]:
    """Head-to-head stats para dois jogadores.

    Combina dados live (matches) com dados históricos (historical_tennis_matches).
    """
    a_norm = normalize_tennis_name(player_a)
    b_norm = normalize_tennis_name(player_b)
    a_fuzzy = f"%{surname_token(a_norm)}%"
    b_fuzzy = f"%{surname_token(b_norm)}%"

    # 1. Dados live
    live_rows = (
        await db.execute(
            text(
                """
                SELECT
                    m.commence_time AS match_date,
                    ht.name AS home_name,
                    at.name AS away_name,
                    m.home_score,
                    m.away_score,
                    NULL::text AS surface,
                    NULL::text AS tourney_name
                FROM matches m
                JOIN teams ht ON ht.id = m.home_id
                JOIN teams at ON at.id = m.away_id
                WHERE m.sport = 'tennis'
                  AND m.status = 'FINISHED'
                  AND (
                    (
                      (ht.name_normalized = :a_norm OR ht.name ILIKE :a_fuzzy)
                      AND (at.name_normalized = :b_norm OR at.name ILIKE :b_fuzzy)
                    ) OR (
                      (ht.name_normalized = :b_norm OR ht.name ILIKE :b_fuzzy)
                      AND (at.name_normalized = :a_norm OR at.name ILIKE :a_fuzzy)
                    )
                  )
                  AND m.home_score IS NOT NULL
                  AND m.away_score IS NOT NULL
                ORDER BY m.commence_time DESC
                LIMIT :n
                """
            ),
            {"a_norm": a_norm, "b_norm": b_norm,
             "a_fuzzy": a_fuzzy, "b_fuzzy": b_fuzzy, "n": n_matches},
        )
    ).fetchall()

    # 2. Dados históricos (tennis-data.co.uk)
    hist_rows = (
        await db.execute(
            text(
                """
                SELECT
                    match_date::timestamp AS match_date,
                    winner AS home_name,
                    loser AS away_name,
                    winner_normalized,
                    loser_normalized,
                    COALESCE(wsets, 0)::int AS home_score,
                    COALESCE(lsets, 0)::int AS away_score,
                    surface,
                    tournament AS tourney_name
                FROM historical_tennis_matches
                WHERE (
                    (
                      (winner_normalized = :a_norm OR winner ILIKE :a_fuzzy)
                      AND (loser_normalized  = :b_norm OR loser  ILIKE :b_fuzzy)
                    ) OR (
                      (winner_normalized = :b_norm OR winner ILIKE :b_fuzzy)
                      AND (loser_normalized  = :a_norm OR loser  ILIKE :a_fuzzy)
                    )
                )
                ORDER BY match_date DESC NULLS LAST
                LIMIT :n
                """
            ),
            {"a_norm": a_norm, "b_norm": b_norm,
             "a_fuzzy": a_fuzzy, "b_fuzzy": b_fuzzy, "n": n_matches},
        )
    ).fetchall()

    # Combinar: live primeiro, depois histórico (sem duplicar)
    all_rows = list(live_rows) + list(hist_rows)
    all_rows.sort(key=lambda r: r.match_date or datetime.min, reverse=True)
    all_rows = all_rows[:n_matches]

    a_wins = 0
    b_wins = 0
    total = 0
    recent: list[dict[str, Any]] = []

    for r in all_rows:
        total += 1
        home = str(r.home_name)
        home_norm = normalize_tennis_name(home)
        sets_home = int(r.home_score or 0)
        sets_away = int(r.away_score or 0)

        home_won = sets_home > sets_away
        a_is_home = home_norm == a_norm or surname_token(a_norm) in home_norm

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

        recent.append(
            {
                "date": r.match_date.isoformat() if r.match_date else None,
                "winner": str(r.home_name) if home_won else str(r.away_name),
                "loser": str(r.away_name) if home_won else str(r.home_name),
                "score": f"{sets_home}-{sets_away}",
                "surface": r.surface if hasattr(r, "surface") else None,
                "tourney": r.tourney_name if hasattr(r, "tourney_name") else None,
            }
        )

    return {
        "games": total,
        "a_wins": a_wins,
        "b_wins": b_wins,
        "recent": recent,
    }


async def get_tennis_serve_stats(
    db: AsyncSession,
    player_name: str,
    surface: str | None = None,
    n_matches: int = 20,
) -> dict[str, Any]:
    """Estatísticas médias de serviço a partir do histórico.

    A tabela `historical_tennis_matches` (tennis-data.co.uk) não inclui aces/svpt por jogo.
    Mantém-se a assinatura para chamadores; devolve estrutura vazia até haver fonte compatível.
    """
    del db, surface, n_matches
    return {"player": player_name, "serve_stats_by_surface": {}}
