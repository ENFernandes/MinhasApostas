# Plano de Implementação - Sistema Funcional

> Documento detalhado do que falta implementar para tornar o sistema 100% funcional com dados reais.

---

## 1. Data Collector (C#) - Estimativa: 45 min

### 1.1 Agendamento de Jobs Hangfire (15 min)
**Problema:** Os jobs existem mas não são agendados automaticamente.

**Ficheiros a modificar:**
- `src/DataCollector/DataCollector.Api/Program.cs`
- `src/DataCollector/DataCollector.Api/Extensions/ServiceCollectionExtensions.cs`

**Implementação:**
```csharp
// Adicionar no Program.cs:
builder.Services.ScheduleHangfireJobs(
    typeof(Program).Assembly, 
    typeof(SportsBettingDbContext).Assembly);

// Implementar ExecuteJob() para usar IServiceProvider:
public static void ExecuteJob(Type jobType)
{
    // Resolving via DI container
}
```

### 1.2 Corrigir FootballCollectorJob (20 min)
**Problema:** O job não preenche os IDs das equipas e não publica na fila.

**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Jobs/FootballCollectorJob.cs`

**Alterações necessárias:**
- [ ] Criar/Atualizar entidades Team antes de criar Match
- [ ] Preencher HomeId e AwayId no MatchEntity
- [ ] Publicar mensagem `football.match.new` na fila RabbitMQ
- [ ] Adicionar logging estruturado

**Código a adicionar:**
```csharp
// Criar ou atualizar equipamentos
var homeTeam = await _teamRepository.UpsertAsync(matchDto.HomeTeam);
var awayTeam = await _teamRepository.UpsertAsync(matchDto.AwayTeam);

var match = new MatchEntity
{
    ExternalId = matchDto.Id.ToString(),
    HomeId = homeTeam.Id,  // <-- Agora preenchido
    AwayId = awayTeam.Id,  // <-- Agora preenchido
    Sport = "football",
    CommenceTime = matchDto.UtcDate.Value,
    Status = MapStatus(matchDto.Status),
};

// Publicar na fila
await _messageQueue.PublishAsync("sports.events", "football.match.new", match);
```

### 1.3 Implementar OddsCollectorJob (10 min)
**Ficheiro:** `src/DataCollector/DataCollector.Infrastructure/Jobs/OddsCollectorJob.cs`

**Funcionalidade:**
- Buscar odds de várias casas de apostas via the-odds-api.com
- Calcular probabilidade implícita (1/odd)
- Guardar na tabela Odds
- Publicar `odds.updated` na fila

---

## 2. Analysis Engine (Python) - Estimativa: 45 min

### 2.1 Implementar Consumers RabbitMQ (20 min)
**Ficheiros:**
- `src/analysis-engine/app/consumers/match_consumer.py`
- `src/analysis-engine/app/consumers/odds_consumer.py`

**Implementação:**
```python
# match_consumer.py - handle_football_match()
async def handle_football_match(match_data: dict):
    # 1. Guardar na BD
    await save_match(match_data)
    
    # 2. Buscar estatísticas históricas
    stats = await get_team_stats(match_data['home_id'], match_data['away_id'])
    
    # 3. Buscar odds atuais
    odds = await get_odds_for_match(match_data['id'])
    
    # 4. Enviar para análise LLM
    analysis = await analyze_match(match_data, stats, odds)
    
    # 5. Guardar recomendação
    await save_recommendation(analysis)
    
    # 6. Publicar resultado
    await publish_recommendation(analysis)
```

### 2.2 Implementar Lógica de Análise (20 min)
**Ficheiros:**
- `src/analysis-engine/app/services/ai/prompt_builder.py`
- `src/analysis-engine/app/services/odds/calculator.py`
- `src/analysis-engine/app/services/odds/kelly.py`

**Algoritmos a implementar:**

1. **Probabilidade Implícita:**
   ```python
   implied_prob = 1 / decimal_odd
   ```

2. **Cálculo de Value:**
   ```python
   value = model_probability - implied_prob
   ```

3. **Critério de Kelly:**
   ```python
   kelly_fraction = (model_prob * odd - 1) / (odd - 1)
   stake = bankroll * kelly_fraction * kelly_multiplier
   ```

4. **Prompt para LLM:**
   ```
   Analise esta partida de futebol:
   
   {home_team} vs {away_team}
   
   Estatísticas (últimos 5 jogos):
   - {home_team}: {home_form}
   - {away_team}: {away_form}
   
   Odds disponíveis:
   {odds_table}
   
   Calcula:
   1. Probabilidade real de cada resultado (1X2)
   2. Identifica valores positivos (value bets)
   3. Sugere stake ótima usando Kelly Criterion
   4. Confiança na recomendação (1-10)
   ```

### 2.3 API Endpoints (5 min)
**Ficheiro:** `src/analysis-engine/app/routers/analysis.py`

**Endpoints necessários:**
- `POST /analysis/match/{match_id}` - Analisar partida específica
- `GET /analysis/match/{match_id}/latest` - Última análise
- `GET /recommendations` - Todas as recomendações ativas

---

## 3. Frontend (React) - Estimativa: 30 min

### 3.1 Atualizar Páginas para usar API Real (20 min)
**Ficheiros:**
- `src/frontend/src/pages/dashboard/index.tsx`
- `src/frontend/src/pages/history/index.tsx`
- `src/frontend/src/hooks/useApi.ts`

**Alterações:**
```typescript
// Remover dados mockados
// const mockMatches = [...] <-- APAGAR

