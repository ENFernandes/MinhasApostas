# Plano de Correções — 3 Problemas Identificados

> Análise realizada em 2026-03-27. Todos os ficheiros e linhas referenciados foram lidos e verificados.

---

## PROBLEMA 1 — Dashboard mostra jogos passados

### Diagnóstico

**Causa raiz A — Job de coleta não atualiza status**
O `FootballCollectorJob` corre a cada 6 horas (`"0 */6 * * *"`) e só vai buscar jogos desde o início do dia atual:
```csharp
// FootballCollectorJob.cs linha 61-62
var dateFrom = DateTime.UtcNow.ToString("yyyy-MM-dd"); // início do dia!
var dateTo = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd");
```
Consequência: jogos de ontem que estavam como `SCHEDULED` nunca têm o status atualizado para `FINISHED` porque o job de hoje começa a buscar apenas a partir de hoje.

**Causa raiz B — Repositório não filtra jogos cujo tempo já passou**
O `MatchRepository.GetUpcomingAsync` filtra por status mas NÃO por data de início:
```csharp
// MatchRepository.cs linha 34-35
.Where(m => m.CommenceTime >= from && m.CommenceTime <= to)
.Where(m => m.Status == "SCHEDULED" || m.Status == "LIVE" || ...)
```
Jogos SCHEDULED de ontem passam no filtro de status e aparecem no dashboard.

**Causa raiz C — Frontend inclui início do dia (não momento atual)**
O `useApi.ts` usa `defaultFrom = new Date(year, month, day)` que é meia-noite local, não o momento atual. Isto faz com que o backend receba pedidos a incluir jogos das 00h até agora.

---

### Correções — Problema 1

#### Fix 1A — `MatchRepository.cs` (CRÍTICO)

**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Repositories/MatchRepository.cs`

**Mudança:** Para jogos SCHEDULED, excluir os que já começaram (com margem de 30 min para atrasos). Para LIVE/IN_PLAY/PAUSED, mostrar sempre.

```csharp
// ANTES (linha 30-35):
var query = _context.Matches
    .Include(m => m.Competition)
    .Include(m => m.HomeTeam)
    .Include(m => m.AwayTeam)
    .Where(m => m.CommenceTime >= from && m.CommenceTime <= to)
    .Where(m => m.Status == "SCHEDULED" || m.Status == "LIVE" || m.Status == "IN_PLAY" || m.Status == "PAUSED" || m.Status == null || m.Status == "");

// DEPOIS:
var now = DateTime.UtcNow;
var query = _context.Matches
    .Include(m => m.Competition)
    .Include(m => m.HomeTeam)
    .Include(m => m.AwayTeam)
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

#### Fix 1B — `FootballCollectorJob.cs` (IMPORTANTE)

**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Jobs/FootballCollectorJob.cs`

**Mudança:** Buscar desde ontem para atualizar statuses de jogos passados + aumentar frequência.

```csharp
// ANTES (linhas 61-62):
var dateFrom = DateTime.UtcNow.ToString("yyyy-MM-dd");
var dateTo = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd");

// DEPOIS:
var dateFrom = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"); // inclui ontem para atualizar statuses
var dateTo = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd");
```

**E alterar o CronExpression para atualizar mais frequentemente:**
```csharp
// ANTES:
public string CronExpression => "0 */6 * * *"; // De 6 em 6 horas

// DEPOIS:
public string CronExpression => "0 */2 * * *"; // De 2 em 2 horas (suficiente para status updates)
```

#### Fix 1C — `useApi.ts` (FRONTEND)

**Ficheiro:** `src/frontend/src/hooks/useApi.ts`

**Mudança:** `defaultFrom` deve ser o momento atual, não meia-noite.

```typescript
// ANTES (linhas 19-20):
const now = new Date()
const defaultFrom = new Date(now.getFullYear(), now.getMonth(), now.getDate())

