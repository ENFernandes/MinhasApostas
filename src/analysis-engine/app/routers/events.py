"""SSE endpoint for real-time recommendation push to frontend."""

import asyncio
import json
import os

import redis.asyncio as aioredis
from fastapi import APIRouter, Request
from sse_starlette.sse import EventSourceResponse
from structlog import get_logger

logger = get_logger()

router = APIRouter(tags=["events"])

REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379")
RECOMMENDATIONS_CHANNEL = "recommendations:new"


@router.get("/stream/recommendations")
async def stream_recommendations(request: Request) -> EventSourceResponse:
    """Endpoint SSE — o frontend subscreve aqui para receber novas recomendações em tempo real.

    Usa Redis pub/sub como canal de notificação entre o Analysis Engine e o frontend.
    Não requer polling — o cliente recebe um evento imediatamente quando uma nova
    recomendação é gerada.
    """

    async def event_generator():
        redis_client = aioredis.from_url(REDIS_URL, decode_responses=True)
        pubsub = redis_client.pubsub()

        try:
            await pubsub.subscribe(RECOMMENDATIONS_CHANNEL)
            logger.info("SSE client subscribed", channel=RECOMMENDATIONS_CHANNEL)

            # Keepalive: envia comentário vazio a cada 15s para manter a conexão
            keepalive_task = asyncio.create_task(_keepalive(request))

            async for message in pubsub.listen():
                if await request.is_disconnected():
                    logger.info("SSE client disconnected")
                    break

                if message["type"] == "message":
                    yield {"event": "recommendation", "data": message["data"]}

            keepalive_task.cancel()

        except asyncio.CancelledError:
            pass
        except Exception as e:
            logger.error("SSE stream error", error=str(e))
        finally:
            await pubsub.unsubscribe(RECOMMENDATIONS_CHANNEL)
            await redis_client.aclose()

    return EventSourceResponse(event_generator())


async def publish_recommendation(recommendation: dict) -> None:
    """Publica uma nova recomendação no canal Redis para notificar o frontend via SSE."""
    try:
        redis_client = aioredis.from_url(REDIS_URL, decode_responses=True)
        await redis_client.publish(RECOMMENDATIONS_CHANNEL, json.dumps(recommendation))
        await redis_client.aclose()
    except Exception as e:
        logger.warning("Failed to publish recommendation to SSE channel", error=str(e))


async def _keepalive(request: Request) -> None:
    """Mantém a conexão SSE ativa com pings periódicos."""
    while True:
        await asyncio.sleep(15)
        if await request.is_disconnected():
            break
