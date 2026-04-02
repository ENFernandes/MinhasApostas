"""RabbitMQ consumer for dead letter queue monitoring."""

import aio_pika
from structlog import get_logger

logger = get_logger()


class DeadLetterConsumer:
    """Consumer para a fila dead.letters — regista erros com contexto para diagnóstico."""

    def __init__(self, rabbitmq_url: str) -> None:
        self.rabbitmq_url = rabbitmq_url
        self.connection: aio_pika.RobustConnection | None = None
        self.channel: aio_pika.Channel | None = None

    async def connect(self) -> None:
        """Establish connection to RabbitMQ and start consuming dead letters."""
        try:
            self.connection = await aio_pika.connect_robust(self.rabbitmq_url)
            self.channel = await self.connection.channel()

            # A fila dead.letters já existe — declarar como passive para não recriar
            queue = await self.channel.declare_queue(
                "dead.letters",
                durable=True,
                passive=True,
            )

            await queue.consume(self._on_message)
            logger.info("Dead letter consumer connected", queue="dead.letters")

        except Exception as e:
            logger.error("Failed to connect dead letter consumer", error=str(e))
            raise

    async def disconnect(self) -> None:
        """Close RabbitMQ connection."""
        if self.connection:
            await self.connection.close()
            logger.info("Dead letter consumer disconnected")

    async def _on_message(self, message: aio_pika.IncomingMessage) -> None:
        """Regista cada dead letter com contexto suficiente para diagnóstico."""
        async with message.process(ignore_processed=True):
            headers = message.headers or {}
            original_queue = headers.get("x-first-death-queue", "unknown")
            reason = headers.get("x-first-death-reason", "unknown")
            routing_key = message.routing_key or "unknown"
            body_preview = message.body[:300].decode(errors="replace")

            logger.error(
                "Dead letter received",
                original_queue=original_queue,
                reason=reason,
                routing_key=routing_key,
                body_preview=body_preview,
                message_id=message.message_id,
            )
