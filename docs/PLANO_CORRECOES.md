# Plano de Correções — 3 Problemas Identificados

> Análise realizada em 2026-03-27. 
> **Última atualização: 2026-03-30** — Todas as correções foram aplicadas.

---

## Estado: ✅ TODAS AS CORREÇÕES APLICADAS

| Fix | Descrição | Ficheiro | Estado |
|-----|-----------|----------|--------|
| 1A | MatchRepository - filtrar jogos passados | `src/DataCollector/DataCollector.Infrastructure/Repositories/MatchRepository.cs` | ✅ Aplicado |
| 1B | FootballCollectorJob - buscar desde ontem + cron 2h | `src/DataCollector/DataCollector.Infrastructure/Jobs/FootballCollectorJob.cs` | ✅ Aplicado |
| 1C | useApi.ts - defaultFrom = now | `src/frontend/src/hooks/useApi.ts` | ✅ Aplicado |
| 2A | Program.cs - JSON serialization UTC com Z | `src/DataCollector/DataCollector.Api/Program.cs` | ✅ Aplicado |
| 2B | Entidades - DateTime.SpecifyKind.Utc | Jobs e Controllers | ✅ Aplicado |
| 3A | analysis_service.py - Over2.5/BTTS com odds reais | `src/analysis-engine/app/services/analysis_service.py` | ✅ Aplicado |
| 3B | OddsCollectorJob - mapeamento por nome das equipas | `src/DataCollector/DataCollector.Infrastructure/Jobs/OddsCollectorJob.cs` | ✅ Aplicado |
| 3C | analysis.py - endpoint /latest | `src/analysis-engine/app/routers/analysis.py` | ✅ Aplicado |
| 3D | analysis_service.py - bankroll dinâmico | `src/analysis-engine/app/services/analysis_service.py` | ✅ Aplicado |

---

## PROBLEMA 1 — Dashboard mostra jogos passados

### Correções Aplicadas

#### Fix 1A — `MatchRepository.cs` ✅

**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Repositories/MatchRepository.cs`

Implementado filtro que exclui jogos SCHEDULED que já começaram (margem 30 min para atrasos):

```csharp
var now = DateTime.UtcNow;
var query = _context.Matches
    .Where(m => m.CommenceTime <= to)
    .Where(m =>
        // Jogos ao vivo: mostrar sempre independente da hora
        (m.Status == "LIVE" || m.Status == "IN_PLAY" || m.Status == "PAUSED")
        ||
        // Jogos agendados: só mostrar se ainda não começaram (margem 30 min para atrasos)
        ((m.Status == "SCHEDULED" || m.Status == null || m.Status == "")
         && m.CommenceTime >= now.AddMinutes(-30))
    );
```

#### Fix 1B — `FootballCollectorJob.cs` ✅

**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Jobs/FootballCollectorJob.cs`

- Cron alterado para `"0 */2 * * *"` (de 2 em 2 horas)
- `dateFrom` agora busca desde ontem para atualizar statuses:

```csharp
var dateFrom = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"); // inclui ontem para atualizar statuses
var dateTo = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd");
```

#### Fix 1C — `useApi.ts` ✅

**Ficheiro:** `src/frontend/src/hooks/useApi.ts`

```typescript
const now = new Date()
const defaultFrom = now // momento atual, não meia-noite
```

---

## PROBLEMA 2 — Horários não estão no timezone de Portugal

### Correções Aplicadas

#### Fix 2A — Configurar JSON serialization no C# ✅

**Ficheiro:** `src/DataCollector/DataCollector.Api/Program.cs`

Adicionado conversor customizado `UtcDateTimeConverter`:

```csharp
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            return DateTime.MinValue;

        if (DateTime.TryParse(value, out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }
        
        return DateTime.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Always write as UTC with Z suffix
        var utcValue = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}
```

Registado em `AddJsonOptions`:
```csharp
options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
```

#### Fix 2B — Garantir DateTimeKind.Utc nas entidades ✅

