"""Odds comparison and movement detection."""

from datetime import datetime
from typing import TypedDict


class OddsMovement(TypedDict):
    """Type definition for odds movement data."""

    bookmaker: str
    market: str
    outcome: str
    old_odd: float
    new_odd: float
    change_pct: float
    movement_type: str  # "shorten" (odds down), "drift" (odds up)
    timestamp: datetime
    significant: bool


class BookmakerComparison(TypedDict):
    """Type definition for bookmaker comparison."""

    outcome: str
    best_odd: float
    best_bookmaker: str
    worst_odd: float
    worst_bookmaker: str
    spread_pct: float
    value_found: bool


def compare_bookmakers(
    odds_by_bookmaker: dict[str, dict[str, float]],
    min_spread_threshold: float = 0.03,
) -> list[BookmakerComparison]:
    """Compare odds across bookmakers to find best prices.

    Args:
        odds_by_bookmaker: Dict of {bookmaker: {outcome: odd}}
        min_spread_threshold: Minimum spread to flag (default 3%)

    Returns:
        List of comparison results per outcome
    """
    if not odds_by_bookmaker:
        return []

    # Get all unique outcomes
    outcomes = set()
    for odds in odds_by_bookmaker.values():
        outcomes.update(odds.keys())

    comparisons = []

    for outcome in outcomes:
        bookmaker_odds = {}

        for bookmaker, odds in odds_by_bookmaker.items():
            if outcome in odds:
                bookmaker_odds[bookmaker] = odds[outcome]

        if not bookmaker_odds:
            continue

        # Find best and worst
        best_bookmaker = max(bookmaker_odds.items(), key=lambda x: x[1])[0]
        worst_bookmaker = min(bookmaker_odds.items(), key=lambda x: x[1])[0]
        best_odd = bookmaker_odds[best_bookmaker]
        worst_odd = bookmaker_odds[worst_bookmaker]

        # Calculate spread
        spread_pct = (best_odd - worst_odd) / worst_odd if worst_odd > 0 else 0

        # RN-07: If spread < 3%, market is efficient - increase value threshold
        is_efficient = spread_pct < min_spread_threshold

        comparisons.append({
            "outcome": outcome,
            "best_odd": best_odd,
            "best_bookmaker": best_bookmaker,
            "worst_odd": worst_odd,
            "worst_bookmaker": worst_bookmaker,
            "spread_pct": round(spread_pct, 4),
            "value_found": not is_efficient,
        })

    return comparisons


def detect_odds_movement(
    historical_odds: list[dict],
    threshold_pct: float = 0.05,
) -> list[OddsMovement]:
    """Detect significant odds movements (sharp money indicators).

    Args:
        historical_odds: List of {timestamp, bookmaker, outcome, odd}
        threshold_pct: Minimum percentage change to flag (5%)

    Returns:
        List of significant movements
    """
    if len(historical_odds) < 2:
        return []

    movements = []

    # Sort by timestamp
    sorted_odds = sorted(historical_odds, key=lambda x: x.get("timestamp", datetime.min))

    # Group by bookmaker and outcome
    grouped = {}
    for odd_data in sorted_odds:
        key = (odd_data.get("bookmaker"), odd_data.get("outcome"))
        if key not in grouped:
            grouped[key] = []
        grouped[key].append(odd_data)

    # Detect movements
    for (bookmaker, outcome), odds_list in grouped.items():
        if len(odds_list) < 2:
            continue

        old_data = odds_list[0]
        new_data = odds_list[-1]

        old_odd = old_data.get("odd", 0)
        new_odd = new_data.get("odd", 0)

        if old_odd <= 0:
            continue

        change_pct = abs(new_odd - old_odd) / old_odd

        if change_pct >= threshold_pct:
            movement_type = "shorten" if new_odd < old_odd else "drift"

            movements.append({
                "bookmaker": bookmaker,
                "market": old_data.get("market", "h2h"),
                "outcome": outcome,
                "old_odd": old_odd,
                "new_odd": new_odd,
                "change_pct": round(change_pct, 4),
                "movement_type": movement_type,
                "timestamp": new_data.get("timestamp", datetime.now()),
                "significant": True,
            })

    return movements


def detect_sharp_money(
    odds_history: list[dict],
    bookmaker_weights: dict[str, float] | None = None,
) -> dict:
    """Detect sharp money by analyzing odds movements across bookmakers.

    Args:
        odds_history: Historical odds data
        bookmaker_weights: Weights for different bookmakers (sharp vs square)

    Returns:
        Analysis of sharp money direction
    """
    if bookmaker_weights is None:
        # Default weights - Pinnacle and sharp books weighted higher
        bookmaker_weights = {
            "pinnacle": 1.0,
            "betfair": 0.9,
            "unibet": 0.7,
            "bet365": 0.7,
            "williamhill": 0.6,
            "bwin": 0.6,
        }

    movements = detect_odds_movement(odds_history)

    if not movements:
        return {"detected": False, "reason": "No significant movements"}

    # Weight movements by bookmaker
    weighted_movements = []
    for movement in movements:
        bookmaker = movement["bookmaker"].lower()
        weight = bookmaker_weights.get(bookmaker, 0.5)

        weighted_movements.append({
            **movement,
            "weight": weight,
            "weighted_change": movement["change_pct"] * weight,
        })

    # Aggregate by outcome
    outcome_changes = {}
    for movement in weighted_movements:
        outcome = movement["outcome"]
        if outcome not in outcome_changes:
            outcome_changes[outcome] = {"total_weighted": 0, "count": 0}

        direction = 1 if movement["movement_type"] == "shorten" else -1
        outcome_changes[outcome]["total_weighted"] += (
            movement["weighted_change"] * direction
        )
        outcome_changes[outcome]["count"] += 1

    # Find strongest signal
    best_outcome = max(
        outcome_changes,
        key=lambda x: outcome_changes[x]["total_weighted"],
    )

    return {
        "detected": True,
        "sharp_outcome": best_outcome,
        "confidence": min(
            1.0,
            abs(outcome_changes[best_outcome]["total_weighted"]),
        ),
        "movements": weighted_movements,
    }


def get_best_price(
    outcome: str,
    odds_data: dict[str, dict],
    min_bookmakers: int = 3,
) -> dict:
    """Get the best available price for an outcome.

    Args:
        outcome: The outcome to find
        odds_data: Dictionary of bookmaker -> odds
        min_bookmakers: Minimum number of bookmakers for reliable price

    Returns:
        Best price details
    """
    prices = []

    for bookmaker, data in odds_data.items():
        if outcome in data:
            prices.append({
                "bookmaker": bookmaker,
                "odd": data[outcome],
            })

    if len(prices) < min_bookmakers:
        return {
            "found": False,
            "reason": f"Only {len(prices)} bookmakers available",
        }

    best = max(prices, key=lambda x: x["odd"])

    return {
        "found": True,
        "outcome": outcome,
        "best_odd": best["odd"],
        "bookmaker": best["bookmaker"],
        "all_prices": prices,
    }
