"""Prompt builder for match analysis."""

import json
from pathlib import Path
from typing import TypedDict


class MatchContext(TypedDict):
    """Context information for a match."""

    key_players_absent: list[str]
    decisive_match: bool
    back_to_back: bool
    h2h_note: str
    recent_form: str
    weather: str | None
    other_notes: str


class ModelProbabilities(TypedDict):
    """Probabilities from statistical models."""

    home: float
    draw: float
    away: float
    over_2_5: float
    btts: float


class RecommendationOutput(TypedDict):
    """Standard output format for LLM recommendations."""

    match_id: str
    recommended_market: dict
    alternative_markets: list[dict]
    reasoning: str
    confidence: int
    context_flags: dict


class PromptBuilder:
    """Builds prompts for LLM match analysis."""

    def __init__(self, prompts_dir: str | None = None):
        """Initialize with prompts directory.

        Args:
            prompts_dir: Directory containing prompt templates
        """
        if prompts_dir is None:
            # Default to app/prompts relative to this file
            current_file = Path(__file__)
            prompts_dir = current_file.parent.parent.parent / "prompts"

        self.prompts_dir = Path(prompts_dir)
        self._load_templates()

    def _load_templates(self) -> None:
        """Load prompt templates from files."""
        self.templates = {}

        # Default templates if files don't exist
        self.templates["match_analysis"] = """Você é um analista especialista em apostas desportivas com foco em value betting.
O seu papel é analisar os dados estatísticos de um jogo e identificar o mercado
com maior valor esperado, considerando tanto a matemática como o contexto.

Regras que nunca pode violar:
- Só recomendar apostas com value ≥ 5%
- Odds entre 1.50 e 8.00 apenas
- Confiança mínima de 6/10 para emitir recomendação
- Se não há value claro, diz explicitamente "sem recomendação"

Dados do jogo:
{match_data}

Probabilidades do modelo estatístico:
{model_probabilities}

Odds disponíveis por bookmaker:
{available_odds}

Contexto adicional:
{context}

Responda APENAS em JSON válido com a estrutura definida no CONTEXT.md.
Campo "reasoning" em português, máximo 3 frases, explicando o raciocínio.
Campo "confidence" de 1 a 10.
"""

        # Try to load from files if they exist
        for template_name in ["match_analysis", "inplay_analysis", "tennis_analysis"]:
            file_path = self.prompts_dir / f"{template_name}.txt"
            if file_path.exists():
                self.templates[template_name] = file_path.read_text(encoding="utf-8")

    def build_match_analysis_prompt(
        self,
        match_data: dict,
        model_probabilities: ModelProbabilities,
        available_odds: dict[str, dict],
        context: MatchContext | None = None,
        is_inplay: bool = False,
        is_tennis: bool = False,
    ) -> str:
        """Build prompt for match analysis.

        Args:
            match_data: Basic match information
            model_probabilities: Statistical model outputs
            available_odds: Odds by bookmaker
            context: Additional context
            is_inplay: Whether match is live
            is_tennis: Whether it's a tennis match

        Returns:
            Formatted prompt string
        """
        # Select appropriate template
        if is_tennis:
            template = self.templates.get("tennis_analysis", self.templates["match_analysis"])
        elif is_inplay:
            template = self.templates.get("inplay_analysis", self.templates["match_analysis"])
        else:
            template = self.templates["match_analysis"]

        # Format match data
        match_str = json.dumps(match_data, indent=2, ensure_ascii=False)

        # Format probabilities
        probs_str = json.dumps(model_probabilities, indent=2)

        # Format odds
        odds_str = json.dumps(available_odds, indent=2, ensure_ascii=False)

        # Format context
        if context:
            context_str = json.dumps(context, indent=2, ensure_ascii=False)
        else:
            context_str = "Nenhum contexto adicional disponível."

        return template.format(
            match_data=match_str,
            model_probabilities=probs_str,
            available_odds=odds_str,
            context=context_str,
        )

    def parse_llm_response(self, response: str) -> RecommendationOutput:
        """Parse LLM response into structured output.

        Args:
            response: Raw LLM response text

        Returns:
            Parsed recommendation output

        Raises:
            ValueError: If response cannot be parsed
        """
        import re

        # Try to extract JSON from response
        # Look for JSON block or plain JSON
        json_match = re.search(r'\{.*\}', response, re.DOTALL)

        if not json_match:
            raise ValueError("No JSON found in LLM response")

        try:
            data = json.loads(json_match.group())

            # Validate required fields
            required_fields = ["recommended_market", "confidence", "reasoning"]
            for field in required_fields:
                if field not in data:
                    raise ValueError(f"Missing required field: {field}")

            # Validate confidence range
            confidence = data.get("confidence", 0)
            if not 1 <= confidence <= 10:
                raise ValueError(f"Confidence must be between 1 and 10, got {confidence}")

            return {
                "match_id": data.get("match_id", ""),
                "recommended_market": data["recommended_market"],
                "alternative_markets": data.get("alternative_markets", []),
                "reasoning": data["reasoning"],
                "confidence": confidence,
                "context_flags": data.get("context_flags", {}),
            }

        except json.JSONDecodeError as e:
            raise ValueError(f"Invalid JSON in response: {e}")

    def create_system_prompt(self, is_tennis: bool = False) -> str:
        """Create system prompt for the LLM.

        Args:
            is_tennis: Whether this is for tennis analysis

        Returns:
            System prompt string
        """
        base = """Você é um analista especialista em apostas desportivas com foco em value betting.
O seu objetivo é identificar oportunidades onde a probabilidade real de um evento
é superior à probabilidade implícita nas odds oferecidas pelos bookmakers.

Você deve:
1. Analisar os dados estatísticos objectivamente
2. Considerar o contexto (formas, lesões, importância do jogo)
3. Calcular o value de cada mercado disponível
4. Recomendar apenas quando o value for ≥ 5%
5. Usar Kelly Criterion para dimensionar a aposta
6. Ser honesto quando não houver value claro

Você NUNCA deve:
1. Recomendar apostas com value < 5%
2. Ignorar odds fora do intervalo 1.50-8.00
3. Emitir recomendações com confiança < 6/10
4. Fazer recomendações genéricas sem fundamentação matemática"""

        if is_tennis:
            base += """

Análise específica para Ténis:
- Considerar superfície do torneio (ELO específico)
- Fadiga do jogador (dias desde último jogo)
- Ranking ATP/WTA actual
- Histórico H2H nessa superfície específica"""

        return base


# Global instance
_prompt_builder: PromptBuilder | None = None


def get_prompt_builder() -> PromptBuilder:
    """Get or create prompt builder singleton."""
    global _prompt_builder
    if _prompt_builder is None:
        _prompt_builder = PromptBuilder()
    return _prompt_builder
