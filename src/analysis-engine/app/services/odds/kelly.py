"""Kelly Criterion calculations for bankroll management."""


DEFAULT_KELLY_FRACTION = 0.25
MAX_STAKE_PCT = 0.05
MIN_STAKE_ABSOLUTE = 1.0


def calculate_kelly_fraction(
    probability: float,
    odd: float,
    fraction: float = DEFAULT_KELLY_FRACTION,
) -> float:
    """Calculate the Kelly fraction for optimal bet sizing.

    Uses fractional Kelly to reduce variance (default 25% of full Kelly).

    Formula: f = (bp - q) / b
    Where:
        b = decimal odd - 1 (net odds received)
        p = probability of winning
        q = probability of losing (1 - p)

    Args:
        probability: Real probability of winning (0-1)
        odd: Decimal odd offered
        fraction: Fraction of Kelly to use (default 0.25)

    Returns:
        Kelly fraction (0 to MAX_STAKE_PCT)
    """
    if probability <= 0 or probability >= 1:
        return 0.0

    if odd <= 1.0:
        return 0.0

    # Calculate net profit per unit bet
    b = odd - 1.0
    q = 1.0 - probability

    # Full Kelly formula
    kelly_full = (b * probability - q) / b

    if kelly_full <= 0:
        return 0.0  # No value - don't bet

    # Apply fractional Kelly
    kelly_fractional = kelly_full * fraction

    # Cap at maximum stake percentage (5%)
    return round(min(kelly_fractional, MAX_STAKE_PCT), 4)


def calculate_stake(
    bankroll: float,
    kelly_fraction: float,
    confidence: int = 5,
    min_stake: float = MIN_STAKE_ABSOLUTE,
    max_stake_pct: float = MAX_STAKE_PCT,
) -> float:
    """Calculate the actual stake amount in euros.

    Args:
        bankroll: Total available bankroll
        kelly_fraction: Kelly fraction from calculate_kelly_fraction
        confidence: LLM confidence score (1-10)
        min_stake: Minimum stake amount (default 1.0)
        max_stake_pct: Maximum stake as percentage of bankroll

    Returns:
        Stake amount in euros
    """
    # Adjust Kelly fraction based on confidence
    # RN-06: High confidence (8-10) = 25% Kelly, Medium (6-7) = 15% Kelly
    if confidence < 6:
        return 0.0  # Don't bet on low confidence

    confidence_multiplier = 1.0 if confidence >= 8 else 0.6
    adjusted_kelly = kelly_fraction * confidence_multiplier

    # Calculate stake
    stake = bankroll * adjusted_kelly

    # Apply minimum stake (RN-10: for bankroll < 50, min 1€)
    if bankroll < 50:
        max_stake = 5.0  # Max 5€ for small bankrolls
        stake = min(stake, max_stake)
        if stake < min_stake:
            return 0.0  # Not worth the risk
    else:
        # Cap at maximum stake
        max_stake = bankroll * max_stake_pct
        stake = min(stake, max_stake)

    return round(stake, 2)


def calculate_stake_simple(
    bankroll: float,
    probability: float,
    odd: float,
    confidence: int = 5,
) -> dict:
    """Simple function to calculate recommended stake.

    Args:
        bankroll: Total available bankroll
        probability: Real probability of winning
        odd: Decimal odd offered
        confidence: LLM confidence score (1-10)

    Returns:
        Dictionary with stake details
    """
    kelly = calculate_kelly_fraction(probability, odd)
    stake = calculate_stake(bankroll, kelly, confidence)

    return {
        "kelly_fraction": kelly,
        "stake_euros": stake,
        "bankroll_percentage": round(kelly * 100, 2),
        "confidence": confidence,
        "recommendation": "BET" if stake > 0 else "NO BET",
    }


def calculate_kelly_multiple_outcomes(
    probabilities: dict[str, float],
    odds: dict[str, float],
    bankroll: float,
    confidence: int = 5,
) -> dict[str, dict]:
    """Calculate Kelly stakes for multiple outcomes.

    Args:
        probabilities: Dictionary of outcome -> probability
        odds: Dictionary of outcome -> odd
        bankroll: Total available bankroll
        confidence: LLM confidence score

    Returns:
        Dictionary of outcome -> stake details
    """
    results = {}

    for outcome in probabilities:
        if outcome in odds:
            prob = probabilities[outcome]
            odd = odds[outcome]

            kelly = calculate_kelly_fraction(prob, odd)
            stake = calculate_stake(bankroll, kelly, confidence)

            results[outcome] = {
                "probability": prob,
                "odd": odd,
                "value": round((prob * odd) - 1, 4),
                "kelly_fraction": kelly,
                "stake_euros": stake,
            }

    return results


def get_confidence_adjusted_kelly(
    base_kelly: float,
    confidence: int,
) -> float:
    """Adjust Kelly fraction based on confidence level.

    Args:
        base_kelly: Base Kelly fraction
        confidence: LLM confidence score (1-10)

    Returns:
        Adjusted Kelly fraction
    """
    # RN-06 adjustments
    if confidence >= 8:
        return base_kelly  # Use full Kelly fraction (25% of full)
    elif confidence >= 6:
        return base_kelly * 0.6  # Reduce to 15% of full Kelly
    else:
        return 0.0  # Don't bet


def validate_stake_limits(
    stake: float,
    bankroll: float,
    max_daily_bets: int = 3,
    current_daily_bets: int = 0,
) -> dict:
    """Validate that stake meets all business rules.

    Args:
        stake: Proposed stake amount
        bankroll: Total bankroll
        max_daily_bets: Maximum bets per day (RN-04)
        current_daily_bets: Number of bets already placed today

    Returns:
        Dictionary with validation results
    """
    errors = []
    warnings = []

    # RN-04: Maximum 3 bets per day
    if current_daily_bets >= max_daily_bets:
        errors.append(f"Maximum daily bets reached ({max_daily_bets})")

    # RN-03: Max 5% per bet
    if stake > bankroll * MAX_STAKE_PCT:
        errors.append(f"Stake exceeds maximum ({MAX_STAKE_PCT * 100}% of bankroll)")

    # RN-10: For bankroll < 50, min 1€ max 5€
    if bankroll < 50:
        if stake < 1.0:
            warnings.append("Stake below minimum 1€ for small bankroll")
        if stake > 5.0:
            errors.append("Stake exceeds 5€ limit for small bankroll")

    # RN-02: Minimum stake 1% of bankroll (unless bankroll < 50)
    if bankroll >= 50 and stake < bankroll * 0.01:
        warnings.append("Stake below recommended 1% minimum")

    return {
        "valid": len(errors) == 0,
        "errors": errors,
        "warnings": warnings,
        "stake": stake if len(errors) == 0 else 0.0,
    }
