namespace SportsBetting.DataCollector.Infrastructure.Services;

/// <summary>
/// Normaliza nomes de jogadores de ténis para a forma canónica "surname initial" em lowercase.
/// Espelha a lógica de normalize_tennis_name() em Python (normalize.py) e a função
/// PostgreSQL normalize_tennis_name() (V017).
///
/// Exemplos:
///   "Djokovic N."    → "djokovic n"
///   "Novak Djokovic" → "djokovic n"
///   "R. Hijikata"    → "hijikata r"
///   "De Minaur A."   → "de minaur a"
/// </summary>
public static class TennisNameNormalizer
{
    /// <summary>
    /// Converte um nome de jogador de ténis para a forma canónica "surname initial".
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var raw = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parts = raw.Select(p => p.TrimEnd('.')).Where(p => p.Length > 0).ToArray();

        if (parts.Length == 0) return name.ToLowerInvariant();
        if (parts.Length == 1) return parts[0].ToLowerInvariant();

        var singles    = parts.Where(p => p.Length == 1).ToArray();
        var nonSingles = parts.Where(p => p.Length >  1).ToArray();

        if (nonSingles.Length == 0) return name.ToLowerInvariant();

        if (singles.Length > 0)
        {
            // Tem inicial explícita: "Djokovic N." / "De Minaur A." / "R. Hijikata"
            return string.Join(' ', nonSingles).ToLowerInvariant() + ' ' + singles[0].ToLowerInvariant();
        }

        // Nome completo sem inicial: "Novak Djokovic" → "djokovic n"
        return nonSingles[^1].ToLowerInvariant() + ' ' + nonSingles[0][0].ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Extrai o token principal do apelido para usar em ILIKE fuzzy.
    /// "djokovic n" → "djokovic"
    /// "de minaur a" → "minaur"
    /// </summary>
    public static string SurnameToken(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized)) return normalized ?? string.Empty;
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return parts[^1].Length == 1 ? parts[^2] : parts[^1];
        return parts.Length > 0 ? parts[0] : normalized;
    }
}
