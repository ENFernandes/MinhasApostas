# Plano de Implementação — Estatísticas Reais e Dados Históricos

> Data: 2026-03-30
> Objectivo: dotar o sistema de dados estatísticos reais para que o modelo LLM
> consiga fazer análises fundamentadas em vez de trabalhar com implied probability mockada.
>
> Estado de partida: M0–M8 concluídos, fixes críticos aplicados, M9 adiado.
> Sistema funcional mas a usar implied probability das odds como proxy de probabilidade real.

---

## Diagnóstico — o que falta exactamente

O LLM recebe actualmente este contexto para analisar um jogo:

```
Benfica vs Porto
Probabilidades modelo: home=0.50, draw=0.30, away=0.20 (← implied probability das odds, não modelo real)
Over2.5: 0.48  BTTS: 0.45  (← calculados das odds, não de dados históricos)
Forma recente: desconhecida
H2H: desconhecido
xG médio: desconhecido
Lesões: desconhecidas
```

O que queremos que o LLM receba:

```
Benfica vs Porto — Liga Portugal, Jornada 28
Probabilidades Poisson (10 jogos, decaimento 0.9):
  lambda_home=1.82, lambda_away=0.94
  home=0.58, draw=0.23, away=0.19
  Over2.5=0.63, BTTS=0.54
Forma recente Benfica (casa, últimos 5): V V E V V — 2.4 golos/jogo, 0.8 sofridos
Forma recente Porto (fora, últimos 5): V D V E D — 1.1 golos/jogo, 1.4 sofridos
H2H últimos 5: Benfica 3V 1E 1D, média 2.8 golos/jogo
xG médio Benfica (casa): 1.94  xG sofrido: 0.71
Lesões Porto: Evanilson (avançado) — duvidoso
Lineups confirmadas: disponíveis 60 min antes
```

A diferença é o que separa uma recomendação genérica de uma análise com valor real.

---

## Gaps identificados — 5 problemas concretos

| # | Gap | Impacto | Ficheiros afectados |
|---|-----|---------|---------------------|
| G1 | Tabelas históricas vazias (`historical_matches`, `player_elo_history`) | Alto | `db/seeds/load_historical.py` |
| G2 | `analysis_service.py` usa implied probability em vez de Poisson real | Alto | `app/services/analysis_service.py` |
| G3 | `match_stats.xg` nunca preenchido — api-football não integrado | Alto | Novo job C# + novo client Refit |
| G4 | Forma recente e H2H nunca enviados ao LLM | Médio | `app/services/stats/form.py` |
| G5 | Lineups e lesões nunca recolhidas | Médio | Novo job C# (api-football) |

---

## Sequência de implementação — 4 fases

A ordem respeita as dependências: os dados históricos têm de existir antes
de o Poisson poder ser calculado; o Poisson tem de funcionar antes de o LLM
receber contexto útil; o api-football enriquece depois de a base estar sólida.

---

## Fase 1 — Dados históricos (execução única, ~20 min de trabalho)

> Objectivo: popular `historical_matches` e `player_elo_history` para que
> o Poisson tenha base histórica para equipas com poucos jogos na época actual.

### 1.1 — Verificar que a migração V010 está aplicada

```bash
make shell-db
\dt historical_*
# Deve mostrar: historical_matches, historical_events, player_elo_history
# + views: latest_player_elo, team_historical_averages
```

Se não existir → aplicar:
```bash
make migrate
# ou manualmente:
psql -U sba_user -d sportsbetting -f db/migrations/V010__create_historical_tables.sql
```

### 1.2 — Instalar dependências Python do script de seed

```bash
cd src/analysis-engine
pip install statsbombpy tqdm pandas sqlalchemy psycopg2-binary
# ou com pyproject.toml:
# Adicionar ao [project.dependencies]:
# "statsbombpy>=1.1.0",
# "tqdm>=4.66.0",
```

### 1.3 — Clonar repos de dados históricos

