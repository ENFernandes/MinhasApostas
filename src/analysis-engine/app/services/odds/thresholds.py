"""Configuration thresholds management with Redis caching."""

import os
from functools import lru_cache
from typing import Any

import redis


# Default thresholds from CONTEXT.md
DEFAULT_THRESHOLDS = {
    "MIN_VALUE_THRESHOLD": 0.05,
    "MIN_CONFIDENCE": 6,
    "KELLY_FRACTION": 0.25,
    "MAX_STAKE_PCT": 0.05,
    "MIN_ODD": 1.50,
    "MAX_ODD": 8.00,
    "MAX_DAILY_BETS": 3,
    "POISSON_WINDOW": 10,
    "ELO_K_FACTOR": 32,
    "INPLAY_START_MIN": 10,
    "INPLAY_END_MIN": 80,
    "ODDS_CACHE_TTL": 60,
}


class ThresholdManager:
    """Manages system thresholds with Redis caching."""

    def __init__(self):
        self._local_cache: dict[str, Any] = {}
        self._redis_client: redis.Redis | None = None
        self._init_redis()

    def _init_redis(self):
        """Initialize Redis connection if available."""
        redis_url = os.getenv("REDIS_URL", "redis://localhost:6379")
        try:
            self._redis_client = redis.from_url(redis_url, decode_responses=True)
            self._redis_client.ping()
        except Exception:
            self._redis_client = None

    def get(self, key: str, default: Any = None) -> Any:
        """Get threshold value with caching.

        Args:
            key: Threshold key
            default: Default value if not found

        Returns:
            Threshold value
        """
        # Check local cache first
        if key in self._local_cache:
            return self._local_cache[key]

        # Try Redis if available
        if self._redis_client:
            try:
                value = self._redis_client.get(f"threshold:{key}")
                if value is not None:
                    # Parse value (could be int, float, etc.)
                    parsed = self._parse_value(value)
                    self._local_cache[key] = parsed
                    return parsed
            except Exception:
                pass

        # Use default from CONTEXT.md
        if key in DEFAULT_THRESHOLDS:
            value = DEFAULT_THRESHOLDS[key]
            self._local_cache[key] = value
            return value

        return default

    def get_float(self, key: str, default: float = 0.0) -> float:
        """Get threshold as float."""
        value = self.get(key, default)
        try:
            return float(value)
        except (ValueError, TypeError):
            return default

    def get_int(self, key: str, default: int = 0) -> int:
        """Get threshold as int."""
        value = self.get(key, default)
        try:
            return int(value)
        except (ValueError, TypeError):
            return default

    def set(self, key: str, value: Any, ttl: int = 3600) -> None:
        """Set threshold value.

        Args:
            key: Threshold key
            value: Value to set
            ttl: Time to live in seconds
        """
        self._local_cache[key] = value

        if self._redis_client:
            try:
                self._redis_client.setex(
                    f"threshold:{key}",
                    ttl,
                    str(value),
                )
            except Exception:
                pass

    def _parse_value(self, value: str) -> Any:
        """Parse string value to appropriate type."""
        # Try int first
        try:
            return int(value)
        except ValueError:
            pass

        # Try float
        try:
            return float(value)
        except ValueError:
            pass

        # Return as string
        return value

    def reload(self) -> None:
        """Clear local cache and reload from Redis/defaults."""
        self._local_cache.clear()

    def get_all(self) -> dict[str, Any]:
        """Get all threshold values."""
        result = dict(DEFAULT_THRESHOLDS)

        # Override with Redis values if available
        if self._redis_client:
            try:
                for key in DEFAULT_THRESHOLDS:
                    value = self._redis_client.get(f"threshold:{key}")
                    if value is not None:
                        result[key] = self._parse_value(value)
            except Exception:
                pass

        return result


# Global instance
_threshold_manager: ThresholdManager | None = None


def get_threshold_manager() -> ThresholdManager:
    """Get or create threshold manager singleton."""
    global _threshold_manager
    if _threshold_manager is None:
        _threshold_manager = ThresholdManager()
    return _threshold_manager


@lru_cache(maxsize=128)
def get_threshold(key: str) -> Any:
    """Cached function to get threshold value.

    Args:
        key: Threshold key

    Returns:
        Threshold value
    """
    manager = get_threshold_manager()
    return manager.get(key)


def get_min_value_threshold() -> float:
    """Get minimum value threshold (default 5%)."""
    return get_threshold_manager().get_float("MIN_VALUE_THRESHOLD", 0.05)


def get_min_confidence() -> int:
    """Get minimum LLM confidence (default 6/10)."""
    return get_threshold_manager().get_int("MIN_CONFIDENCE", 6)


def get_kelly_fraction() -> float:
    """Get Kelly fraction (default 25%)."""
    return get_threshold_manager().get_float("KELLY_FRACTION", 0.25)


def get_max_stake_pct() -> float:
    """Get maximum stake percentage (default 5%)."""
    return get_threshold_manager().get_float("MAX_STAKE_PCT", 0.05)


def get_odd_limits() -> tuple[float, float]:
    """Get min and max odd limits."""
    manager = get_threshold_manager()
    min_odd = manager.get_float("MIN_ODD", 1.50)
    max_odd = manager.get_float("MAX_ODD", 8.00)
    return min_odd, max_odd


def get_max_daily_bets() -> int:
    """Get maximum daily bets limit."""
    return get_threshold_manager().get_int("MAX_DAILY_BETS", 3)


def get_poisson_window() -> int:
    """Get window size for Poisson calculations."""
    return get_threshold_manager().get_int("POISSON_WINDOW", 10)


def get_elo_k_factor(is_grand_slam: bool = False) -> float:
    """Get ELO K-factor.

    Args:
        is_grand_slam: Whether this is a Grand Slam tournament

    Returns:
        K-factor value
    """
    if is_grand_slam:
        return 16.0  # Lower K for more stable Grand Slams
    return get_threshold_manager().get_float("ELO_K_FACTOR", 32.0)


def get_inplay_limits() -> tuple[int, int]:
    """Get in-play betting time limits (start, end) minutes."""
    manager = get_threshold_manager()
    start = manager.get_int("INPLAY_START_MIN", 10)
    end = manager.get_int("INPLAY_END_MIN", 80)
    return start, end


def get_odds_cache_ttl() -> int:
    """Get odds cache TTL in seconds."""
    return get_threshold_manager().get_int("ODDS_CACHE_TTL", 60)
