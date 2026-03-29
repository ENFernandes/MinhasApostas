"""Main FastAPI application entry point."""

import os
from contextlib import asynccontextmanager

import structlog
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.middleware.gzip import GZipMiddleware
from fastapi.responses import JSONResponse
from slowapi import Limiter
from slowapi.util import get_remote_address

from app.consumers.match_consumer import MatchConsumer, handle_football_match, handle_tennis_match
from app.consumers.odds_consumer import OddsConsumer, handle_odds_update
from app.routers import analysis, health

# Configure structlog
structlog.configure(
    processors=[
        structlog.processors.TimeStamper(fmt="iso"),
        structlog.processors.JSONRenderer(),
    ],
    wrapper_class=structlog.make_filtering_bound_logger(20),
    context_class=dict,
    logger_factory=structlog.PrintLoggerFactory(),
)

logger = structlog.get_logger()

# Rate limiter
limiter = Limiter(key_func=get_remote_address)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager.

    Handles startup and shutdown events.
    """
    # Startup
    logger.info("Starting up analysis engine")

    # Initialize database tables
    try:
        from app.db.database import engine
        from app.db.models import Base
        async with engine.begin() as conn:
            await conn.run_sync(Base.metadata.create_all)
        logger.info("Database tables initialized")
    except Exception as e:
        logger.error("Failed to initialize database", error=str(e))

    rabbitmq_url = os.getenv(
        "RABBITMQ_URL",
        "amqp://guest:guest@localhost:5672/"
    )

    # Start consumers
    app.state.match_consumer = MatchConsumer(rabbitmq_url)
    app.state.match_consumer.register_handler("football.match.new", handle_football_match)
    app.state.match_consumer.register_handler("tennis.match.new", handle_tennis_match)
    await app.state.match_consumer.connect()

    app.state.odds_consumer = OddsConsumer(rabbitmq_url)
    app.state.odds_consumer.register_handler(handle_odds_update)
    await app.state.odds_consumer.connect()

    logger.info("All consumers started")

    yield

    # Shutdown
    logger.info("Shutting down analysis engine")

    if hasattr(app.state, "match_consumer"):
        await app.state.match_consumer.disconnect()
    if hasattr(app.state, "odds_consumer"):
        await app.state.odds_consumer.disconnect()

    logger.info("All consumers stopped")


# Create FastAPI app
app = FastAPI(
    title="Sports Betting Analysis Engine",
    description="AI-powered analysis engine for sports betting recommendations",
    version="0.1.0",
    lifespan=lifespan,
)

# Add middleware
app.state.limiter = limiter
app.add_middleware(GZipMiddleware, minimum_size=1000)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# Exception handlers
@app.exception_handler(Exception)
async def global_exception_handler(request, exc):
    """Handle all unhandled exceptions."""
    logger.error("Unhandled exception", error=str(exc))
    return JSONResponse(
        status_code=500,
        content={"error": "Internal server error", "detail": str(exc)},
    )


# Include routers
app.include_router(health.router)
app.include_router(analysis.router)


@app.get("/")
async def root():
    """Root endpoint."""
    return {
        "service": "Sports Betting Analysis Engine",
        "version": "0.1.0",
        "status": "running",
    }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "app.main:app",
        host="0.0.0.0",
        port=int(os.getenv("PORT", "8000")),
        reload=os.getenv("ENVIRONMENT") == "development",
        workers=1,
    )