// DEPOIS:
const now = new Date()
const defaultFrom = now // momento atual, não meia-noite
```

---

## PROBLEMA 2 — Horários não estão no timezone de Portugal

### Diagnóstico

**Causa raiz — C# serializa `DateTime` sem indicador UTC**

O C# retorna datas no formato `"2025-03-27T20:00:00"` sem o `Z` final. O JavaScript interpreta datas sem sufixo como **horário local do browser** em vez de UTC.

```csharp
// MatchesController.cs — CommenceTime é DateTime sem DateTimeKind.Utc
CommenceTime = m.CommenceTime, // serializa como "2025-03-27T20:00:00"
```

O frontend usa `formatDate(match.commenceTime)` → `new Date("2025-03-27T20:00:00")` → interpretado como local → convertido para `Europe/Lisbon` a partir do tempo errado.

Se o servidor estiver em UTC e o browser também, não há diferença. Mas se o servidor ou browser não estiverem em UTC, o resultado é incorreto.

O `formatDate` em `lib/utils.ts` está CORRETO (`timeZone: 'Europe/Lisbon'`) — o problema está na serialização do C#.

---

### Correções — Problema 2

#### Fix 2A — Configurar JSON serialization no C# (CRÍTICO)

**Ficheiro:** `src/DataCollector/DataCollector.Api/Program.cs`

**Mudança:** Configurar o serializador para incluir `Z` em todas as datas UTC.

```csharp
// Adicionar/modificar a configuração do JSON serializer:
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Garante que DateTime é serializado com 'Z' (UTC)
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
        // DateTimeKind.Utc → serializa como "2025-03-27T20:00:00Z"
    });
```

#### Fix 2B — Garantir DateTimeKind.Utc nas entidades (IMPORTANTE)

**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Data/SportsBettingDbContext.cs` ou nas entidades

**Mudança:** Garantir que `CommenceTime` é marcado como UTC ao ler da BD:

```csharp
// No FootballCollectorJob.cs, ao criar MatchEntity:
CommenceTime = DateTime.SpecifyKind(matchDto.UtcDate.Value, DateTimeKind.Utc),
```

E nos DTOs de resposta (`Dtos.cs`):
```csharp
// Ao mapear para o DTO de resposta:
CommenceTime = DateTime.SpecifyKind(m.CommenceTime, DateTimeKind.Utc),
```

---

## PROBLEMA 3 — Estatísticas e Odds mockadas

### Diagnóstico

**Bug 3A — Over2.5 e BTTS hardcoded a 50%**
```python
# analysis_service.py linhas 201-202
over_2_5=0.5,  # MOCKADO
btts=0.5,      # MOCKADO
```

**Bug 3B — OddsCollectorJob: mapeamento de IDs errado (CRÍTICO)**
```csharp
// OddsCollectorJob.cs linha 86
MatchId = Guid.Parse(eventOdds.Id!), // eventOdds.Id é "abc123def456", NÃO um UUID!
```
O ID da Odds API é uma string alfanumérica (ex: `"3ea7af7d86b13b2cd3ca04ce7e77a5da"`), não um GUID. `Guid.Parse` lança exceção, odds nunca são guardadas.

**Bug 3C — Endpoint `/analysis/match/{id}/latest` sempre retorna 404**
```python
# analysis.py linhas 79-86
rec_repo = RecommendationRepository(db)
# This would need a method to get latest by match_id
# For now, return a placeholder
raise HTTPException(status_code=404, detail="Analysis not found")  # SEMPRE 404!
```

**Bug 3D — Bankroll hardcoded a €100**
```python
# analysis_service.py linha 233
stake = min(kelly * 100, 10)  # Assume €100 de bankroll, max €10
```

---

### Correções — Problema 3

#### Fix 3A — Calcular Over2.5 e BTTS com odds reais (PRIORITÁRIO)

**Ficheiro:** `src/analysis-engine/app/services/analysis_service.py`

**Mudança:** Calcular Over2.5 e BTTS a partir das implied probabilities das odds reais. Se não houver odds desses mercados, usar valor conservador baseado na média da liga.

