"""Serviço de alertas — envia notificações Telegram para recomendações de alta confiança."""

import os

import httpx
from structlog import get_logger

logger = get_logger()

TELEGRAM_BASE = "https://api.telegram.org"
MIN_CONFIDENCE = int(os.getenv("ALERT_MIN_CONFIDENCE", "7"))


async def send_telegram_alert(recommendation: dict) -> None:
    """Envia alerta Telegram quando uma recomendação tem confiança >= MIN_CONFIDENCE.

    Falha silenciosa — não interrompe o fluxo de análise se o Telegram não estiver configurado.

    Args:
        recommendation: dicionário com os campos da recomendação (match_id, market, outcome, odd,
                         value, confidence, kelly_fraction, stake_euros).
    """
    token = os.getenv("TELEGRAM_BOT_TOKEN")
    chat_id = os.getenv("TELEGRAM_CHAT_ID")

    if not token or not chat_id:
        return  # Telegram não configurado — silencioso

    confidence = recommendation.get("confidence", 0)
    if confidence < MIN_CONFIDENCE:
        return

    value = recommendation.get("value", 0)
    kelly = recommendation.get("kelly_fraction", 0)
    stake = recommendation.get("stake_euros", 0)
    odd = recommendation.get("odd", 0)
    market = recommendation.get("market", "—")
    outcome = recommendation.get("outcome", "—")
    match_id = recommendation.get("match_id", "—")

    text = (
        f"🎯 *Nova recomendação — confiança {confidence}/10*\n\n"
        f"Jogo ID: `{match_id}`\n"
        f"Mercado: *{market}* → *{outcome}*\n"
        f"Odd: `{odd:.2f}` | Value: `+{value:.1%}`\n"
        f"Kelly: `{kelly:.1%}` | Stake sugerido: `€{stake:.2f}`"
    )

    try:
        async with httpx.AsyncClient(timeout=10) as client:
            response = await client.post(
                f"{TELEGRAM_BASE}/bot{token}/sendMessage",
                json={
                    "chat_id": chat_id,
                    "text": text,
                    "parse_mode": "Markdown",
                },
            )
            if response.status_code == 200:
                logger.info(
                    "Telegram alert sent",
                    match_id=match_id,
                    confidence=confidence,
                )
            else:
                logger.warning(
                    "Telegram alert failed",
                    status=response.status_code,
                    body=response.text[:200],
                )
    except Exception as e:
        logger.warning("Telegram alert error", error=str(e))
