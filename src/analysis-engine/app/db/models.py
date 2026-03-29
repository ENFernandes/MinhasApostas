"""SQLAlchemy models for the analysis engine."""

from datetime import datetime
from uuid import uuid4

from sqlalchemy import Column, DateTime, Float, ForeignKey, Integer, String, Text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import declarative_base, relationship

Base = declarative_base()


class Team(Base):
    """Team model — mirrors C# teams table (read-only)."""
    __tablename__ = "teams"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid4)
    external_id = Column(String(100), nullable=False)
    sport = Column(String(50), nullable=False)
    name = Column(String(200), nullable=False)
    short_name = Column(String(50))
    country = Column(String(100))
    competition_id = Column(UUID(as_uuid=True))
    created_at = Column(DateTime(timezone=True))
    updated_at = Column(DateTime(timezone=True))


class Competition(Base):
    """Competition model — mirrors C# competitions table (read-only)."""
    __tablename__ = "competitions"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid4)
    external_id = Column(String(100), nullable=False)
    name = Column(String(200), nullable=False)
    sport = Column(String(50), nullable=False)
    country = Column(String(100))
    season = Column(String(20))
    created_at = Column(DateTime(timezone=True))


class Match(Base):
    """Match model — mirrors C# matches table (read-only)."""
    __tablename__ = "matches"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid4)
    external_id = Column(String(100), nullable=False)
    sport = Column(String(50), nullable=False)
    competition_id = Column(UUID(as_uuid=True), ForeignKey("competitions.id"))
    home_id = Column(UUID(as_uuid=True), ForeignKey("teams.id"), nullable=False)
    away_id = Column(UUID(as_uuid=True), ForeignKey("teams.id"), nullable=False)
    commence_time = Column(DateTime(timezone=True), nullable=False)
    status = Column(String(50), default="SCHEDULED")
    home_score = Column(Integer)
    away_score = Column(Integer)
    home_xg = Column(Float)
    away_xg = Column(Float)
    minute = Column(Integer)
    created_at = Column(DateTime(timezone=True))
    updated_at = Column(DateTime(timezone=True))

    # Relationships
    home_team = relationship("Team", foreign_keys=[home_id])
    away_team = relationship("Team", foreign_keys=[away_id])
    competition = relationship("Competition", foreign_keys=[competition_id])


class Odds(Base):
    """Odds model."""
    __tablename__ = "odds"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid4)
    match_id = Column(UUID(as_uuid=True), nullable=False)
    bookmaker = Column(String(100), nullable=False)
    market = Column(String(50), nullable=False)
    outcome = Column(String(50), nullable=False)
    odd_decimal = Column(Float, nullable=False)
    implied_probability = Column(Float)
    captured_at = Column(DateTime(timezone=True), default=datetime.utcnow)


class Recommendation(Base):
    """Betting recommendation model."""
    __tablename__ = "recommendations"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid4)
    match_id = Column(UUID(as_uuid=True), nullable=False)
    market = Column(String(50), nullable=False)
    outcome = Column(String(50), nullable=False)
    bookmaker = Column(String(100))
    odd_decimal = Column(Float, nullable=False)
    model_probability = Column(Float, nullable=False)
    implied_probability = Column(Float)
    value = Column(Float)
    kelly_fraction = Column(Float)
    stake_euros = Column(Float)
    confidence = Column(Integer)
    reasoning = Column(Text)
    status = Column(String(50), default="PENDING")
    created_at = Column(DateTime(timezone=True), default=datetime.utcnow)
    updated_at = Column(DateTime(timezone=True), default=datetime.utcnow, onupdate=datetime.utcnow)


class Bet(Base):
    """Placed bet tracking model."""
    __tablename__ = "bets"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid4)
    recommendation_id = Column(UUID(as_uuid=True))
    match_id = Column(UUID(as_uuid=True), nullable=False)
    market = Column(String(50), nullable=False)
    outcome = Column(String(50), nullable=False)
    bookmaker = Column(String(100))
    odd_placed = Column(Float, nullable=False)
    stake_euros = Column(Float, nullable=False)
    result = Column(String(20))  # PENDING, WIN, LOSS, VOID
    profit_loss = Column(Float)
    placed_at = Column(DateTime(timezone=True), default=datetime.utcnow)
    settled_at = Column(DateTime(timezone=True))
