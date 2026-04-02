# Plano de Melhorias — MinhasApostas

> Baseado na análise do repositório em Março 2026.  
> Foco principal: fechar o loop de uso (dados → análise → utilizador → feedback).

---

## Resumo executivo

O projeto tem uma arquitetura sólida mas o **fluxo de uso está partido em 4 pontos críticos**: os jobs do Hangfire não têm schedule configurado (o sistema pode arrancar e nunca recolher dados), a dead letter queue engole erros silenciosamente, o frontend não recebe push de dados novos, e o utilizador não consegue fechar o ciclo de feedback (ganhou/perdeu a aposta). Estas melhorias estão organizadas em 3 fases sequenciais — cada fase deixa o sistema funcionalmente utilizável antes de passar à seguinte.

---

## Fase 1 — O sistema tem de funcionar do início ao fim

**Objetivo:** um utilizador consegue arrancar o sistema e ver recomendações no dashboard sem intervenção manual.

**Duração estimada:** 1–2 semanas

---

### 1.1 Configurar o schedule do Hangfire

**Problema:** não há nenhum ficheiro visível que defina quando os jobs de recolha de dados correm. O sistema pode arrancar e nunca disparar um único pedido às APIs externas.

**O que fazer:**

Criar (ou verificar que existe) um ficheiro de registo de jobs recorrentes no DataCollector, por exemplo em `DataCollector.Api/Jobs/JobScheduler.cs`:

```csharp
// Registar no Program.cs ou num IHostedService
RecurringJob.AddOrUpdate<FootballCollectorJob>(
    "football-upcoming",
    job => job.CollectUpcomingMatches(),
    "0 */6 * * *"   // a cada 6 horas
);

RecurringJob.AddOrUpdate<OddsCollectorJob>(
    "odds-refresh",
    job => job.RefreshOdds(),
    "*/30 * * * *"  // a cada 30 minutos
);
```

Documentar os schedules no `CONTEXT.md` ou num novo `SCHEDULES.md` para que fique claro o que corre quando.

**Ficheiros a criar/alterar:**
- `src/DataCollector/DataCollector.Api/Jobs/JobScheduler.cs` — novo
- `src/DataCollector/DataCollector.Api/Program.cs` — registar o scheduler
- `CONTEXT.md` — documentar frequências

---

### 1.2 Consumidor da dead letter queue com alerta

**Problema:** mensagens que falham (timeout de API, resposta inválida do LLM, parsing incorreto) caem na `dead.letters` e desaparecem silenciosamente. O utilizador não tem qualquer indicação de que se perderam dados.

**O que fazer:**

No Analysis Engine, adicionar um consumidor dedicado à fila `dead.letters` que pelo menos registe os erros com contexto suficiente para diagnóstico:

```python
# src/analysis-engine/app/consumers/dead_letter_consumer.py

async def consume_dead_letters(channel):
    async def on_message(message):
        async with message.process(ignore_processed=True):
            original_queue = message.headers.get("x-first-death-queue", "unknown")
            reason = message.headers.get("x-first-death-reason", "unknown")
            logger.error(
                "Dead letter received",
                original_queue=original_queue,
                reason=reason,
                body_preview=message.body[:200].decode(errors="replace"),
            )
            # Fase 2: enviar alerta Telegram se configurado

    await channel.basic_consume("dead.letters", on_message)
```

**Ficheiros a criar/alterar:**
- `src/analysis-engine/app/consumers/dead_letter_consumer.py` — novo
- `src/analysis-engine/app/main.py` — registar o consumidor no arranque

---

### 1.3 Remover a API key hardcoded no docker-compose

**Problema:** `VITE_API_KEY=f4da4685b2...` está em texto claro no `docker-compose.yml`, versionado no repositório.

**O que fazer:**

```yaml
# docker-compose.yml — antes
VITE_API_KEY: ${FRONTEND_API_KEY:-f4da4685b2039b4c4eec35c51a4147341ba977210041a12062e3d2bafd1cee96}

# docker-compose.yml — depois
VITE_API_KEY: ${FRONTEND_API_KEY}
```

```bash
# .env.example — adicionar com instrução
FRONTEND_API_KEY=  # gerar com: openssl rand -hex 32
```

**Ficheiros a alterar:**
- `docker-compose.yml`
- `.env.example`

---

### 1.4 Remover a política HA do RabbitMQ (single-node)

**Problema:** `definitions.json` tem `ha-mode: all` que é configuração de cluster. Com um único nó, gera warnings no arranque e pode confundir o diagnóstico.

**O que fazer:**

Remover o bloco `policies` do `definitions.json`:

```json
// Apagar completamente este bloco:
"policies": [
  {
    "name": "ha-all",
    "definition": { "ha-mode": "all", "ha-sync-mode": "automatic" },
    ...
  }
]
```

Se no futuro for necessário clustering, reintroduzir com documentação explícita.

**Ficheiros a alterar:**
- `definitions.json`

---

## Fase 2 — O utilizador vê o que está a acontecer

**Objetivo:** o dashboard reflecte o estado real do sistema em tempo próximo do real, com indicação de progresso e erros.

