"""Analysis router for on-demand match analysis."""

from datetime import datetime
from uuid import UUID

from fastapi import APIRouter, HTTPException, status
from structlog import get_logger

from app.db.database import get_db_session
from app.db.repositories import MatchRepository, RecommendationRepository
from app.models.schemas import AnalysisRequest, AnalysisResponse, ModelProbabilities, RecommendationCreate, RecommendedMarket
from app.services.analysis_service import AnalysisService

logger = get_logger()
router = APIRouter(prefix="/analysis", tags=["analysis"])

# Initialize analysis service
analysis_service = AnalysisService()


@router.post("/match/{match_id}", response_model=AnalysisResponse)
async def analyze_match_endpoint(match_id: str, request: AnalysisRequest | None = None):
    """Perform on-demand analysis of a match.

    Args:
        match_id: Unique match identifier
        request: Optional analysis request with additional context

    Returns:
        Complete analysis with recommendations
    """
    logger.info("Analyzing match", match_id=match_id)

    try:
        # Use the analysis service
        result = await analysis_service.analyze_match(match_id)
        
        if not result:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Match not found or analysis failed",
            )

        return result

    except HTTPException:
        raise
    except Exception as e:
        logger.error("Analysis failed", match_id=match_id, error=str(e))
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Analysis failed: {str(e)}",
        )


@router.get("/match/{match_id}/latest", response_model=AnalysisResponse)
async def get_latest_analysis(match_id: str):
    """Get the latest analysis for a match.

    Args:
        match_id: Unique match identifier

    Returns:
        Latest analysis or 404
    """
    try:
        async with get_db_session() as db:
            # Get match
            match_repo = MatchRepository(db)
            match = await match_repo.get_by_id(UUID(match_id))
            
            if not match:
                raise HTTPException(
                    status_code=status.HTTP_404_NOT_FOUND,
                    detail="Match not found",
                )

            rec_repo = RecommendationRepository(db)
            rec = await rec_repo.get_latest_by_match_id(UUID(match_id))

            if not rec:
                raise HTTPException(
                    status_code=status.HTTP_404_NOT_FOUND,
                    detail="No analysis found for this match",
                )

            return AnalysisResponse(
                match_id=str(match.id),
                sport=match.sport,
                home_team=match.home_team.name if match.home_team else "Unknown",
                away_team=match.away_team.name if match.away_team else "Unknown",
                commence_time=match.commence_time,
                is_live=match.status in ("LIVE", "IN_PLAY", "PAUSED") if match.status else False,
                model_probabilities=ModelProbabilities(
                    home=float(rec.model_probability or 0),
                    draw=0.0,
                    away=round(1.0 - float(rec.model_probability or 0) - float(rec.implied_probability or 0), 3),
                    over_2_5=0.0,
                    btts=0.0,
                ),
                recommended_market=RecommendedMarket(
                    market=rec.market,
                    outcome=rec.outcome,
                    bookmaker=rec.bookmaker or "N/A",
                    odd=float(rec.odd_decimal),
                    implied_probability=float(rec.implied_probability or 0),
                    model_probability=float(rec.model_probability or 0),
                    value=float(rec.value or 0),
                    kelly_fraction=float(rec.kelly_fraction or 0),
                    stake_euros=float(rec.stake_euros or 0),
                    confidence=rec.confidence or 1,
                ),
                alternative_markets=[],
                reasoning=rec.reasoning or "",
                context_flags={},
                generated_at=rec.created_at or datetime.utcnow(),
                llm_provider="cached",
                llm_model="cached",
            )

    except HTTPException:
        raise
    except Exception as e:
        logger.error("Failed to get analysis", match_id=match_id, error=str(e))
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Failed to get analysis: {str(e)}",
        )


@router.get("/recommendations")
async def get_recommendations(status: str = "PENDING", limit: int = 50):
    """Get all recommendations with optional filtering.

    Args:
        status: Filter by status (PENDING, ACCEPTED, REJECTED, WON, LOST)
        limit: Maximum number of results

    Returns:
        List of recommendations
    """
    try:
        async with get_db_session() as db:
            repo = RecommendationRepository(db)
            recommendations = await repo.get_active(limit=limit)
            
            return [
                {
                    "id": str(r.id),
                    "match_id": str(r.match_id),
                    "market": r.market,
                    "outcome": r.outcome,
                    "bookmaker": r.bookmaker,
                    "odd": r.odd_decimal,
                    "value": r.value,
                    "confidence": r.confidence,
                    "stake": r.stake_euros,
                    "reasoning": r.reasoning,
                    "created_at": r.created_at.isoformat() if r.created_at else None,
                }
                for r in recommendations
            ]

    except Exception as e:
        logger.error("Failed to get recommendations", error=str(e))
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Failed to get recommendations: {str(e)}",
        )
