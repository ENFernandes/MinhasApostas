"""Odds calculation and probability conversion utilities."""

IMPLIED_PROB_MIN_ODD = 1.01
IMPLIED_PROB_MAX_ODD = 100.0


def calculate_implied_probability(odd: float) -> float:
    """Convert decimal odd to implied probability.

    Args:
        odd: Decimal odd (e.g., 2.50)

    Returns:
        Implied probability as decimal (e.g., 0.40)

    Raises:
        ValueError: If odd is invalid
    """
    if odd < IMPLIED_PROB_MIN_ODD or odd > IMPLIED_PROB_MAX_ODD:
        raise ValueError(f"Odd must be between {IMPLIED_PROB_MIN_ODD} and {IMPLIED_PROB_MAX_ODD}")

    return round(1.0 / odd, 4)


def remove_bookmaker_margin(
    odds: dict[str, float],
    method: str = "proportional",
) -> dict[str, float]:
    """Remove bookmaker margin (vig) to get fair probabilities.

    Args:
        odds: Dictionary of outcome -> odd
        method: Method to remove margin ("proportional" or "power")

    Returns:
        Dictionary of outcome -> fair probability
    """
    implied_probs = {k: calculate_implied_probability(v) for k, v in odds.items()}
    total_prob = sum(implied_probs.values())

    if method == "proportional":
        # Distribute margin proportionally
        return {k: round(v / total_prob, 4) for k, v in implied_probs.items()}

    elif method == "power":
        # Power method - more accurate for some markets
        n = len(odds)
        fair_probs = {}

        for outcome, implied in implied_probs.items():
            # Solve for fair probability using power method
            fair = implied ** (1.0 / n) / sum(p ** (1.0 / n) for p in implied_probs.values())
            fair_probs[outcome] = round(fair, 4)

        return fair_probs

    else:
        raise ValueError(f"Unknown margin removal method: {method}")


def calculate_value(
    real_probability: float,
    odd: float,
) -> float:
    """Calculate value (EV) for a bet.

    Formula: value = (prob_real * odd) - 1

    Args:
        real_probability: Calculated real probability (0-1)
        odd: Decimal odd offered by bookmaker

    Returns:
        Value as decimal (positive = value bet)
    """
    return round((real_probability * odd) - 1.0, 4)


def should_bet(
    real_probability: float,
    odd: float,
    min_value: float = 0.05,
    min_odd: float = 1.50,
    max_odd: float = 8.00,
) -> bool:
    """Determine if a bet has value and meets criteria.

    Args:
        real_probability: Calculated real probability
        odd: Decimal odd offered
        min_value: Minimum value threshold (default 5%)
        min_odd: Minimum odd (default 1.50)
        max_odd: Maximum odd (default 8.00)

    Returns:
        True if bet should be recommended
    """
    # Check odd is within acceptable range
    if odd < min_odd or odd > max_odd:
        return False

    # Calculate value
    value = calculate_value(real_probability, odd)

    # Check value meets threshold
    return value >= min_value


def calculate_expected_return(
    probabilities: dict[str, float],
    odds: dict[str, float],
) -> dict[str, float]:
    """Calculate expected return for multiple outcomes.

    Args:
        probabilities: Dictionary of outcome -> real probability
        odds: Dictionary of outcome -> odd

    Returns:
        Dictionary of outcome -> expected return
    """
    results = {}

    for outcome in probabilities:
        if outcome in odds:
            prob = probabilities[outcome]
            odd = odds[outcome]
            results[outcome] = {
                "probability": prob,
                "odd": odd,
                "expected_value": calculate_value(prob, odd),
                "should_bet": should_bet(prob, odd),
            }

    return results


def find_best_odd(
    outcome: str,
    odds_by_bookmaker: dict[str, dict[str, float]],
) -> dict:
    """Find the best odd for a specific outcome across bookmakers.

    Args:
        outcome: The outcome to find (e.g., "home", "over_2_5")
        odds_by_bookmaker: Dictionary of bookmaker -> {outcome -> odd}

    Returns:
        Dictionary with best bookmaker and odd
    """
    best_odd = 0.0
    best_bookmaker = None

    for bookmaker, odds in odds_by_bookmaker.items():
        if outcome in odds and odds[outcome] > best_odd:
            best_odd = odds[outcome]
            best_bookmaker = bookmaker

    return {
        "outcome": outcome,
        "bookmaker": best_bookmaker,
        "odd": best_odd,
        "implied_probability": calculate_implied_probability(best_odd) if best_odd > 0 else 0,
    }


def calculate_arbitrage(
    odds_by_bookmaker: dict[str, dict[str, float]],
) -> dict:
    """Check for arbitrage opportunities across bookmakers.

    Args:
        odds_by_bookmaker: Dictionary of bookmaker -> {outcome -> odd}

    Returns:
        Dictionary with arbitrage details if found
    """
    # Get best odd for each outcome from different bookmakers
    outcomes = set()
    for odds in odds_by_bookmaker.values():
        outcomes.update(odds.keys())

    best_odds = {}
    best_bookmakers = {}

    for outcome in outcomes:
        result = find_best_odd(outcome, odds_by_bookmaker)
        if result["bookmaker"] is not None:
            best_odds[outcome] = result["odd"]
            best_bookmakers[outcome] = result["bookmaker"]

    # Calculate arbitrage percentage
    arb_pct = sum(1.0 / odd for odd in best_odds.values())

    if arb_pct < 1.0:
        # Arbitrage found!
        stakes = {k: (1.0 / v) / arb_pct for k, v in best_odds.items()}
        profit = (1.0 / arb_pct) - 1.0

        return {
            "has_arbitrage": True,
            "arbitrage_percentage": round(arb_pct, 4),
            "profit_percentage": round(profit * 100, 2),
            "best_odds": best_odds,
            "bookmakers": best_bookmakers,
            "recommended_stakes": {k: round(v * 100, 2) for k, v in stakes.items()},
        }

    return {
        "has_arbitrage": False,
        "arbitrage_percentage": round(arb_pct, 4),
    }


def get_fair_odd(
    probability: float,
    margin: float = 0.0,
) -> float:
    """Calculate fair odd from probability with optional margin.

    Args:
        probability: Real probability (0-1)
        margin: Bookmaker margin to add (0-0.1)

    Returns:
        Fair decimal odd
    """
    if probability <= 0 or probability >= 1:
        raise ValueError("Probability must be between 0 and 1")

    fair_odd = 1.0 / probability

    if margin > 0:
        fair_odd = fair_odd * (1.0 - margin)

    return round(fair_odd, 2)
