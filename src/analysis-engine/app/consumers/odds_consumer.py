"""RabbitMQ consumer for odds updates."""

import json
from typing import Callable

import aio_pika
from structlog import get_logger

logger = get_logger()


class OddsConsumer:
    """Consumer for odds-related RabbitMQ messages."""

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
                "analysis.odds",
                durable=True,
            )

            # Bind to routing key
            await queue.bind(exchange, routing_key="odds.updated")

            # Set up consumer
            await queue.consume(self._on_message)

            logger.info("Odds consumer connected", exchange=self.exchange_name)

        except Exception as e:
            logger.error("Failed to connect to RabbitMQ", error=str(e))
            raise

    async def disconnect(self) -> None:
        """Close RabbitMQ connection."""
        if self.connection:
            await self.connection.close()
            logger.info("Odds consumer disconnected")

    def register_handler(self, handler: Callable) -> None:
        """Register a handler for odds updates.

        Args:
            handler: Async function to process odds updates
        """
        self._handlers["odds.updated"] = handler

    async def _on_message(self, message: aio_pika.IncomingMessage) -> None:
        """Process incoming message.

        Args:
            message: The incoming RabbitMQ message
        """
        async with message.process():
            try:
                body = json.loads(message.body.decode())
                logger.info(
                    "Received odds update",
                    match_id=body.get("match_id"),
                    bookmaker=body.get("bookmaker"),
                )

                # Check if value has changed significantly (>2%)
                await self._check_value_change(body)

                # Find handler
                handler = self._handlers.get("odds.updated")
                if handler:
                    await handler(body)

            except json.JSONDecodeError as e:
                logger.error("Failed to decode message", error=str(e))
            except Exception as e:
                logger.error("Error processing message", error=str(e))

    async def _check_value_change(self, odds_data: dict) -> None:
        """Check if odds change is significant.

        Args:
            odds_data: The odds update data
        """
        # This would check against cached odds and trigger
        # re-analysis if value has changed significantly
        pass


async def handle_odds_update(message: dict) -> None:
    """Handle odds update — re-triggers analysis for the affected match."""
    from app.services.analysis_service import AnalysisService

    match_id = message.get("match_id")
    logger.info("Processing odds update", match_id=match_id)

    if not match_id:
        logger.warning("odds.updated message missing match_id")
        return

    try:
        service = AnalysisService()
        await service.analyze_match(str(match_id))
    except Exception as e:
        logger.error("Error re-analyzing match after odds update", match_id=match_id, error=str(e))