```bash
mkdir -p data/historical

# Resultados históricos de 40+ ligas (1993–hoje)
# Não precisa de clone — acesso directo por CSV via URL

# ELO de ténis ATP (1968–hoje)
git clone https://github.com/JeffSackmann/tennis_atp data/historical/tennis_atp

# ELO de ténis WTA (1968–hoje)
git clone https://github.com/JeffSackmann/tennis_wta data/historical/tennis_wta

# StatsBomb Open Data (eventos de remate com xG — opcional, pesado ~2GB)
# git clone https://github.com/statsbomb/open-data data/historical/statsbomb
```

### 1.4 — Executar script de carregamento

```bash
# Carregar futebol histórico (football-data.co.uk CSVs via URL)
# Tempo estimado: 8-12 minutos (40 ligas × 8 épocas)
python db/seeds/load_historical.py --sport football

# Carregar ELO de ténis (Sackmann CSVs)
# Tempo estimado: 5-8 minutos
python db/seeds/load_historical.py --sport tennis

# StatsBomb (opcional — só se quiser treinar modelo xG)
# Tempo estimado: 15-25 minutos
# python db/seeds/load_historical.py --sport statsbomb
```

### 1.5 — Verificar carregamento

```sql
-- No shell da BD (make shell-db):
SELECT COUNT(*) FROM historical_matches;
-- Esperado: ~500.000 a 800.000 linhas

SELECT league_code, COUNT(*) FROM historical_matches
GROUP BY league_code ORDER BY COUNT(*) DESC LIMIT 10;
-- Deve mostrar E0 (Premier League), SP1 (La Liga), D1 (Bundesliga), etc.

SELECT COUNT(*) FROM player_elo_history;
-- Esperado: ~2.000.000 a 4.000.000 linhas (ATP + WTA desde 1968)

SELECT * FROM latest_player_elo
WHERE player_name ILIKE '%Djokovic%';
-- Deve mostrar ELO actual por superfície
```

**Critério de conclusão da Fase 1:**
- `historical_matches` com ≥ 100.000 linhas
- `player_elo_history` com ≥ 500.000 linhas
- View `latest_player_elo` a devolver resultados para jogadores conhecidos

---

## Fase 2 — Modelo Poisson real no Python Engine (~2h)

> Objectivo: substituir o cálculo de probabilidades baseado em implied probability
> pelo modelo de Poisson bivariado com dados históricos reais.

### 2.1 — Copiar ficheiro `poisson.py` para o repositório

**Ficheiro a criar:** `src/analysis-engine/app/services/stats/poisson.py`

O ficheiro já está gerado nesta sessão (ver outputs). Copiar para o repositório.

**Dependências Python a adicionar ao `pyproject.toml`:**
```toml
[project.dependencies]
scipy = ">=1.13.0"
numpy = ">=1.26.0"
```

### 2.2 — Criar `form.py` — forma recente e H2H

**Ficheiro a criar:** `src/analysis-engine/app/services/stats/form.py`

```python
# Funções a implementar:

async def calcular_forma_recente(
    db: AsyncSession,
    team_name: str,
    is_home: bool,
    n_jogos: int = 5,
) -> FormData:
    """
    Últimos N jogos: resultado, golos marcados/sofridos, xG se disponível.
    Fonte primária: tabela matches (época actual)
    Fallback: historical_matches
    """
    ...

async def calcular_h2h(
    db: AsyncSession,
    home_team: str,
    away_team: str,
    n_jogos: int = 5,
) -> H2HData:
    """
    Últimos N confrontos directos entre as duas equipas.
    Fonte: historical_matches + matches
    """
    ...

async def calcular_elo_tenista(
    db: AsyncSession,
    player_name: str,
    surface: str,
) -> float:
    """
    ELO actual do jogador na superfície especificada.
    Fonte: view latest_player_elo
    """
    ...
```

