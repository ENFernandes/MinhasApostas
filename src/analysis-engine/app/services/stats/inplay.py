"""In-play probability adjustments based on match state."""

from typing import TypedDict


class MatchState(TypedDict):
    """Current state of an in-play match."""

    minute: int
    home_score: int
    away_score: int
    home_xg: float
    away_xg: float
    home_red_cards: int
    away_red_cards: int
    home_momentum: float  # -1.0 to 1.0
    away_momentum: float


class AdjustedProbabilities(TypedDict):
    """Adjusted probabilities after considering match state."""

    home: float
    draw: float
    away: float
    over_2_5: float
    btts: float
    confidence: float  # How reliable the adjustment is


def adjust_probabilities_inplay(
    base_probs: dict[str, float],
    state: MatchState,
) -> AdjustedProbabilities:
    """Adjust pre-match probabilities based on in-play events.

    Args:
        base_probs: Pre-match probabilities from model
        state: Current match state

    Returns:
        Adjusted probabilities
    """
    minute = state["minute"]
    home_score = state["home_score"]
    away_score = state["away_score"]
    score_diff = home_score - away_score

    # Time decay factor - as match progresses, current score matters more
    time_factor = minute / 90.0

    # Adjust for current score
    if score_diff > 0:
        # Home team leading - increase their win probability
        score_adjustment = 0.3 * time_factor * min(score_diff, 3)
        home_adj = score_adjustment
        draw_adj = -0.1 * time_factor
        away_adj = -(score_adjustment + 0.1 * time_factor)
    elif score_diff < 0:
        # Away team leading
        score_adjustment = 0.3 * time_factor * min(abs(score_diff), 3)
        home_adj = -(score_adjustment + 0.1 * time_factor)
        draw_adj = -0.1 * time_factor
        away_adj = score_adjustment
    else:
        # Draw - increase draw probability slightly
        home_adj = -0.05 * time_factor
        draw_adj = 0.1 * time_factor
        away_adj = -0.05 * time_factor

    # Adjust for red cards
    red_card_factor = (state["away_red_cards"] - state["home_red_cards"]) * 0.15
    home_adj += red_card_factor * time_factor
    away_adj -= red_card_factor * time_factor

    # Adjust for momentum (xG difference)
    xg_diff = state["home_xg"] - state["away_xg"]
    momentum_adj = xg_diff * 0.1 * time_factor
    home_adj += momentum_adj
    away_adj -= momentum_adj

    # Apply adjustments
    adjusted = {
        "home": clamp(base_probs.get("home", 0.33) + home_adj),
        "draw": clamp(base_probs.get("draw", 0.33) + draw_adj),
        "away": clamp(base_probs.get("away", 0.33) + away_adj),
    }

    # Normalize to ensure sum = 1.0
    total = sum(adjusted.values())
    adjusted = {k: v / total for k, v in adjusted.items()}

    # Adjust derived markets
    total_goals = home_score + away_score
    remaining_time = (90 - minute) / 90.0

    # Over 2.5 - if already hit, probability = 1.0
    if total_goals > 2:
        adjusted["over_2_5"] = 1.0
    else:
        needed = 3 - total_goals
        # Probability based on time remaining and xG
        goal_rate = (state["home_xg"] + state["away_xg"]) / max(minute, 1)
        expected_goals_remaining = goal_rate * (90 - minute)
        adjusted["over_2_5"] = min(1.0, expected_goals_remaining / needed)

    # BTTS - if already hit, probability = 1.0
    if home_score > 0 and away_score > 0:
        adjusted["btts"] = 1.0
    else:
        # Probability based on xG and remaining time
        if home_score == 0:
            home_prob = min(1.0, state["home_xg"] * remaining_time)
        else:
            home_prob = 1.0

        if away_score == 0:
            away_prob = min(1.0, state["away_xg"] * remaining_time)
        else:
            away_prob = 1.0

        adjusted["btts"] = home_prob * away_prob

    # Confidence decreases as we move away from base probabilities
    confidence = max(0.5, 1.0 - time_factor * 0.5)

    return {
        "home": round(adjusted["home"], 4),
        "draw": round(adjusted["draw"], 4),
        "away": round(adjusted["away"], 4),
        "over_2_5": round(adjusted["over_2_5"], 4),
        "btts": round(adjusted["btts"], 4),
        "confidence": round(confidence, 4),
    }


def clamp(value: float, min_val: float = 0.01, max_val: float = 0.99) -> float:
    """Clamp a value between min and max."""
    return max(min_val, min(max_val, value))


def should_recommend_inplay(
    adjusted_probs: AdjustedProbabilities,
    current_odds: dict[str, float],
    minute: int,
) -> dict:
    """Determine if an in-play bet should be recommended.

    Args:
        adjusted_probs: Adjusted in-play probabilities
        current_odds: Current odds offered by bookmakers
        minute: Current minute of the match

    Returns:
        Dictionary with recommendation details
    """
    # Rule RN-05: Only recommend after minute 10 and before minute 80
    if minute < 10 or minute > 80:
        return {"should_bet": False, "reason": "Outside recommended time window"}

    best_value = 0.0
    best_market = None
    best_outcome = None

    # Check each market for value
    for outcome in ["home", "draw", "away"]:
        if outcome not in current_odds:
            continue

        prob = adjusted_probs[outcome]
        odd = current_odds[outcome]
        value = (prob * odd) - 1

        if value > best_value and value >= 0.05:  # Min 5% value
            best_value = value
            best_market = "h2h"
            best_outcome = outcome

    # Check over/under markets
    if "over_2_5" in current_odds:
        prob = adjusted_probs["over_2_5"]
        odd = current_odds["over_2_5"]
        value = (prob * odd) - 1

        if value > best_value and value >= 0.05:
            best_value = value
            best_market = "totals"
            best_outcome = "over_2_5"

    if best_market is None:
        return {"should_bet": False, "reason": "No value found"}

    return {
        "should_bet": True,
        "market": best_market,
        "outcome": best_outcome,
        "value": round(best_value, 4),
        "confidence": adjusted_probs["confidence"],
        "minute": minute,
    }
