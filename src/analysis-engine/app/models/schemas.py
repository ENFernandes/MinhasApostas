"""Pydantic models for the analysis engine."""

from datetime import datetime
from typing import Any

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel


class MatchData(BaseModel):
    """Input data for a match."""

    id: str
    sport: str = Field(..., pattern="^(football|tennis)$")
    home_team: str
    away_team: str
    commence_time: datetime
    is_live: bool = False
    minute: int | None = None
    home_score: int | None = None
    away_score: int | None = None
    competition: str | None = None


class ModelProbabilities(BaseModel):
    """Probabilities from statistical models."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    home: float = Field(..., ge=0, le=1)
    draw: float = Field(..., ge=0, le=1)
    away: float = Field(..., ge=0, le=1)
    over_2_5: float = Field(..., ge=0, le=1)
    btts: float = Field(..., ge=0, le=1)
    # poisson_historical | xgboost | implied_probability | placeholder_1x2 | elo_* (ténis)
    data_source: str = "implied_probability"


class OddsData(BaseModel):
    """Odds data from a bookmaker."""

    bookmaker: str
    market: str
    outcome: str
    odd: float = Field(..., ge=1.01)


class AnalysisRequest(BaseModel):
    """Request for match analysis."""

    match: MatchData
    odds: list[OddsData]
    context: dict[str, Any] | None = None


class RecommendedMarket(BaseModel):
    """Recommended betting market."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    market: str
    outcome: str
    bookmaker: str
    odd: float
    implied_probability: float
    model_probability: float
    value: float
    kelly_fraction: float
    stake_euros: float
    confidence: int = Field(..., ge=1, le=10)


class AlternativeMarket(BaseModel):
    """Alternative betting market."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    market: str
    outcome: str
    odd: float
    value: float
    confidence: int = Field(..., ge=1, le=10)


class AnalysisResponse(BaseModel):
    """Response from match analysis."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    match_id: str
    sport: str
    home_team: str
    away_team: str
    commence_time: datetime
    is_live: bool
    model_probabilities: ModelProbabilities
    recommended_market: RecommendedMarket | None
    alternative_markets: list[AlternativeMarket]
    reasoning: str
    context_flags: dict[str, Any]
    generated_at: datetime
    llm_provider: str
    llm_model: str


class HealthStatus(BaseModel):
    """Health check response."""

    status: str
    timestamp: datetime
    version: str
    services: dict[str, bool]


class ErrorResponse(BaseModel):
    """Error response."""

    error: str
    detail: str | None = None
    timestamp: datetime = Field(default_factory=datetime.utcnow)


# Database schemas
class MatchCreate(BaseModel):
    """Schema for creating a match."""
    external_id: str
    sport: str
    home_team: str
    away_team: str
    competition: str | None = None
    commence_time: datetime
    status: str = "SCHEDULED"


class OddsCreate(BaseModel):
    """Schema for creating odds."""
    match_id: Any  # UUID
    bookmaker: str
    market: str
    outcome: str
    odd_decimal: float
    implied_probability: float | None = None


class RecommendationCreate(BaseModel):
    """Schema for creating a recommendation."""
    match_id: Any  # UUID
    market: str
    outcome: str
    bookmaker: str | None = None
    odd_decimal: float
    model_probability: float
    implied_probability: float | None = None
    value: float | None = None
    kelly_fraction: float | None = None
    stake_euros: float | None = None
    confidence: int | None = None
    reasoning: str | None = None
    status: str = "PENDING"


class BetCreate(BaseModel):
    """Schema for creating a bet."""
    recommendation_id: Any | None = None  # UUID
    match_id: Any  # UUID
    market: str
    outcome: str
    bookmaker: str | None = None
    odd_placed: float
    stake_euros: float