**Schema de output (Pydantic):**
```python
class FormData(BaseModel):
    team: str
    venue: str              # "home" | "away"
    games: list[GameResult]
    avg_goals_scored: float
    avg_goals_conceded: float
    avg_xg_scored: float | None
    wins: int
    draws: int
    losses: int
    form_string: str        # ex: "VVEDV"

class H2HData(BaseModel):
    home_team: str
    away_team: str
    games: list[GameResult]
    home_wins: int
    draws: int
    away_wins: int
    avg_total_goals: float
    last_meeting: GameResult | None
```

### 2.3 — Actualizar `analysis_service.py`

**Ficheiro a modificar:** `src/analysis-engine/app/services/analysis_service.py`

Substituir o método `_calculate_probabilities` existente:

```python
# REMOVER: cálculo baseado em implied probability (Fix 3A anterior)
# ADICIONAR: chamada ao modelo Poisson real

from app.services.stats.poisson import calcular_probabilidades_poisson
from app.services.stats.form import calcular_forma_recente, calcular_h2h

async def _calculate_probabilities(
    self,
    match: MatchData,
    odds: list,
    db: AsyncSession,
) -> ModelProbabilities:

    # 1. Tentar Poisson com dados históricos
    try:
        poisson_result = await calcular_probabilidades_poisson(
            db=db,
            home_team=match.home_team,
            away_team=match.away_team,
            league_code=match.league_code,
        )
        return ModelProbabilities(
            home=poisson_result.home,
            draw=poisson_result.draw,
            away=poisson_result.away,
            over_2_5=poisson_result.over_2_5,
            btts=poisson_result.btts,
            data_source=poisson_result.data_source,
        )
    except Exception as e:
        logger.warning("Poisson falhou, a usar implied probability", error=str(e))

    # 2. Fallback: implied probability (comportamento actual)
    return self._calculate_from_implied_probability(odds)
```

**Actualizar `_build_context` para incluir forma e H2H:**

```python
async def _build_context(self, match, db) -> dict:
    home_form = await calcular_forma_recente(db, match.home_team, is_home=True)
    away_form = await calcular_forma_recente(db, match.away_team, is_home=False)
    h2h = await calcular_h2h(db, match.home_team, match.away_team)

    return {
        "home_form": home_form.model_dump(),
        "away_form": away_form.model_dump(),
        "h2h": h2h.model_dump(),
    }
```

### 2.4 — Actualizar o prompt do LLM

**Ficheiro a modificar:** `src/analysis-engine/app/prompts/match_analysis.txt`

Adicionar secções de forma e H2H ao prompt:

```
Forma recente {home_team} (casa, últimos 5):
{home_form_summary}
Médias: {home_avg_scored} golos marcados, {home_avg_conceded} sofridos

Forma recente {away_team} (fora, últimos 5):
{away_form_summary}
Médias: {away_avg_scored} golos marcados, {away_avg_conceded} sofridos

Histórico H2H (últimos 5 encontros):
{h2h_summary}
```

**Critério de conclusão da Fase 2:**
- Endpoint `/analysis/match/{id}` retorna `data_source: "match_stats_xg"` ou `"historical_matches"` (não `"implied_probability"`)
- Campos `home_form` e `h2h` presentes no JSON de output
- `pytest tests/stats/` passa 100%

---

## Fase 3 — API-Football: xG e lineups em tempo real (~3h)