Implementado em:
- `FootballCollectorJob.cs` linha 117: `CommenceTime = DateTime.SpecifyKind(matchDto.UtcDate.Value, DateTimeKind.Utc)`
- `TennisCollectorJob.cs` linhas 143, 147
- `MatchesController.cs` linha 51: `CommenceTime = DateTime.SpecifyKind(m.CommenceTime, DateTimeKind.Utc)`

---

## PROBLEMA 3 — Estatísticas e Odds mockadas

### Correções Aplicadas

#### Fix 3A — Calcular Over2.5 e BTTS com odds reais ✅

**Ficheiro:** `src/analysis-engine/app/services/analysis_service.py`

Método `_calculate_probabilities` agora calcula Over2.5 e BTTS a partir das odds reais:
- Se existirem odds de totals, calcula implied probability sem vig
- Se não existirem, usa heurística baseada no equilíbrio do jogo

```python
# Calcular Over2.5 a partir das odds de totals (se disponíveis)
over25_odds = [o.odd for o in odds if o.market in ("totals", "over_under") and "2.5" in str(o.outcome) and "over" in str(o.outcome).lower()]
# ... cálculo com par over/under para remover vig

# Calcular BTTS a partir das odds de BTTS (se disponíveis)
btts_yes_odds = [o.odd for o in odds if o.market == "btts" and o.outcome.lower() in ("yes", "sim")]
# ... cálculo similar
```

#### Fix 3B — Corrigir mapeamento de IDs no OddsCollectorJob ✅

**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Jobs/OddsCollectorJob.cs`

Agora mapeia por nome das equipas + data em vez de usar o ID da Odds API:

```csharp
// Mapear evento da Odds API para o nosso match interno por nome de equipa + data
var matchEntity = await _matchRepository.FindByTeamNamesAndDateAsync(
    eventOdds.HomeTeam,
    eventOdds.AwayTeam,
    eventOdds.CommenceTime.Value,
    cancellationToken);

if (matchEntity is null)
{
    _logger.LogInformation("No match found for {Home} vs {Away} on {Date}",
        eventOdds.HomeTeam, eventOdds.AwayTeam, eventOdds.CommenceTime);
    continue;
}
```

O método `FindByTeamNamesAndDateAsync` já existe no `MatchRepository` com fuzzy matching.

#### Fix 3C — Implementar endpoint `/analysis/match/{id}/latest` ✅

**Ficheiros:** 
- `src/analysis-engine/app/routers/analysis.py` — endpoint implementado
- `src/analysis-engine/app/db/repositories.py` — método `get_latest_by_match_id` adicionado

O endpoint agora retorna a recomendação guardada para o jogo, ou 404 se não existir análise.

#### Fix 3D — Bankroll dinâmico via variável de ambiente ✅

**Ficheiro:** `src/analysis-engine/app/services/analysis_service.py`

```python
bankroll = float(os.getenv("DEFAULT_BANKROLL", "100"))
max_stake_pct = float(os.getenv("MAX_STAKE_PCT", "0.05"))  # 5% por defeito
stake = min(kelly * bankroll, bankroll * max_stake_pct)
```

---

## Notas Importantes

1. **Odds API mapeamento (Fix 3B)**: A `the-odds-api.com` usa nomes de equipas em inglês (ex: "Sporting CP", "Benfica") enquanto a `football-data.org` pode usar nomes diferentes. O matching fuzzy por nome pode falhar. Uma solução mais robusta seria guardar o `ExternalId` da Odds API numa coluna separada da tabela `matches`.

2. **Dados estatísticos reais (Over2.5/BTTS)**: Mesmo com o Fix 3A, as probabilidades são calculadas a partir das odds do mercado (implied probability), não de dados históricos reais. Para cálculo verdadeiramente estatístico, é necessário implementar o modelo de Poisson descrito no `CONTEXT.md` com dados históricos da API.

3. **Timezone storage**: Os dados são guardados em UTC na BD (correto). O Fix 2 garante que a serialização JSON inclui o `Z` para que o browser interprete corretamente.

---

## Histórico de Alterações

| Data | Descrição |
|------|-----------|
| 2026-03-27 | Plano de correções criado |
| 2026-03-30 | Todas as correções aplicadas (Fix 1A a 3D) |
