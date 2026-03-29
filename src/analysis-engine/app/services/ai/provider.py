"""LLM Provider abstraction layer."""

from typing import Protocol, TypedDict


class LLMResponse(TypedDict):
    """Standard response format from LLM providers."""

    content: str
    model: str
    provider: str
    usage: dict[str, int] | None
    latency_ms: float


class ILLMProvider(Protocol):
    """Protocol for LLM provider implementations.

    All LLM providers must implement this interface.
    """

    async def complete(
        self,
        prompt: str,
        system: str | None = None,
        temperature: float = 0.7,
        max_tokens: int = 2000,
    ) -> LLMResponse:
        """Send a completion request to the LLM.

        Args:
            prompt: The user prompt
            system: Optional system message
            temperature: Sampling temperature (0-2)
            max_tokens: Maximum tokens to generate

        Returns:
            Standardized LLM response
        """
        ...

    async def health_check(self) -> bool:
        """Check if the provider is available.

        Returns:
            True if provider is healthy
        """
        ...

    @property
    def name(self) -> str:
        """Provider name."""
        ...

    @property
    def model(self) -> str:
        """Current model name."""
        ...