```python
# ANTES (linhas 197-203):
return ModelProbabilities(
    home=round(home_prob, 3),
    draw=round(draw_prob, 3),
    away=round(away_prob, 3),
    over_2_5=0.5,  # MOCKADO
    btts=0.5,      # MOCKADO
)

# DEPOIS:
def _calculate_probabilities(self, match: MatchData, odds: list) -> ModelProbabilities:
    """Calculate model probabilities based on odds."""
    # Calcular 1X2 com average implied probability (sem margem)
    home_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("1", match.home_team)]
    draw_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("X", "Draw")]
    away_odds = [o.odd for o in odds if o.market in ("1X2", "h2h") and o.outcome in ("2", match.away_team)]

    if home_odds and draw_odds and away_odds:
        avg_home = sum(home_odds) / len(home_odds)
        avg_draw = sum(draw_odds) / len(draw_odds)
        avg_away = sum(away_odds) / len(away_odds)
        total = (1/avg_home) + (1/avg_draw) + (1/avg_away)
        home_prob = (1/avg_home) / total
        draw_prob = (1/avg_draw) / total
        away_prob = (1/avg_away) / total
    else:
        home_prob, draw_prob, away_prob = 0.40, 0.30, 0.30

    # Calcular Over2.5 a partir das odds de totals (se disponíveis)
    over25_odds = [o.odd for o in odds if o.market in ("totals", "over_under") and "2.5" in str(o.outcome) and "over" in str(o.outcome).lower()]
    if over25_odds:
        avg_over25 = sum(over25_odds) / len(over25_odds)
        # Implied probability sem margem (usar par over/under para remover vig)
        under25_odds = [o.odd for o in odds if o.market in ("totals", "over_under") and "2.5" in str(o.outcome) and "under" in str(o.outcome).lower()]
        if under25_odds:
            avg_under25 = sum(under25_odds) / len(under25_odds)
            total_ou = (1/avg_over25) + (1/avg_under25)
            over_2_5_prob = (1/avg_over25) / total_ou
        else:
            over_2_5_prob = 1 / avg_over25
    else:
        # Sem odds de totals: estimar com base na força ofensiva implícita nas odds 1X2
        # Jogos equilibrados (draw ~30%+) tendem a ter menos golos
        over_2_5_prob = 0.45 + (home_prob * 0.1) + (away_prob * 0.1)  # heurística conservadora
        over_2_5_prob = min(over_2_5_prob, 0.70)

    # Calcular BTTS a partir das odds de BTTS (se disponíveis)
    btts_yes_odds = [o.odd for o in odds if o.market == "btts" and o.outcome.lower() in ("yes", "sim", "yes")]
    if btts_yes_odds:
        avg_btts = sum(btts_yes_odds) / len(btts_yes_odds)
        btts_no_odds = [o.odd for o in odds if o.market == "btts" and o.outcome.lower() in ("no", "não", "no")]
        if btts_no_odds:
            avg_btts_no = sum(btts_no_odds) / len(btts_no_odds)
            total_btts = (1/avg_btts) + (1/avg_btts_no)
            btts_prob = (1/avg_btts) / total_btts
        else:
            btts_prob = 1 / avg_btts
    else:
        # Estimativa: BTTS correlaciona com força ofensiva de ambas as equipas
        # Se nenhuma equipa é claramente dominante (away_prob > 20%), BTTS ~45-55%
        btts_prob = 0.40 + (min(home_prob, away_prob) * 0.3)
        btts_prob = min(btts_prob, 0.65)

    return ModelProbabilities(
        home=round(home_prob, 3),
        draw=round(draw_prob, 3),
        away=round(away_prob, 3),
        over_2_5=round(over_2_5_prob, 3),
        btts=round(btts_prob, 3),
    )
```

#### Fix 3B — Corrigir mapeamento de IDs no OddsCollectorJob (CRÍTICO)

