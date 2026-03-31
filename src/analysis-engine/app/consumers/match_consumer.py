"""RabbitMQ consumer for match events."""

import json
import os
from typing import Callable

import aio_pika
from structlog import get_logger

from app.services.ai.factory import ProviderFactory

logger = get_logger()


class MatchConsumer:
    """Consumer for match-related RabbitMQ messages."""

    def __init__(
        self,
        rabbitmq_url: str,
        exchange_name: str = "sports.events",
    ):
        self.rabbitmq_url = rabbitmq_url
        self.exchange_name = exchange_name
        self.connection: aio_pika.RobustConnection | None = None
        self.channel: aio_pika.Channel | None = None
        self._handlers: dict[str, Callable] = {}

    async def connect(self) -> None:
        """Establish connection to RabbitMQ."""
        try:
            self.connection = await aio_pika.connect_robust(self.rabbitmq_url)
            self.channel = await self.connection.channel()

            # Declare exchange
            exchange = await self.channel.declare_exchange(
                self.exchange_name,
                aio_pika.ExchangeType.TOPIC,
                durable=True,
            )

            # Declare queue
            queue = await self.channel.declare_queue(
                "analysis.match",
                durable=True,
            )

            # Bind to routing keys
            await queue.bind(exchange, routing_key="football.match.new")
            await queue.bind(exchange, routing_key="tennis.match.new")

            # Set up consumer
            await queue.consume(self._on_message)

            logger.info("Match consumer connected", exchange=self.exchange_name)

        except Exception as e:
            logger.error("Failed to connect to RabbitMQ", error=str(e))
            raise

    async def disconnect(self) -> None:
        """Close RabbitMQ connection."""
        if self.connection:
            await self.connection.close()
            logger.info("Match consumer disconnected")

    def register_handler(self, routing_key: str, handler: Callable) -> None:
        """Register a handler for a specific routing key."""
        self._handlers[routing_key] = handler

    async def _on_message(self, message: aio_pika.IncomingMessage) -> None:
        """Process incoming message."""
        async with message.process():
            routing_key = message.routing_key or "unknown"

            try:
                body = json.loads(message.body.decode())
                logger.info(
                    "Received message",
                    routing_key=routing_key,
                    match_id=body.get("ExternalId") or body.get("external_id"),
                )

                # Find handler for this routing key
                handler = self._handlers.get(routing_key)
                if handler:
                    await handler(body)
                else:
                    logger.warning("No handler for routing key", routing_key=routing_key)

            except json.JSONDecodeError as e:
                logger.error("Failed to decode message", error=str(e))
            except Exception as e:
                logger.error("Error processing message", error=str(e))


async def handle_football_match(message: dict) -> None:
    """Handle new football match messages and trigger analysis."""
    from app.db.database import get_db_session
    from app.db.repositories import MatchRepository
    from app.services.analysis_service import AnalysisService

    external_id = message.get("ExternalId") or message.get("external_id")
    logger.info(
        "Processing football match",
        match_id=external_id,
        home=message.get("HomeTeam") or message.get("home_team"),
        away=message.get("AwayTeam") or message.get("away_team"),
    )

    try:
        # Match is already saved by C# — look it up by external_id
        async with get_db_session() as db:
            match_repo = MatchRepository(db)
            match = await match_repo.get_by_external_id(str(external_id), "football")

            if not match:
                logger.warning("Match not yet in DB, skipping analysis", external_id=external_id)
                return

            logger.info("Match found, triggering analysis", match_id=str(match.id))

        # Run analysis
        service = AnalysisService()
        await service.analyze_match(str(match.id))

    except Exception as e:
        logger.error("Error handling football match", error=str(e), exc_info=True)


async def handle_tennis_match(message: dict) -> None:
    """Handle new tennis match messages and trigger analysis."""
    from app.db.database import get_db_session
    from app.db.repositories import MatchRepository
    from app.services.analysis_service import AnalysisService

    external_id = message.get("ExternalId") or message.get("external_id")
    logger.info(
        "Processing tennis match",
        match_id=external_id,
        player1=message.get("HomeTeam") or message.get("home_team"),
        player2=message.get("AwayTeam") or message.get("away_team"),
    )

    try:
        async with get_db_session() as db:
            match_repo = MatchRepository(db)
            match = await match_repo.get_by_external_id(str(external_id), "tennis")

            if not match:
                logger.warning("Tennis match not yet in DB, skipping analysis", external_id=external_id)
                return

            logger.info("Tennis match found, triggering analysis", match_id=str(match.id))

        service = AnalysisService()
        await service.analyze_match(str(match.id))

    except Exception as e:
        logger.error("Error handling tennis match", error=str(e), exc_info=True)