**Duração estimada:** 2–3 semanas

---

### 2.1 Push de dados para o frontend via SSE

**Problema:** o frontend faz polling REST. As odds mudam frequentemente — um utilizador pode estar a ver dados com 5–10 minutos de atraso sem saber.

**O que fazer:**

No Analysis Engine (FastAPI), adicionar um endpoint SSE que o frontend subscreve:

```python
# src/analysis-engine/app/api/events.py

from fastapi import APIRouter
from sse_starlette.sse import EventSourceResponse
import asyncio, json

router = APIRouter()

@router.get("/stream/recommendations")
async def stream_recommendations(request: Request):
    async def event_generator():
        pubsub = redis_client.pubsub()
        await pubsub.subscribe("recommendations:new")
        async for message in pubsub.listen():
            if await request.is_disconnected():
                break
            if message["type"] == "message":
                yield {"data": message["data"]}
    return EventSourceResponse(event_generator())
```

No frontend React, substituir o polling por um `EventSource`:

```typescript
// src/frontend/src/hooks/useRecommendations.ts

useEffect(() => {
  const es = new EventSource(`${API_URL}/stream/recommendations`);
  es.onmessage = (e) => {
    const rec = JSON.parse(e.data);
    setRecommendations(prev => [rec, ...prev]);
  };
  return () => es.close();
}, []);
```

O Redis já está na infra — usar o pub/sub nativo como canal de notificação entre o Analysis Engine e o frontend.

**Ficheiros a criar/alterar:**
- `src/analysis-engine/app/api/events.py` — novo
- `src/analysis-engine/requirements.txt` — adicionar `sse-starlette`
- `src/frontend/src/hooks/useRecommendations.ts` — alterar

---

### 2.2 Estado de processamento visível no dashboard

**Problema:** o fluxo recolha → análise → recomendação é assíncrono e pode demorar minutos. O utilizador não sabe se o sistema está a trabalhar ou parado.

**O que fazer:**

Adicionar um endpoint de estado ao DataCollector:

```csharp
// GET /api/system/status
public record SystemStatusDto(
    int JobsRunningNow,
    DateTimeOffset? LastCollectionRun,
    int MatchesCollectedToday,
    int QueueDepth,
    string OverallStatus  // "idle" | "collecting" | "analysing" | "error"
);
```

No dashboard React, mostrar uma barra de estado persistente:

```
● A recolher odds...   Último update: há 4 min   Jogos hoje: 23   Recomendações: 7
```

**Ficheiros a criar/alterar:**
- `src/DataCollector/DataCollector.Api/Controllers/SystemController.cs` — novo
- `src/frontend/src/components/StatusBar.tsx` — novo

---

### 2.3 Clarificar o papel do Redis — cache de respostas LLM

**Problema:** o Redis está configurado mas não é claro o que cacheia. Chamadas ao LLM para o mesmo jogo a cada atualização de odds são caras e lentas.

**O que fazer:**

No Analysis Engine, fazer cache das análises LLM por `match_id + odds_snapshot_hash`:

```python
# src/analysis-engine/app/services/llm_service.py

async def analyse_match(match_id: str, odds: dict) -> str:
    cache_key = f"llm:{match_id}:{hash_odds(odds)}"
    cached = await redis_client.get(cache_key)
    if cached:
        return cached.decode()

    result = await call_llm(match_id, odds)
    await redis_client.setex(cache_key, 3600, result)  # TTL 1h
    return result
```

Documentar no `CONTEXT.md` o que é cacheado e durante quanto tempo.

**Ficheiros a alterar:**
- `src/analysis-engine/app/services/llm_service.py`
- `CONTEXT.md`

---

## Fase 3 — Fechar o ciclo de feedback

**Objetivo:** o utilizador consegue registar o resultado das apostas e o sistema usa esse histórico para melhorar e mostrar métricas de acerto.

**Duração estimada:** 2–3 semanas

---

### 3.1 Ciclo de vida das recomendações (PENDING → WON/LOST/VOID)

**Problema:** o schema tem `status: 'PENDING'` mas não há mecanismo para o utilizador registar o resultado, nem para o sistema verificar automaticamente.

**O que fazer:**

Migração da base de dados:

```sql
-- db/migrations/V003__recommendation_outcome.sql

ALTER TABLE recommendations
  ADD COLUMN outcome VARCHAR(10)  -- 'WON' | 'LOST' | 'VOID' | NULL
  CHECK (outcome IN ('WON', 'LOST', 'VOID')),
  ADD COLUMN outcome_recorded_at TIMESTAMPTZ,
  ADD COLUMN outcome_source VARCHAR(20) DEFAULT 'manual';
  -- 'manual' = utilizador registou | 'auto' = verificado via API de resultados
```

Endpoint para registar resultado manualmente:

```csharp
// PATCH /api/recommendations/{id}/outcome
public record RecordOutcomeRequest(string Outcome); // "WON" | "LOST" | "VOID"
```

No frontend, botões por recomendação:

```
[✓ Ganhou]  [✗ Perdeu]  [— Anulada]
```