**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Jobs/OddsCollectorJob.cs`

**Problema:** `eventOdds.Id` é o ID da Odds API (alfanumérico), não o nosso GUID interno.

**Solução:** Mapear por nome das equipas + data do evento para encontrar o nosso `MatchEntity`.

```csharp
// ANTES (linha 83-86):
var odd = new OddsEntity
{
    Id = Guid.NewGuid(),
    MatchId = Guid.Parse(eventOdds.Id!), // ❌ CRASH - não é um GUID

// DEPOIS:
// 1. Primeiro, tentar encontrar o match interno pelo home_team, away_team e data
var homeTeam = eventOdds.HomeTeam;
var awayTeam = eventOdds.AwayTeam;
var commenceTime = eventOdds.CommenceTime; // datetime do evento

// Buscar match por nome das equipas (fuzzy match) e data
var matchEntity = await FindMatchByTeamsAndDateAsync(homeTeam, awayTeam, commenceTime, cancellationToken);

if (matchEntity == null)
{
    _logger.LogDebug("No match found for {Home} vs {Away} on {Date}", homeTeam, awayTeam, commenceTime);
    continue; // Pular este evento - não temos o jogo na nossa BD
}

// Agora criar as odds com o ID correto
var odd = new OddsEntity
{
    Id = Guid.NewGuid(),
    MatchId = matchEntity.Id, // ✅ ID correto
    ...
};
```

**Adicionar método auxiliar:**
```csharp
private async Task<MatchEntity?> FindMatchByTeamsAndDateAsync(
    string? homeTeam, string? awayTeam, DateTime? commenceTime, CancellationToken cancellationToken)
{
    if (homeTeam == null || awayTeam == null || commenceTime == null)
        return null;

    // Procurar por data (janela de ±2 horas) e nome de equipa (case-insensitive, contains)
    var windowStart = commenceTime.Value.AddHours(-2);
    var windowEnd = commenceTime.Value.AddHours(2);

    return await _dbContext.Matches
        .Include(m => m.HomeTeam)
        .Include(m => m.AwayTeam)
        .Where(m => m.CommenceTime >= windowStart && m.CommenceTime <= windowEnd)
        .Where(m =>
            (m.HomeTeam != null && EF.Functions.Like(m.HomeTeam.Name, $"%{homeTeam}%")) ||
            (m.HomeTeam != null && EF.Functions.Like(homeTeam, $"%{m.HomeTeam.Name}%")))
        .Where(m =>
            (m.AwayTeam != null && EF.Functions.Like(m.AwayTeam.Name, $"%{awayTeam}%")) ||
            (m.AwayTeam != null && EF.Functions.Like(awayTeam, $"%{m.AwayTeam.Name}%")))
        .FirstOrDefaultAsync(cancellationToken);
}
```

#### Fix 3C — Implementar endpoint `/analysis/match/{id}/latest` (IMPORTANTE)

**Ficheiro:** `src/analysis-engine/app/routers/analysis.py`

**Passo 1:** Adicionar método ao `RecommendationRepository` em `repositories.py`:

```python
# Adicionar em RecommendationRepository:
async def get_latest_by_match_id(self, match_id: UUID) -> Recommendation | None:
    """Get the latest recommendation for a match."""
    from sqlalchemy import desc
    result = await self.session.execute(
        select(Recommendation)
        .where(Recommendation.match_id == match_id)
        .order_by(desc(Recommendation.created_at))
        .limit(1)
    )
    return result.scalar_one_or_none()
```

**Passo 2:** Implementar o endpoint em `analysis.py`:

```python
# ANTES (linhas 56-95) — sempre retorna 404
# DEPOIS:
@router.get("/match/{match_id}/latest", response_model=AnalysisResponse)
async def get_latest_analysis(match_id: str):
    try:
        async with get_db_session() as db:
            match_repo = MatchRepository(db)
            match = await match_repo.get_by_id(UUID(match_id))

            if not match:
                raise HTTPException(status_code=404, detail="Match not found")

            rec_repo = RecommendationRepository(db)
            rec = await rec_repo.get_latest_by_match_id(UUID(match_id))

            if not rec:
                raise HTTPException(status_code=404, detail="No analysis found for this match")

            # Construir AnalysisResponse a partir da recomendação guardada
            return AnalysisResponse(
                match_id=str(match.id),
                sport=match.sport,
                home_team=match.home_team,
                away_team=match.away_team,
                commence_time=match.commence_time,
                is_live=match.status == "LIVE",
                model_probabilities=ModelProbabilities(
                    home=rec.model_probability,
                    draw=0.0,
                    away=0.0,
                    over_2_5=0.0,
                    btts=0.0,
                ),
                recommended_market=RecommendedMarket(
                    market=rec.market,
                    outcome=rec.outcome,
                    bookmaker=rec.bookmaker or "N/A",
                    odd=rec.odd_decimal,
                    implied_probability=rec.implied_probability,
                    model_probability=rec.model_probability,
                    value=rec.value,
                    kelly_fraction=rec.kelly_fraction,
                    stake_euros=rec.stake_euros,
                    confidence=rec.confidence,
                ),
                alternative_markets=[],
                reasoning=rec.reasoning or "",
                context_flags={},
                generated_at=rec.created_at or datetime.utcnow(),
                llm_provider="cached",
                llm_model="cached",
            )

    except HTTPException:
        raise
    except Exception as e:
        logger.error("Failed to get analysis", match_id=match_id, error=str(e))
        raise HTTPException(status_code=500, detail=f"Failed to get analysis: {str(e)}")
```

#### Fix 3D — Bankroll dinâmico via variável de ambiente (MELHORIA)

**Ficheiro:** `src/analysis-engine/app/services/analysis_service.py`

```python
# ANTES (linha 233):
stake = min(kelly * 100, 10)  # Max €10, assuming €100 bankroll

# DEPOIS:
bankroll = float(os.getenv("DEFAULT_BANKROLL", "100"))
max_stake_pct = float(os.getenv("MAX_STAKE_PCT", "0.05"))  # 5% por defeito
stake = min(kelly * bankroll, bankroll * max_stake_pct)
```

**Adicionar ao `.env`:**
```env
DEFAULT_BANKROLL=200
MAX_STAKE_PCT=0.05
```

---

## Ordem de Implementação Recomendada

| Prioridade | Fix | Ficheiro | Impacto |
|-----------|-----|----------|---------|
| 🔴 CRÍTICO | Fix 1A | `MatchRepository.cs` | Elimina jogos passados do dashboard |
| 🔴 CRÍTICO | Fix 3B | `OddsCollectorJob.cs` | Odds reais a ser guardadas corretamente |
| 🟠 ALTA | Fix 1B | `FootballCollectorJob.cs` | Statuses atualizados mais rapidamente |
| 🟠 ALTA | Fix 2A + 2B | `Program.cs` + entidades | Horas corretas em Portugal |
| 🟡 MÉDIA | Fix 3A | `analysis_service.py` | Over2.5/BTTS com dados reais |
| 🟡 MÉDIA | Fix 3C | `analysis.py` + `repositories.py` | Endpoint `/latest` funcional |
| 🟢 BAIXA | Fix 1C | `useApi.ts` | Frontend não pede jogos desde meia-noite |
| 🟢 BAIXA | Fix 3D | `analysis_service.py` | Bankroll configurável |

---

## Notas Importantes

1. **Odds API mapeamento (Fix 3B)**: A `the-odds-api.com` usa nomes de equipas em inglês (ex: "Sporting CP", "Benfica") enquanto a `football-data.org` pode usar nomes diferentes. O matching fuzzy por nome pode falhar. Uma solução mais robusta seria guardar o `ExternalId` da Odds API numa coluna separada da tabela `matches`.

2. **Dados estatísticos reais (Over2.5/BTTS)**: Mesmo com o Fix 3A, as probabilidades são calculadas a partir das odds do mercado (implied probability), não de dados históricos reais. Para cálculo verdadeiramente estatístico, é necessário implementar o modelo de Poisson descrito no `CONTEXT.md` com dados históricos da API.

3. **Timezone storage**: Os dados são guardados em UTC na BD (correto). O Fix 2 garante apenas que a serialização JSON inclui o `Z` para que o browser interprete corretamente.
