"""OpenRouter LLM provider implementation."""

import os
import time

import httpx

from .provider import ILLMProvider, LLMResponse


DEFAULT_OPENROUTER_MODEL = "anthropic/claude-3.5-sonnet"
OPENROUTER_BASE_URL = "https://openrouter.ai/api/v1"
MAX_RETRIES = 3


class OpenRouterProvider:
    """OpenRouter API provider - access to multiple LLMs."""

    def __init__(
        self,
        api_key: str | None = None,
        model: str | None = None,
        fallback_model: str | None = None,
        base_url: str = OPENROUTER_BASE_URL,
    ):
        self.api_key = api_key or os.getenv("OPENROUTER_API_KEY")
        self._model = model or os.getenv("LLM_MODEL", DEFAULT_OPENROUTER_MODEL)
        self._fallback_model = fallback_model or os.getenv("LLM_FALLBACK_MODEL")
        self.base_url = base_url

        if not self.api_key:
            raise ValueError("OpenRouter API key not provided")

        self.client = httpx.AsyncClient(
            base_url=self.base_url,
            headers={
                "Authorization": f"Bearer {self.api_key}",
                "HTTP-Referer": "https://sportsbetting.ai",  # Required by OpenRouter
                "X-Title": "Sports Betting AI",
                "content-type": "application/json",
            },
            timeout=60.0,
        )

    @property
    def name(self) -> str:
        return "openrouter"

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
        """Send completion request to OpenRouter API."""
        start_time = time.time()

        messages = []
        if system:
            messages.append({"role": "system", "content": system})
        messages.append({"role": "user", "content": prompt})

        payload = {
            "model": self._model,
            "messages": messages,
            "max_tokens": max_tokens,
            "temperature": temperature,
        }

        # Try primary model, fallback if configured
        models_to_try = [self._model]
        if self._fallback_model:
            models_to_try.append(self._fallback_model)

        for model in models_to_try:
            payload["model"] = model

            for attempt in range(MAX_RETRIES):
                try:
                    response = await self.client.post(
                        "/chat/completions",
                        json=payload,
                    )
                    response.raise_for_status()
                    data = response.json()

                    latency_ms = (time.time() - start_time) * 1000

                    return {
                        "content": data["choices"][0]["message"]["content"],
                        "model": data.get("model", model),
                        "provider": self.name,
                        "usage": data.get("usage"),
                        "latency_ms": round(latency_ms, 2),
                    }

                except httpx.HTTPStatusError as e:
                    if e.response.status_code == 429 and attempt < MAX_RETRIES - 1:
                        time.sleep(2 ** attempt)
                        continue
                    # Try fallback model on error
                    break

                except Exception:
                    if attempt < MAX_RETRIES - 1:
                        time.sleep(2 ** attempt)
                        continue
                    break

        raise RuntimeError("All models failed")

    async def health_check(self) -> bool:
        """Check if OpenRouter API is accessible."""
        try:
            response = await self.client.get("/models")
            return response.status_code == 200
        except Exception:
            return False

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.client.aclose()
