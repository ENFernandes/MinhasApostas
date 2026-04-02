"""normalize.py — Normalização canónica de nomes de jogadores de ténis.

Forma canónica: "surname initial" em lowercase.
  "Djokovic N."    → "djokovic n"
  "Novak Djokovic" → "djokovic n"
  "De Minaur A."   → "de minaur a"
  "N. Djokovic"    → "djokovic n"

Nota: apelidos compostos vindos de nome completo sem inicial (ex: "Alex De Minaur")
produzem "minaur a" em vez de "de minaur a". Para esses casos, o fallback ILIKE
das queries cobre o match via token do apelido.
"""

from __future__ import annotations


def normalize_tennis_name(name: str) -> str:
    """Converte nome de jogador de ténis para forma canónica 'surname initial'."""
    name = name.strip()
    if not name:
        return name

    raw_parts = name.split()
    # Remover pontos finais e filtrar vazios
    parts = [p.rstrip(".").strip() for p in raw_parts]
    parts = [p for p in parts if p]

    if not parts:
        return name.lower()
    if len(parts) == 1:
        return parts[0].lower()

    singles = [p for p in parts if len(p) == 1]
    non_singles = [p for p in parts if len(p) > 1]

    if not non_singles:
        return name.lower()

    if singles:
        # Tem inicial explícita: "Djokovic N." / "De Minaur A." / "N. Djokovic"
        return " ".join(non_singles).lower() + " " + singles[0].lower()

    # Nome completo sem inicial: "Novak Djokovic"
    # Último token = apelido, primeira letra do primeiro token = inicial
    surname = non_singles[-1].lower()
    initial = non_singles[0][0].lower()
    return surname + " " + initial


def surname_token(normalized: str) -> str:
    """Extrai o token principal do apelido para uso em ILIKE fallback.

    "djokovic n" → "djokovic"
    "de minaur a" → "minaur"   (último token antes da inicial)
    """
    parts = normalized.split()
    if len(parts) >= 2:
        return parts[-2] if len(parts[-1]) == 1 else parts[-1]
    return parts[0] if parts else normalized
