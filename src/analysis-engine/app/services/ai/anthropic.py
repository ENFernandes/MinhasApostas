"""Anthropic LLM provider implementation."""

import asyncio
import os
import time

import httpx

from .provider import ILLMProvider, LLMResponse


DEFAULT_ANTHROPIC_MODEL = "claude-3-5-sonnet-20241022"
ANTHROPIC_BASE_URL = "https://api.anthropic.com/v1"
MAX_RETRIES = 3


class AnthropicProvider:
    """Anthropic Claude API provider using httpx."""

    def __init__(
        self,
        api_key: str | None = None,
        model: str | None = None,
        base_url: str = ANTHROPIC_BASE_URL,
    ):
        self.api_key = api_key or os.getenv("ANTHROPIC_API_KEY")
        self._model = model or os.getenv("LLM_MODEL", DEFAULT_ANTHROPIC_MODEL)
        self.base_url = base_url

        if not self.api_key:
            raise ValueError("Anthropic API key not provided")

        self.client = httpx.AsyncClient(
            base_url=self.base_url,
            headers={
                "x-api-key": self.api_key,
                "anthropic-version": "2023-06-01",
                "content-type": "application/json",
            },
            timeout=60.0,
        )

    @property
    def name(self) -> str:
        return "anthropic"

    @property
    def model(self) -> str:
        return self._model

    async def complete(
        self,
        prompt: str,
        system: str | None = None,
        temperature: float = 0.7,
        max_tokens: int = 2000,
    ) -> LLMResponse:
        """Send completion request to Anthropic API with retry logic."""
        start_time = time.time()

        messages = [{"role": "user", "content": prompt}]

        payload = {
            "model": self._model,
            "messages": messages,
            "max_tokens": max_tokens,
            "temperature": temperature,
        }

        if system:
            payload["system"] = system

        # Retry with exponential backoff
        for attempt in range(MAX_RETRIES):
            try:
                response = await self.client.post(
                    "/messages",
                    json=payload,
                )
                response.raise_for_status()
                data = response.json()

                latency_ms = (time.time() - start_time) * 1000

                return {
                    "content": data["content"][0]["text"],
                    "model": data.get("model", self._model),
                    "provider": self.name,
                    "usage": data.get("usage"),
                    "latency_ms": round(latency_ms, 2),
                }

            except httpx.HTTPStatusError as e:
                if e.response.status_code == 429 and attempt < MAX_RETRIES - 1:
                    await asyncio.sleep(2 ** attempt)
                    continue
                raise

            except Exception:
                if attempt < MAX_RETRIES - 1:
                    await asyncio.sleep(2 ** attempt)
                    continue
                raise

        raise RuntimeError("Max retries exceeded")

    async def health_check(self) -> bool:
        """Check if Anthropic API is accessible."""
        try:
            # Try a minimal request
            response = await self.client.post(
                "/messages",
                json={
                    "model": self._model,
                    "messages": [{"role": "user", "content": "hi"}],
                    "max_tokens": 1,
                },
            )
            return response.status_code == 200
        except Exception:
            return False

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.client.aclose()