**Ficheiros a criar/alterar:**
- `db/migrations/V003__recommendation_outcome.sql` — novo
- `src/DataCollector/DataCollector.Api/Controllers/RecommendationsController.cs` — alterar
- `src/frontend/src/components/RecommendationCard.tsx` — alterar

---

### 3.2 Dashboard de performance da IA

**Problema:** não há nenhuma forma de o utilizador avaliar se as recomendações da IA estão a ser rentáveis ao longo do tempo.

**O que fazer:**

Adicionar uma vista de métricas com os indicadores fundamentais de apostas:

```
Período: Último mês  ▾

Taxa de acerto:     61.3%   (49/80 apostas)
ROI:               +8.2%
Yield:             +4.1%
Maior série positiva:  7
Maior série negativa:  4
Lucro simulado (Kelly): +€127.40
```

Estas métricas calculam-se directamente da tabela `recommendations` com `outcome IS NOT NULL`.

Query base:

```sql
SELECT
  COUNT(*) FILTER (WHERE outcome = 'WON') AS wins,
  COUNT(*) FILTER (WHERE outcome = 'LOST') AS losses,
  SUM(stake_euros) FILTER (WHERE outcome = 'WON') * AVG(odd_decimal) - SUM(stake_euros) AS profit
FROM recommendations
WHERE outcome IS NOT NULL
  AND created_at > NOW() - INTERVAL '30 days';
```

**Ficheiros a criar/alterar:**
- `src/DataCollector/DataCollector.Api/Controllers/StatsController.cs` — novo
- `src/frontend/src/pages/PerformancePage.tsx` — novo

---

### 3.3 Alertas Telegram quando sai uma recomendação de alta confiança

**Problema:** o `.env.example` tem `TELEGRAM_BOT_TOKEN` e `TELEGRAM_CHAT_ID` mas não há código que os use. O utilizador tem de estar com o dashboard aberto para ver novas recomendações.

**O que fazer:**

No Analysis Engine, enviar mensagem quando `confidence >= 7`:

```python
# src/analysis-engine/app/services/alert_service.py

async def send_telegram_alert(rec: Recommendation):
    if not settings.telegram_bot_token:
        return
    if rec.confidence < 7:
        return

    text = (
        f"🎯 *Nova recomendação — confiança {rec.confidence}/10*\n\n"
        f"*{rec.match_home} vs {rec.match_away}*\n"
        f"Mercado: {rec.market} → {rec.outcome}\n"
        f"Odd: {rec.odd_decimal:.2f} | Value: +{rec.value:.1%}\n"
        f"Kelly: {rec.kelly_fraction:.1%} | Stake: €{rec.stake_euros:.2f}\n\n"
        f"_{rec.reasoning[:200]}..._"
    )
    await httpx_client.post(
        f"https://api.telegram.org/bot{settings.telegram_bot_token}/sendMessage",
        json={"chat_id": settings.telegram_chat_id, "text": text, "parse_mode": "Markdown"},
    )
```

**Ficheiros a criar/alterar:**
- `src/analysis-engine/app/services/alert_service.py` — novo
- `src/analysis-engine/app/consumers/recommendations_consumer.py` — chamar o alert_service

---

## Tabela de prioridades

| # | Melhoria | Fase | Impacto | Esforço | Ficheiros principais |
|---|----------|------|---------|---------|----------------------|
| 1 | Configurar schedule Hangfire | 1 | 🔴 Crítico | Baixo | `JobScheduler.cs` |
| 2 | Consumidor dead letter queue | 1 | 🔴 Crítico | Baixo | `dead_letter_consumer.py` |
| 3 | Remover API key hardcoded | 1 | 🔴 Segurança | Mínimo | `docker-compose.yml` |
| 4 | Remover política HA rabbit | 1 | 🟡 Limpeza | Mínimo | `definitions.json` |
| 5 | SSE para push de dados | 2 | 🔴 UX | Médio | `events.py`, `useRecommendations.ts` |
| 6 | Barra de estado do sistema | 2 | 🟡 UX | Médio | `SystemController.cs`, `StatusBar.tsx` |
| 7 | Cache LLM no Redis | 2 | 🟡 Custo/perf | Baixo | `llm_service.py` |
| 8 | Ciclo de vida recomendações | 3 | 🔴 Funcional | Médio | migração SQL, controller, card |
| 9 | Dashboard de performance | 3 | 🟡 Valor | Médio | `StatsController.cs`, `PerformancePage.tsx` |
| 10 | Alertas Telegram | 3 | 🟢 Conforto | Baixo | `alert_service.py` |

---

## O que não está neste plano (intencionalmente)

As seguintes melhorias identificadas na análise foram excluídas deste plano por serem de infraestrutura e não afectarem directamente o fluxo de uso:

- CI/CD com GitHub Actions
- Migração para Flyway/Alembic
- Downgrade de versões (.NET 10 → 9, Python 3.14 → 3.12)
- Estratégia de branches

Estas devem ser tratadas num plano de infra separado, em paralelo com a Fase 2.

---

*Gerado a partir da análise do repositório ENFernandes/MinhasApostas — Março 2026*