// Usar dados da API
const { data: matches, isLoading } = useMatches()

// Mostrar loading state
if (isLoading) return <LoadingSpinner />

// Mostrar dados reais
{matches?.map(match => (
  <MatchCard key={match.id} match={match} />
))}
```

### 3.2 Configurar Proxy/Vite (10 min)
**Ficheiro:** `src/frontend/vite.config.ts`

**Adicionar proxy para APIs:**
```typescript
server: {
  proxy: {
    '/api': 'http://localhost:8080',
    '/analysis': 'http://localhost:8090',
  }
}
```

---

## 4. Configuração e Infraestrutura - Estimativa: 20 min

### 4.1 API Keys (5 min)
**Ficheiro:** `.env`

**Obter keys em:**
- https://www.football-data.org/ (grátis, 10 req/min)
- https://api-tennis.com/ (grátis, 200 req/dia)
- https://the-odds-api.com/ (grátis, 500 créditos/mês)

```env
FOOTBALL_DATA_API_KEY=sua_key_aqui
TENNIS_API_KEY=sua_key_aqui
ODDS_API_KEY=sua_key_aqui
```

### 4.2 Migrações Base de Dados (10 min)
**Verificar/criar:** `db/migrations/001_initial_schema.sql`

**Tabelas necessárias:**
- teams (id, external_id, name, sport, country)
- matches (id, external_id, sport, home_id, away_id, ...)
- odds (id, match_id, bookmaker, market, outcome, odd, ...)
- recommendations (id, match_id, market, outcome, value, stake, ...)
- bets (id, recommendation_id, stake, result, profit_loss, ...)

### 4.3 Variáveis de Ambiente Docker (5 min)
**Ficheiro:** `docker-compose.yml`

**Verificar se todas as vars estão configuradas:**
```yaml
environment:
  - FOOTBALL_DATA_API_KEY=${FOOTBALL_DATA_API_KEY}
  - TENNIS_API_KEY=${TENNIS_API_KEY}
  - ODDS_API_KEY=${ODDS_API_KEY}
```

---

## 5. Testes e Validação - Estimativa: 15 min

### 5.1 Testar Fluxo Completo
```bash
# 1. Reiniciar containers
docker-compose down && docker-compose up -d

# 2. Verificar logs
docker-compose logs -f data-collector
docker-compose logs -f analysis-engine

# 3. Testar endpoints
curl http://localhost:8080/api/matches/upcoming
curl http://localhost:8090/health

# 4. Verificar frontend
open http://localhost:3000
```

### 5.2 Verificar Fila RabbitMQ
- Aceder http://localhost:15672
- Verificar se mensagens estão a ser publicadas/consumidas

---

## Resumo de Tempo Total

| Componente | Tempo Estimado |
|------------|----------------|
| Data Collector | 45 min |
| Analysis Engine | 45 min |
| Frontend | 30 min |
| Configuração | 20 min |
| Testes | 15 min |
| **TOTAL** | **~2.5 horas** |

---

## Prioridade de Implementação

1. **Alta:** Agendamento de Jobs + ExecuteJob() (Data Collector funcional)
2. **Alta:** Corrigir FootballCollectorJob com publicação na fila
3. **Alta:** Implementar MatchConsumer no Analysis Engine
4. **Média:** Lógica de análise com LLM
5. **Média:** Frontend com dados reais
6. **Baixa:** OddsCollectorJob e TennisCollectorJob

---

## Próximos Passos

Queres que eu comece a implementação? Posso:

1. **Implementar tudo de uma vez** (~2.5 horas)
2. **Fasear:** Primeiro só coleta de dados, depois análise
3. **Implementar apenas uma parte específica**

Qual a tua preferência?
