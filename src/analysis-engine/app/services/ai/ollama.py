"""Ollama local LLM provider implementation."""

import os
import time

import httpx

from .provider import ILLMProvider, LLMResponse


DEFAULT_OLLAMA_MODEL = "llama3.1:8b"
DEFAULT_OLLAMA_URL = "http://localhost:11434"
MAX_RETRIES = 3


class OllamaProvider:
    """Ollama local LLM provider for development."""

    def __init__(
        self,
        base_url: str | None = None,
        model: str | None = None,
    ):
        self.base_url = base_url or os.getenv("OLLAMA_BASE_URL", DEFAULT_OLLAMA_URL)
        self._model = model or os.getenv("LLM_MODEL", DEFAULT_OLLAMA_MODEL)

        self.client = httpx.AsyncClient(
            base_url=self.base_url,
            timeout=120.0,  # Local models can be slow
        )

    @property
    def name(self) -> str:
        return "ollama"

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
        """Send completion request to Ollama API."""
        start_time = time.time()

        # Build the prompt with system message if provided
        full_prompt = prompt
        if system:
            full_prompt = f"{system}\n\n{prompt}"

        payload = {
            "model": self._model,
            "prompt": full_prompt,
            "stream": False,
            "options": {
                "temperature": temperature,
                "num_predict": max_tokens,
            },
        }

        for attempt in range(MAX_RETRIES):
            try:
                response = await self.client.post(
                    "/api/generate",
                    json=payload,
                )
                response.raise_for_status()
                data = response.json()

                latency_ms = (time.time() - start_time) * 1000

                return {
                    "content": data["response"],
                    "model": self._model,
                    "provider": self.name,
                    "usage": None,  # Ollama doesn't provide usage stats
                    "latency_ms": round(latency_ms, 2),
                }

            except Exception:
                if attempt < MAX_RETRIES - 1:
                    time.sleep(2 ** attempt)
                    continue
                raise

        raise RuntimeError("Max retries exceeded")

    async def health_check(self) -> bool:
        """Check if Ollama is running and model is available."""
        try:
            # Check if Ollama is running
            response = await self.client.get("/api/tags")
            if response.status_code != 200:
                return False

            # Check if our model is available
            data = response.json()
            models = data.get("models", [])

            return any(m.get("name") == self._model for m in models)

        except Exception:
            return False

    async def pull_model(self) -> bool:
        """Pull the model if not available."""
        try:
            response = await self.client.post(
                "/api/pull",
                json={"name": self._model, "stream": False},
                timeout=300.0,  # Model download can take time
            )
            return response.status_code == 200
        except Exception:
            return False

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.client.aclose()
