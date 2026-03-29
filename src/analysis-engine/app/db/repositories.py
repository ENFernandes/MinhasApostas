"""Database repositories."""

from uuid import UUID

from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.future import select
from sqlalchemy.orm import selectinload

from app.db.models import Bet, Match, Odds, Recommendation
from app.models.schemas import BetCreate, OddsCreate, RecommendationCreate


class MatchRepository:
    """Repository for matches — read-only, C# side writes matches."""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def get_by_id(self, match_id: UUID) -> Match | None:
        """Get match by ID with team and competition relationships loaded."""
        result = await self.session.execute(
            select(Match)
            .options(
                selectinload(Match.home_team),
                selectinload(Match.away_team),
                selectinload(Match.competition),
            )
            .where(Match.id == match_id)
        )
        return result.scalar_one_or_none()

    async def get_by_external_id(self, external_id: str, sport: str) -> Match | None:
        """Get match by external ID with team and competition relationships loaded."""
        result = await self.session.execute(
            select(Match)
            .options(
                selectinload(Match.home_team),
                selectinload(Match.away_team),
                selectinload(Match.competition),
            )
            .where(Match.external_id == external_id, Match.sport == sport)
        )
        return result.scalar_one_or_none()

    async def get_upcoming(self, limit: int = 50) -> list[Match]:
        """Get upcoming matches."""
        from datetime import datetime

        result = await self.session.execute(
            select(Match)
            .options(
                selectinload(Match.home_team),
                selectinload(Match.away_team),
                selectinload(Match.competition),
            )
            .where(Match.commence_time > datetime.utcnow())
            .order_by(Match.commence_time)
            .limit(limit)
        )
        return result.scalars().all()


class OddsRepository:
    """Repository for odds."""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def create(self, odds_data: OddsCreate) -> Odds:
        """Create odds record."""
        odds = Odds(
            match_id=odds_data.match_id,
            bookmaker=odds_data.bookmaker,
            market=odds_data.market,
            outcome=odds_data.outcome,
            odd_decimal=odds_data.odd_decimal,
            implied_probability=odds_data.implied_probability,
        )
        self.session.add(odds)
        await self.session.flush()
        return odds

    async def get_by_match(self, match_id: UUID) -> list[Odds]:
        """Get odds for a match."""
        result = await self.session.execute(
            select(Odds).where(Odds.match_id == match_id)
        )
        return result.scalars().all()


class RecommendationRepository:
    """Repository for recommendations."""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def create(self, rec_data: RecommendationCreate) -> Recommendation:
        """Create recommendation."""
        rec = Recommendation(
            match_id=rec_data.match_id,
            market=rec_data.market,
            outcome=rec_data.outcome,
            bookmaker=rec_data.bookmaker,
            odd_decimal=rec_data.odd_decimal,
            model_probability=rec_data.model_probability,
            implied_probability=rec_data.implied_probability,
            value=rec_data.value,
            kelly_fraction=rec_data.kelly_fraction,
            stake_euros=rec_data.stake_euros,
            confidence=rec_data.confidence,
            reasoning=rec_data.reasoning,
            status=rec_data.status,
        )
        self.session.add(rec)
        await self.session.flush()
        return rec

    async def get_active(self, limit: int = 50) -> list[Recommendation]:
        """Get active recommendations."""
        result = await self.session.execute(
            select(Recommendation)
            .where(Recommendation.status == "PENDING")
            .order_by(Recommendation.confidence.desc())
            .limit(limit)
        )
        return result.scalars().all()

    async def get_latest_by_match_id(self, match_id: UUID) -> Recommendation | None:
        """Get the latest recommendation for a specific match."""
        from sqlalchemy import desc
        result = await self.session.execute(
            select(Recommendation)
            .where(Recommendation.match_id == match_id)
            .order_by(desc(Recommendation.created_at))
            .limit(1)
        )
        return result.scalar_one_or_none()


class BetRepository:
    """Repository for bets."""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def create(self, bet_data: BetCreate) -> Bet:
        """Create bet record."""
        bet = Bet(
            recommendation_id=bet_data.recommendation_id,
            match_id=bet_data.match_id,
            market=bet_data.market,
            outcome=bet_data.outcome,
            bookmaker=bet_data.bookmaker,
            odd_placed=bet_data.odd_placed,
            stake_euros=bet_data.stake_euros,
            result="PENDING",
        )
        self.session.add(bet)
        await self.session.flush()
        return bet
