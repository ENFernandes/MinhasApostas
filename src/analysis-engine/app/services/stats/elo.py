"""ELO rating system for tennis players."""

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

INITIAL_ELO = 1500.0
K_FACTOR_NORMAL = 32.0
K_FACTOR_GRAND_SLAM = 16.0


async def get_player_elo(
    db: AsyncSession,
    player_name: str,
    surface: str,
) -> float:
    """Busca o ELO actual do jogador na superfície via view latest_player_elo.

    Fallback: 1500.0 (ELO inicial) se jogador não encontrado.
    """
    result = await db.execute(
        text("""
            SELECT elo FROM latest_player_elo
            WHERE player_name ILIKE :name
              AND surface = :surface
            LIMIT 1
        """),
        {"name": f"%{player_name}%", "surface": surface},
    )
    row = result.fetchone()
    return float(row.elo) if row else INITIAL_ELO


def probabilidade_vitoria_elo(elo_a: float, elo_b: float) -> float:
    """Probabilidade do jogador A vencer o jogador B com base em ELO."""
    return 1.0 / (1.0 + 10.0 ** ((elo_b - elo_a) / 400.0))


def calculate_elo_probability(elo_a: float, elo_b: float) -> float:
    """Calculate the probability of player A beating player B.

    Args:
        elo_a: ELO rating of player A
        elo_b: ELO rating of player B

    Returns:
        Probability of player A winning (0.0 to 1.0)
    """
    return 1.0 / (1.0 + 10.0 ** ((elo_b - elo_a) / 400.0))


def update_elo(
    elo_winner: float,
    elo_loser: float,
    is_grand_slam: bool = False,
) -> tuple[float, float]:
    """Update ELO ratings after a match.

    Args:
        elo_winner: Current ELO of the winner
        elo_loser: Current ELO of the loser
        is_grand_slam: Whether this is a Grand Slam tournament

    Returns:
        Tuple of (new_winner_elo, new_loser_elo)
    """
    k = K_FACTOR_GRAND_SLAM if is_grand_slam else K_FACTOR_NORMAL

    expected_winner = calculate_elo_probability(elo_winner, elo_loser)
    expected_loser = 1.0 - expected_winner

    new_winner = elo_winner + k * (1.0 - expected_winner)
    new_loser = elo_loser + k * (0.0 - expected_loser)

    return round(new_winner, 1), round(new_loser, 1)


def get_surface_elo(
    player: dict,
    surface: str,
) -> float:
    """Get the ELO rating for a specific surface.

    Args:
        player: Player data dictionary
        surface: Surface type (clay, grass, hard, indoor)

    Returns:
        ELO rating for the surface
    """
    surface_elos = {
        "clay": player.get("elo_clay"),
        "grass": player.get("elo_grass"),
        "hard": player.get("elo_hard"),
        "indoor": player.get("elo_indoor"),
    }

    return surface_elos.get(surface.lower(), player.get("elo_overall", INITIAL_ELO)) or INITIAL_ELO


def update_surface_elo(
    player: dict,
    surface: str,
    won: bool,
    opponent_elo: float,
    is_grand_slam: bool = False,
) -> float:
    """Update ELO for a specific surface.

    Args:
        player: Player data dictionary
        surface: Surface type
        won: Whether the player won
        opponent_elo: Opponent's ELO on this surface
        is_grand_slam: Whether this is a Grand Slam

    Returns:
        New ELO rating for the surface
    """
    current_elo = get_surface_elo(player, surface)

    if won:
        new_elo, _ = update_elo(current_elo, opponent_elo, is_grand_slam)
    else:
        _, new_elo = update_elo(opponent_elo, current_elo, is_grand_slam)

    return new_elo


def calculate_set_probability(
    elo_a: float,
    elo_b: float,
    best_of: int = 3,
) -> dict[str, float]:
    """Calculate probability of winning in different set formats.

    Args:
        elo_a: ELO rating of player A
        elo_b: ELO rating of player B
        best_of: Number of sets (3 or 5)

    Returns:
        Dictionary with probabilities for 2-0, 2-1, 3-0, 3-1, 3-2, etc.
    """
    p = calculate_elo_probability(elo_a, elo_b)

    if best_of == 3:
        # Best of 3 sets
        p_2_0 = p * p  # noqa: F841
        p_2_1 = 2 * p * p * (1 - p)  # noqa: F841
        p_1_2 = 2 * (1 - p) * (1 - p) * p  # noqa: F841
        p_0_2 = (1 - p) * (1 - p)  # noqa: F841

        return {
            "2-0": round(p * p, 4),
            "2-1": round(2 * p * p * (1 - p), 4),
            "1-2": round(2 * (1 - p) * (1 - p) * p, 4),
            "0-2": round((1 - p) * (1 - p), 4),
        }
    else:
        # Best of 5 sets (Grand Slams)
        # This is a simplified calculation
        return {
            "3-0": round(p**3, 4),
            "3-1": round(3 * p**3 * (1 - p), 4),
            "3-2": round(6 * p**3 * (1 - p) ** 2, 4),
            "2-3": round(6 * p**2 * (1 - p) ** 3, 4),
            "1-3": round(3 * p * (1 - p) ** 3, 4),
            "0-3": round((1 - p) ** 3, 4),
        }