> Objectivo: recolher estatísticas detalhadas (xG, remates, posse) e lineups
> via api-football para complementar os dados históricos com informação em tempo real.
>
> Pré-requisito: API key de api-football (https://dashboard.api-football.com/register)
> Rate limit: 100 req/dia no plano gratuito — gerir com cuidado.

### 3.1 — Registar API key

```bash
# Adicionar ao .env:
API_FOOTBALL_KEY=your_key_here
```

```yaml
# Adicionar ao docker-compose.yml no serviço data-collector:
ApiKeys__ApiFootball: ${API_FOOTBALL_KEY}
```

### 3.2 — Criar `IApiFootballClient.cs`

**Ficheiro a criar:** `src/DataCollector/DataCollector.Infrastructure/Clients/IApiFootballClient.cs`

O ficheiro já está gerado nesta sessão (ver outputs). Copiar para o repositório.

**Registar no `ServiceCollectionExtensions.cs`:**

```csharp
// O scanner de Reflection já regista automaticamente qualquer IApiClient.
// Verificar que IApiFootballClient implementa IApiClient.
// Adicionar base URL no appsettings:
services.AddRefitClient<IApiFootballClient>()
    .ConfigureHttpClient(c => {
        c.BaseAddress = new Uri("https://v3.football.api-sports.io");
        c.DefaultRequestHeaders.Add("x-apisports-key", apiFootballKey);
    });
```

### 3.3 — Criar `FootballStatsDetailJob.cs`

**Ficheiro a criar:** `src/DataCollector/DataCollector.Infrastructure/Jobs/FootballStatsDetailJob.cs`

**Lógica do job:**

```csharp
// Corre: de hora em hora
// CronExpression: "0 * * * *"

// 1. Buscar jogos terminados nas últimas 3h
var recentlyFinished = await _matchRepo.GetFinishedSinceAsync(DateTime.UtcNow.AddHours(-3));

// 2. Para cada jogo, verificar se já tem match_stats preenchido
foreach (var match in recentlyFinished)
{
    if (await _statsRepo.ExistsForMatchAsync(match.Id)) continue;

    // 3. Buscar external_id do api-football (precisa de mapeamento)
    var apiFootballId = await MapToApiFootballIdAsync(match);
    if (apiFootballId == null) continue;

    // 4. Buscar estatísticas detalhadas
    var stats = await _apiFootballClient.GetFixtureStatisticsAsync(apiFootballId.Value);

    // 5. Extrair xG e outras métricas
    foreach (var teamStats in stats.Response)
    {
        var xg = teamStats.Statistics
            .FirstOrDefault(s => s.Type == "expected_goals")?.ValueAsDecimal;
        var shots = teamStats.Statistics
            .FirstOrDefault(s => s.Type == "Total Shots")?.ValueAsDecimal;
        var possession = teamStats.Statistics
            .FirstOrDefault(s => s.Type == "Ball Possession")?.ValueAsString;

        await _statsRepo.UpsertAsync(new MatchStatsEntity {
            MatchId = match.Id,
            TeamId = await ResolveTeamIdAsync(teamStats.Team.Name),
            Xg = xg,
            Shots = (int?)shots,
            Possession = ParsePossession(possession),
            // ...
        });
    }
}
```

**Critério de uso dos 100 req/dia:**

| Job | Frequência | Req/chamada | Req/dia estimado |
|-----|-----------|-------------|-----------------|
| `FootballStatsDetailJob` | Hora em hora | ~5 jogos × 1 req | ~20 req |
| `FootballLineupsJob` | 2x por jogo (D-1 e D-0) | ~5 jogos × 1 req | ~10 req |
| `FootballInjuriesJob` | Diário por competição | ~7 ligas × 1 req | ~7 req |
| **Total estimado** | | | **~37 req/dia** |

Margem segura de 63 req/dia para picos.

### 3.4 — Criar `FootballLineupsJob.cs`

**Ficheiro a criar:** `src/DataCollector/DataCollector.Infrastructure/Jobs/FootballLineupsJob.cs`

```csharp
// Corre: D-1 (às 20h) e D-0 (às 12h e às 17h)
// CronExpression: "0 20 * * *" + "0 12,17 * * *"

// Buscar jogos das próximas 24h sem lineups confirmadas
// Para cada jogo com apiFootballId conhecido:
//   GET /fixtures/lineups?fixture={id}
//   Se response não vazio → guardar em nova tabela match_lineups
//   Publicar evento "lineups.confirmed" no RabbitMQ
```

**Nova tabela necessária:**

```sql
-- Adicionar a nova migração: V011__create_match_lineups.sql
CREATE TABLE IF NOT EXISTS match_lineups (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id        UUID NOT NULL REFERENCES matches(id),
    team_id         UUID NOT NULL REFERENCES teams(id),
    formation       VARCHAR(10),          -- "4-3-3", "4-4-2", etc.
    players         JSONB NOT NULL,       -- array de {name, position, number}
    substitutes     JSONB NOT NULL,
    confirmed_at    TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(match_id, team_id)
);
```

### 3.5 — Actualizar `analysis_service.py` para usar lineups e lesões

```python
async def _build_context(self, match, db) -> dict:
    # ... forma e H2H (Fase 2) ...

    # Adicionar lineups se disponíveis
    lineups = await _get_lineups(db, match.id)
    injuries = await _get_injuries(db, match.id)

    context["lineups_available"] = lineups is not None
    if lineups:
        context["home_formation"] = lineups.home_formation
        context["away_formation"] = lineups.away_formation

    if injuries:
        context["key_absences"] = [
            f"{i.player_name} ({i.team}, {i.reason})"
            for i in injuries if i.is_key_player
        ]
```

**Critério de conclusão da Fase 3:**
- `match_stats.xg` preenchido para jogos terminados nas últimas 24h
- `match_lineups` preenchido para pelo menos 1 jogo próximo
- JSON de output do `/analysis/match/{id}` inclui `lineups_available: true`
- Rate limit não excedido (verificar headers `x-ratelimit-remaining`)

---

## Fase 4 — ELO real para ténis (~1h)

> Objectivo: usar os ELOs calculados dos CSVs Sackmann na análise de ténis,
> em vez de probabilidade 50/50.

### 4.1 — Criar `elo.py` no Python Engine

**Ficheiro a criar:** `src/analysis-engine/app/services/stats/elo.py`

```python
async def get_player_elo(
    db: AsyncSession,
    player_name: str,
    surface: str,
) -> float:
    """
    Busca ELO actual do jogador na superfície via view latest_player_elo.
    Fallback: 1500 (ELO inicial) se jogador não encontrado.
    """
    result = await db.execute(text("""
        SELECT elo FROM latest_player_elo
        WHERE player_name ILIKE :name
          AND surface = :surface
        LIMIT 1
    """), {"name": f"%{player_name}%", "surface": surface})

    row = result.fetchone()
    return float(row.elo) if row else 1500.0


def probabilidade_vitoria_elo(elo_a: float, elo_b: float) -> float:
    """Probabilidade do jogador A vencer o jogador B."""
    return 1 / (1 + 10 ** ((elo_b - elo_a) / 400))
```

### 4.2 — Integrar ELO na análise de ténis

**Ficheiro a modificar:** `src/analysis-engine/app/services/analysis_service.py`

```python
# Para jogos de ténis:
if match.sport == "tennis":
    surface = match.surface or "Hard"
    elo_home = await get_player_elo(db, match.home_team, surface)
    elo_away = await get_player_elo(db, match.away_team, surface)
    prob_home = probabilidade_vitoria_elo(elo_home, elo_away)

    return ModelProbabilities(
        home=round(prob_home, 4),
        draw=0.0,
        away=round(1 - prob_home, 4),
        over_2_5=0.0,
        btts=0.0,
        data_source=f"elo_{surface.lower()}",
    )
```

**Critério de conclusão da Fase 4:**
- Análise de jogo de ténis retorna `data_source: "elo_clay"` ou `"elo_hard"`
- Probabilidades diferentes de 50/50 para jogadores com rankings distintos

---

## Resumo de ficheiros a criar/modificar

### Ficheiros novos

| Ficheiro | Fase | Notas |
|---------|------|-------|
| `src/analysis-engine/app/services/stats/poisson.py` | 2 | Já gerado — copiar do output |
| `src/analysis-engine/app/services/stats/form.py` | 2 | Criar de raiz |
| `src/analysis-engine/app/services/stats/elo.py` | 4 | Criar de raiz |
| `src/DataCollector/.../Clients/IApiFootballClient.cs` | 3 | Já gerado — copiar do output |
| `src/DataCollector/.../Jobs/FootballStatsDetailJob.cs` | 3 | Criar de raiz |
| `src/DataCollector/.../Jobs/FootballLineupsJob.cs` | 3 | Criar de raiz |
| `db/migrations/V011__create_match_lineups.sql` | 3 | Criar de raiz |
| `db/seeds/load_historical.py` | 1 | Já existe — executar |

### Ficheiros a modificar

| Ficheiro | Fase | O que muda |
|---------|------|-----------|
| `app/services/analysis_service.py` | 2, 3, 4 | Integrar Poisson, forma, H2H, lineups, ELO |
| `app/prompts/match_analysis.txt` | 2 | Adicionar secções de forma e H2H |
| `DataCollector.Api/appsettings.json` | 3 | Adicionar `ApiKeys:ApiFootball` |
| `docker-compose.yml` | 3 | Passar `API_FOOTBALL_KEY` ao container |
| `.env.example` | 3 | Documentar `API_FOOTBALL_KEY` |
| `pyproject.toml` | 2 | Adicionar scipy, numpy, tqdm |

---

## Estimativa de tempo total

| Fase | Trabalho humano | Execução automática |
|------|----------------|---------------------|
| Fase 1 — Dados históricos | ~20 min (setup + verificação) | ~20 min (script a correr) |
| Fase 2 — Poisson real | ~2h de OpenCode | — |
| Fase 3 — API-Football | ~3h de OpenCode | Contínuo (jobs Hangfire) |
| Fase 4 — ELO ténis | ~1h de OpenCode | — |
| **Total** | **~6h de OpenCode** | **~20 min de espera** |

---

## Ordem de execução com OpenCode

```bash
# 1. Iniciar OpenCode
make opencode

# 2. Prime context (OpenCode lê harness files automaticamente)
# Verificar que AGENTS.md, CONTEXT.md e TASKS.md estão actualizados

# 3. Executar Fase 1 (manual — não precisa de OpenCode)
python db/seeds/load_historical.py --sport football
python db/seeds/load_historical.py --sport tennis

# 4. Fase 2 — Poisson (OpenCode em modo build)
# Prompt para OpenCode:
# "Implementa a Fase 2 do plano de implementação:
#  1. Copia o ficheiro poisson.py do output para src/analysis-engine/app/services/stats/
#  2. Cria form.py com FormData, H2HData e as funções calcular_forma_recente e calcular_h2h
#  3. Actualiza analysis_service.py para usar Poisson em vez de implied probability
#  4. Actualiza match_analysis.txt para incluir forma e H2H no contexto
#  5. Adiciona scipy e numpy ao pyproject.toml
#  6. Corre pytest e confirma que passa"

# 5. Fase 3 — API-Football (OpenCode em modo build)
# Prompt para OpenCode:
# "Implementa a Fase 3 do plano: IApiFootballClient, FootballStatsDetailJob,
#  FootballLineupsJob, migração V011. Gerir rate limit a max 80 req/dia."

# 6. Fase 4 — ELO ténis (OpenCode em modo build)
# Prompt para OpenCode:
# "Implementa a Fase 4: elo.py e integração na análise de jogos de ténis"
```

---

## Métricas de sucesso

Quando todas as 4 fases estiverem completas, o JSON de output deve ter:

```json
{
  "model_probabilities": {
    "home": 0.58,
    "draw": 0.23,
    "away": 0.19,
    "over_2_5": 0.63,
    "btts": 0.54,
    "data_source": "match_stats_xg"  // ← não "implied_probability"
  },
  "context_flags": {
    "home_form": "VVEDV",
    "away_form": "DVDEE",
    "h2h_note": "Benfica 3V 1E 1D nos últimos 5 encontros",
    "home_xg_avg": 1.94,
    "away_xg_avg": 0.71,
    "key_absences": ["Evanilson (Porto) — duvidoso"],
    "lineups_available": true
  }
}
```

Se `data_source` retornar `"historical_matches"` em vez de `"match_stats_xg"`,
o xG da api-football ainda não está disponível para essa equipa — é o comportamento
esperado de fallback, não um erro.
