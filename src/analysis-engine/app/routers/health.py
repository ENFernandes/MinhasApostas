"""Health check router."""

from datetime import datetime

from fastapi import APIRouter
from structlog import get_logger

from app.models.schemas import HealthStatus
from app.services.ai.factory import ProviderFactory

logger = get_logger()
router = APIRouter(tags=["health"])


@router.get("/health", response_model=HealthStatus)
async def health_check():
    """Check health of all services.

    Returns:
        Health status of the service and dependencies
    """
    services = {
        "api": True,
        "database": await _check_database(),
        "rabbitmq": await _check_rabbitmq(),
        "llm": await _check_llm(),
    }

    all_healthy = all(services.values())

    return HealthStatus(
        status="healthy" if all_healthy else "degraded",
        timestamp=datetime.utcnow(),
        version="0.1.0",
        services=services,
    )


async def _check_database() -> bool:
    """Check database connectivity."""
    try:
        # Placeholder - would check actual DB connection
        return True
    except Exception:
        return False


async def _check_rabbitmq() -> bool:
    """Check RabbitMQ connectivity."""
    try:
        # Placeholder - would check actual RabbitMQ connection
        return True
    except Exception:
        return False


async def _check_llm() -> bool:
    """Check LLM provider health."""
    try:
        import os

        provider_name = os.getenv("LLM_PROVIDER", "ollama")
        provider = ProviderFactory.create_provider(provider_name)
        return await provider.health_check()
    except Exception:
        return False
