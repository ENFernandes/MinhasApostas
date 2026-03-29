"""Factory for creating LLM provider instances."""

import os

from .anthropic import AnthropicProvider
from .ollama import OllamaProvider
from .openrouter import OpenRouterProvider
from .provider import ILLMProvider


class ProviderFactory:
    """Factory for creating LLM provider instances based on environment config."""

    @staticmethod
    def create_provider(
        provider_name: str | None = None,
    ) -> ILLMProvider:
        """Create an LLM provider instance.

        Args:
            provider_name: Provider to use (anthropic, openrouter, ollama)
                          If None, uses LLM_PROVIDER env var

        Returns:
            ILLMProvider instance

        Raises:
            ValueError: If provider is not recognized
        """
        provider = provider_name or os.getenv("LLM_PROVIDER", "ollama")

        match provider.lower():
            case "anthropic":
                return AnthropicProvider()

            case "openrouter":
                return OpenRouterProvider()

            case "ollama":
                return OllamaProvider()

            case _:
                raise ValueError(
                    f"Unknown LLM provider: {provider}. "
                    "Use: anthropic, openrouter, or ollama"
                )

    @staticmethod
    def get_available_providers() -> list[str]:
        """Get list of available provider names."""
        return ["anthropic", "openrouter", "ollama"]

    @staticmethod
    def check_provider_health(provider_name: str | None = None) -> dict[str, bool]:
        """Check health of all providers or specific one.

        Args:
            provider_name: Specific provider to check, or None for all

        Returns:
            Dictionary of provider name -> health status
        """
        import asyncio

        async def _check():
            results = {}

            providers = (
                [provider_name]
                if provider_name
                else ProviderFactory.get_available_providers()
            )

            for name in providers:
                try:
                    provider = ProviderFactory.create_provider(name)
                    results[name] = await provider.health_check()
                except Exception:
                    results[name] = False

            return results

        return asyncio.run(_check())


def get_provider() -> ILLMProvider:
    """Get the configured LLM provider.

    Returns:
        ILLMProvider instance based on LLM_PROVIDER env var
    """
    return ProviderFactory.create_provider()
