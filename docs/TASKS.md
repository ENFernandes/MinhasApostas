# TASKS.md — Backlog e Estado do Projeto

> Ficheiro de controlo de progresso. Actualizar após cada sessão de trabalho.
> O agente OpenCode lê este ficheiro para saber o que está feito, o que está
> em curso e qual a próxima tarefa a executar.
>
> Estados: `[ ]` pendente · `[~]` em curso · `[x]` concluído · `[!]` bloqueado

---

## Estado geral


| Módulo                         | Estado | Progresso |
| ------------------------------ | ------ | --------- |
| M0 — Infraestrutura base       | `[x]`  | 100%      |
| M1 — Database schema           | `[x]`  | 100%      |
| M2 — C# Data Collectors        | `[x]`  | 100%      |
| M3 — C# ASP.NET API            | `[x]`  | 100%      |
| M4 — Python Stats Engine       | `[x]`  | 100%      |
| M5 — Python Odds Engine        | `[x]`  | 100%      |
| M6 — LLM Provider abstraction  | `[x]`  | 100%      |
| M7 — Python FastAPI + RabbitMQ | `[x]`  | 100%      |
| M8 — React Dashboard           | `[x]`  | 100%      |
| M9 — Alertas e notificações    | `[ ]`  | 0%        |


---

## M0 — Infraestrutura base

> Objectivo: ambiente local a correr com um comando (`docker compose up`).
> Sem isto nenhum outro módulo pode ser testado.

- **M0-01** Criar repositório Git com estrutura de pastas definida no `AGENTS.md`
- **M0-02** Criar `docker-compose.yml` com os seguintes serviços:
  - PostgreSQL 16 (porta 5432, volume persistente)
  - RabbitMQ 3.13 com management plugin (portas 5672 + 15672)
  - Redis 7 (porta 6379)
  - Caddy como reverse proxy (porta 80)
- **M0-03** Criar `.env.example` com todas as variáveis definidas no `AGENTS.md`
- **M0-04** Criar `Makefile` com comandos: `make up`, `make down`, `make logs`, `make reset-db`
- **M0-05** Validar que todos os serviços sobem sem erros e comunicam entre si
- **M0-06** Criar `README.md` raiz com instruções de setup (5 minutos do zero ao running)

**Critério de conclusão:** `docker compose up` levanta todos os serviços, RabbitMQ
management UI acessível em `http://localhost:15672`, PostgreSQL aceitando ligações.

---

## M1 — Database schema

> Objectivo: schema completo da base de dados com migrações versionadas.
> Todas as tabelas, índices e relações definidas antes de escrever qualquer serviço.

### Tabelas principais

- **M1-01** Tabela `competitions` — ligas e torneios
  ```sql
  id, external_id, name, sport, country, season, is_active, created_at
  ```
- **M1-02** Tabela `teams` — equipas de futebol
  ```sql
  id, external_id, name, short_name, country, competition_id, created_at
  ```
- **M1-03** Tabela `players` — jogadores de ténis
  ```sql
  id, external_id, name, country, ranking, elo_overall,
  elo_clay, elo_grass, elo_hard, elo_indoor, created_at, updated_at
  ```
- **M1-04** Tabela `matches` — jogos (futebol e ténis)
  ```sql
  id, external_id, sport, competition_id, home_id, away_id,
  commence_time, status, home_score, away_score,
  home_xg, away_xg, minute, created_at, updated_at
  ```
- **M1-05** Tabela `match_stats` — estatísticas por jogo (futebol)
  ```sql
  id, match_id, team_id, shots, shots_on_target, possession,
  corners, fouls, yellow_cards, red_cards, xg, created_at
  ```
- **M1-06** Tabela `odds` — odds por jogo, bookmaker e mercado
  ```sql
  id, match_id, bookmaker, market, outcome, odd_decimal,
  implied_probability, captured_at
  ```
- **M1-07** Tabela `recommendations` — recomendações geradas pelo sistema
  ```sql
  id, match_id, market, outcome, bookmaker, odd_decimal,
  model_probability, implied_probability, value, kelly_fraction,
  stake_euros, confidence, reasoning, llm_provider, llm_model,
  status, created_at
  ```
- **M1-08** Tabela `bet_results` — resultado real das apostas
  ```sql
  id, recommendation_id, stake_actual, odd_actual, outcome,
  profit_loss, notes, settled_at
  ```
- **M1-09** Tabela `config` — thresholds configuráveis em runtime
  ```sql
  id, key, value, description, updated_at
  ```
  Seed com os valores default do `CONTEXT.md`.
- **M1-10** Tabela `team_form` — forma recente calculada (cache)
  ```sql
  id, team_id, last_10_games (jsonb), avg_goals_scored_home,
  avg_goals_scored_away, avg_goals_conceded_home,
  avg_goals_conceded_away, calculated_at
  ```

### Índices e constraints

- **M1-11** Índices em `matches(commence_time)`, `matches(status)`,
`odds(match_id, bookmaker, market)`, `recommendations(match_id)`,
`recommendations(status)`, `bet_results(recommendation_id)`
- **M1-12** Constraint unique em `matches(external_id, sport)` —
evitar duplicados na recolha
- **M1-13** Constraint unique em `odds(match_id, bookmaker, market, outcome, captured_at)`

### Migrações

- **M1-14** Criar ficheiros de migração SQL versionados em `db/migrations/`
formato: `V001__create_competitions.sql`, `V002__create_teams.sql`, etc.
- **M1-15** Adicionar Flyway ou DbUp ao serviço C# para aplicar migrações
automaticamente no startup

**Critério de conclusão:** todas as tabelas criadas, migrações a correr sem erros,
seed de `config` com valores default carregado.

---

## M2 — C# Data Collectors

> Objectivo: recolha automática e agendada de dados das três APIs externas.
> Usar Reflection para registo automático de todos os collectors e jobs.

### Reflection DI Scanner

- **M2-01** Criar `ServiceCollectionExtensions.cs` em `DataCollector.Api/Extensions/`
que faz assembly scanning e regista automaticamente:
  - Todas as classes que implementem `IScopedService` → `AddScoped`
  - Todas as classes que implementem `ITransientService` → `AddTransient`
  - Todas as classes que implementem `ISingletonService` → `AddSingleton`
  - Todas as interfaces Refit (`IFootballDataClient`, etc.) → `AddRefitClient`
  - Todos os Hangfire jobs → registo automático no scheduler
- **M2-02** Criar marker interfaces em `DataCollector.Core/Interfaces/`:
`IScopedService`, `ITransientService`, `ISingletonService`, `IRepository<T>`, `IJobService`

### Football Collector

- **M2-03** Criar `IFootballDataClient` (Refit interface) com endpoints:
  - `GET /competitions/{id}/matches` — jogos por competição
  - `GET /matches/{id}` — detalhe de um jogo
  - `GET /teams/{id}` — detalhe de equipa
- **M2-04** Criar `FootballCollectorJob : IJobService` que:
  - Corre diariamente às 08:00
  - Recolhe jogos das próximas 48h para todas as competições activas
  - Persiste na tabela `matches` (upsert por `external_id`)
  - Publica evento `football.match.new` no RabbitMQ para cada jogo novo
- **M2-05** Criar `FootballStatsJob : IJobService` que:
  - Corre de hora a hora para jogos com `status = IN_PLAY`
  - Actualiza `match_stats` e `matches.minute`, `home_score`, `away_score`
- **M2-06** Competições a recolher (configurável em `config`):
  - Premier League (PL), La Liga (PD), Bundesliga (BL1)
  - Serie A (SA), Ligue 1 (FL1), Primeira Liga (PPL)
  - Champions League (CL)

### Tennis Collector

- **M2-07** Criar `ITennisApiClient` (Refit interface) com endpoints:
  - `GET /` com `method=get_tournaments` — torneios activos
  - `GET /` com `method=get_H2H` — histórico H2H
  - `GET /` com `method=get_live_score` — scores em tempo real
- **M2-08** Criar `TennisCollectorJob : IJobService` que:
  - Corre diariamente às 07:00
  - Recolhe jogos ATP e WTA das próximas 24h
  - Persiste em `matches` e `players`
  - Publica evento `tennis.match.new` no RabbitMQ

### Odds Collector

- **M2-09** Criar `IOddsApiClient` (Refit interface) com endpoints:
  - `GET /sports` — lista de desportos disponíveis
  - `GET /sports/{sport}/odds` — odds por desporto e região
- **M2-10** Criar `OddsCollectorJob : IJobService` que:
  - Corre de 30 em 30 minutos para jogos nas próximas 6h
  - Corre de 5 em 5 minutos para jogos in-play
  - Persiste odds na tabela `odds`
  - Publica evento `odds.updated` no RabbitMQ
  - Respeita rate limits da API (controlo de créditos usados)
- **M2-11** Bookmakers europeus a recolher (região `eu`):
Betfair, Unibet, Pinnacle, Bet365, Bwin, William Hill

### Repositórios

- **M2-12** Criar `IMatchRepository`, `IOddsRepository`, `ITeamRepository`,
`IPlayerRepository` em `DataCollector.Core/Interfaces/`
- **M2-13** Implementar repositórios EF Core em `DataCollector.Infrastructure/Repositories/`
— todos implementam `IScopedService` para auto-registo via Reflection

### Testes

- **M2-14** Unit tests para cada job (mockar Refit clients e repositórios)
- **M2-15** Integration test que valida ligação real à football-data.org API
(marcado com `[Fact(Skip = "integration")]` — corre manualmente)

**Critério de conclusão:** `dotnet test` passa, jobs a correr no Hangfire,
dados a aparecer nas tabelas da BD após primeira execução.

---

## M3 — C# ASP.NET Core API

> Objectivo: API REST interna que expõe os dados recolhidos ao Python Engine
> e ao React Frontend.

- **M3-01** Endpoint `GET /api/matches/upcoming` — jogos das próximas 24h
com parâmetros: `sport`, `competition`, `from`, `to`
- **M3-02** Endpoint `GET /api/matches/{id}` — detalhe completo de um jogo
incluindo últimas odds e stats
- **M3-03** Endpoint `GET /api/matches/{id}/odds` — todas as odds de um jogo
agrupadas por bookmaker e mercado
- **M3-04** Endpoint `GET /api/teams/{id}/form` — forma recente de uma equipa
(últimos 10 jogos, médias de golos)
- **M3-05** Endpoint `GET /api/players/{id}/stats` — stats de um jogador de
ténis incluindo ELO por superfície
- **M3-06** Endpoint `POST /api/recommendations` — recebe recomendação do
Python Engine e persiste na BD
- **M3-07** Endpoint `GET /api/recommendations` — lista recomendações com
filtros: `date`, `sport`, `status`
- **M3-08** Endpoint `POST /api/bets` — registar resultado real de uma aposta
- **M3-09** Endpoint `GET /api/stats/performance` — P&L total, ROI,
taxa de acerto por mercado e desporto
- **M3-10** Configurar autenticação API Key simples para todos os endpoints
(header `X-API-Key`) — evitar exposição acidental
- **M3-11** Swagger/OpenAPI documentação gerada automaticamente
- **M3-12** Health check endpoint `GET /health` para Docker healthcheck

**Critério de conclusão:** Swagger UI acessível, todos os endpoints a responder,
autenticação a funcionar.

---

## M4 — Python Stats Engine

> Objectivo: módulo de cálculo estatístico puro — sem I/O, sem BD, sem LLM.
> Funções testáveis isoladamente com inputs e outputs bem definidos.

- **M4-01** Implementar `services/stats/poisson.py`:
  - `calcular_lambda(team_id, is_home, window) -> float`
  - `calcular_probabilidades_poisson(lambda_casa, lambda_fora) -> dict`
  - Janela deslizante de 10 jogos com factor de decaimento 0.9
- **M4-02** Implementar `services/stats/elo.py`:
  - `probabilidade_elo(elo_a, elo_b) -> float`
  - `actualizar_elo(elo_vencedor, elo_perdedor, k) -> tuple`
  - ELO separado por superfície para ténis
- **M4-03** Implementar `services/stats/form.py`:
  - `calcular_forma_recente(team_id, n_jogos) -> FormData`
  - `calcular_h2h(home_id, away_id, n_jogos) -> H2HData`
  - `calcular_xg_medio(team_id, is_home, window) -> float`
- **M4-04** Implementar `services/stats/inplay.py`:
  - `ajustar_probabilidades_inplay(base_probs, minuto, score, xg_parcial) -> dict`
  - Ajuste de probabilidades baseado no estado actual do jogo
- **M4-05** Testes unitários para todas as funções estatísticas
com fixtures de dados reais de jogos históricos
- **M4-06** Benchmark de performance — o cálculo Poisson completo
deve demorar < 50ms por jogo

**Critério de conclusão:** `pytest tests/stats/` passa 100%, coverage > 90%.

---

## M5 — Python Odds Engine

> Objectivo: cálculo de value bets e gestão de banca.

- **M5-01** Implementar `services/odds/calculator.py`:
  - `probabilidade_implicita(odd) -> float`
  - `remover_margem(odds_dict) -> dict` — odds justas sem vig
  - `calcular_value(prob_real, odd_decimal) -> float`
  - `deve_apostar(prob_real, odd_decimal, **thresholds) -> bool`
- **M5-02** Implementar `services/odds/kelly.py`:
  - `kelly_fraction(prob_real, odd_decimal, fraction, cap) -> float`
  - `calcular_stake(kelly_frac, banca_total) -> float`
  - Respeitar todas as regras de negócio RN-01 a RN-10 do `CONTEXT.md`
- **M5-03** Implementar `services/odds/comparator.py`:
  - `melhor_odd_por_mercado(odds_list) -> dict` — melhor odd entre bookmakers
  - `detectar_movimento_odds(odds_historico) -> OddsMovement` — sharp money
- **M5-04** Implementar `services/odds/thresholds.py`:
  - Lê configuração da BD (tabela `config`) via cache Redis
  - Expõe `get_threshold(key) -> float` usado por todo o engine
- **M5-05** Testes unitários com casos de edge: value exactamente 5%,
Kelly negativo, odd fora dos limites, banca < 50€ (RN-10)

**Critério de conclusão:** `pytest tests/odds/` passa 100%, todas as RN validadas.

---

## M6 — LLM Provider abstraction

> Objectivo: sistema pluggable de providers LLM.
> Trocar de provider = mudar variável de ambiente.

- **M6-01** Definir `services/ai/provider.py` — Protocol (interface):
  ```python
  class ILLMProvider(Protocol):
      async def complete(self, prompt: str, system: str) -> LLMResponse: ...
      async def health_check(self) -> bool: ...
  ```
- **M6-02** Implementar `services/ai/anthropic.py`:
  - Usa `httpx` async (não o SDK oficial — mais controlo)
  - Modelo configurável via `LLM_MODEL`
  - Retry com backoff exponencial (3 tentativas)
- **M6-03** Implementar `services/ai/openrouter.py`:
  - API compatível com OpenAI — endpoint `/chat/completions`
  - Header `HTTP-Referer` obrigatório (política OpenRouter)
  - Suporte a fallback de modelo via `LLM_FALLBACK_MODEL`
- **M6-04** Implementar `services/ai/ollama.py`:
  - Endpoint local `http://localhost:11434/api/generate`
  - Verificar se modelo está disponível no health_check
  - Timeout generoso (modelos locais são lentos): 120s
- **M6-05** Implementar `services/ai/factory.py`:
  ```python
  def get_provider() -> ILLMProvider:
      match settings.LLM_PROVIDER:
          case "anthropic": return AnthropicProvider()
          case "openrouter": return OpenRouterProvider()
          case "ollama": return OllamaProvider()
  ```
- **M6-06** Implementar `services/ai/prompt_builder.py`:
  - Lê templates de `app/prompts/*.txt`
  - `build_match_analysis_prompt(match, probs, odds, context) -> str`
  - `parse_llm_response(raw) -> RecommendationOutput` com validação Pydantic
- **M6-07** Testes unitários com mocks dos três providers
- **M6-08** Teste de integração manual (marcado skip) para cada provider real

**Critério de conclusão:** `pytest tests/ai/` passa, trocar `LLM_PROVIDER`
no `.env` e o sistema usa o provider correcto sem restart de código.

---

## M7 — Python FastAPI + RabbitMQ

> Objectivo: ligar todos os módulos Python num serviço coeso com API REST
> e consumo de eventos do RabbitMQ.

- **M7-01** Criar `main.py` com FastAPI app, lifespan, e registo de routers
- **M7-02** Criar `consumers/match_consumer.py`:
  - Consome `football.match.new` e `tennis.match.new`
  - Despoleta análise completa: stats → odds → LLM → recomendação
  - Publica resultado em `analysis.results` exchange
- **M7-03** Criar `consumers/odds_consumer.py`:
  - Consome `odds.updated`
  - Re-calcula value para jogos activos
  - Se value mudou significativamente (> 2%), re-analisa com LLM
- **M7-04** Criar `routers/analysis.py`:
  - `POST /analysis/match/{id}` — análise on-demand de um jogo
  - `GET /analysis/match/{id}/latest` — última recomendação para um jogo
- **M7-05** Criar `routers/health.py`:
  - `GET /health` — estado do serviço, BD, RabbitMQ, LLM provider
- **M7-06** Configurar connection pool RabbitMQ com reconexão automática
- **M7-07** Rate limiting no FastAPI — máximo 60 requests/minuto por IP
- **M7-08** Dockerfile para o serviço Python

**Critério de conclusão:** serviço sobe, consome mensagens do RabbitMQ,
endpoint de análise on-demand funcional, health check verde.

---

## M8 — React Dashboard

> Objectivo: interface de utilizador para visualizar jogos, recomendações e histórico.

### Setup

- **M8-01** Criar projecto Vite + React 18 + TypeScript
- **M8-02** Configurar Tailwind CSS + shadcn/ui
- **M8-03** Configurar TanStack Query + Zustand
- **M8-04** Configurar proxy Vite para API C# e Python em dev

### Páginas

- **M8-05** Página `/` — Dashboard diário
  - Cards de jogos do dia com recomendação destacada
  - Filtro por desporto e competição
  - Badge de confiança (cor por nível: verde ≥8, amarelo 6-7)
  - Odds ao vivo com atualização automática (polling 60s)
- **M8-06** Página `/match/:id` — Detalhe do jogo
  - Probabilidades do modelo vs odds do mercado (gráfico de barras)
  - Comparação de odds entre bookmakers
  - Reasoning do LLM formatado
  - Botão "registar aposta" → abre modal com stake
- **M8-07** Página `/history` — Histórico de apostas
  - Tabela de apostas com resultado e P&L
  - Gráfico de evolução da banca (linha temporal)
  - Métricas: ROI total, taxa de acerto, P&L por mercado
- **M8-08** Página `/settings` — Configurações
  - Banca actual
  - Thresholds (MIN_VALUE, MIN_CONFIDENCE, MAX_STAKE_PCT)
  - Provider LLM activo (read-only, informativo)

### Componentes partilhados

- **M8-09** `OddBadge` — exibe odd com cor (verde se value positivo)
- **M8-10** `ConfidenceMeter` — barra visual de confiança 1-10
- **M8-11** `MatchCard` — card resumo de jogo com recomendação
- **M8-12** `BankrollChart` — gráfico de evolução da banca (Recharts)
- **M8-13** `StakeModal` — modal para registar aposta com cálculo Kelly

**Critério de conclusão:** todas as páginas funcionais, dados reais a aparecer,
registo de aposta end-to-end a funcionar.

---

## M9 — Alertas e notificações

> Objectivo: notificar o utilizador de value bets em tempo real.

- **M9-01** Integrar Telegram Bot API no Python Engine
  - Enviar mensagem quando `confidence ≥ 8` e `value ≥ 0.10`
  - Formato: jogo, mercado, odd, stake recomendado, reasoning (1 frase)
- **M9-02** Integrar envio de email (SMTP) como fallback
  - Template HTML com resumo da recomendação
- **M9-03** Configurar variáveis: `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`,
`SMTP_`* — opcionais, sistema funciona sem eles
- **M9-04** Adicionar preferências de alerta à página `/settings` no React

**Critério de conclusão:** mensagem Telegram recebida após análise com alta confiança.

---

## Sessões de trabalho

> Registar aqui o que foi feito em cada sessão para manter contexto.


| Data | Módulo | O que foi feito                                               | Próxima tarefa |
| ---- | ------ | ------------------------------------------------------------- | -------------- |
| 2026-03-24 | M0 | Infraestrutura Docker (PostgreSQL, RabbitMQ, Redis, Caddy), Makefile, .env.example, README.md | M2-01 |
| 2026-03-24 | M1 | Database schema completo (10 tabelas), migrações SQL, seed config | M2-01 |
| 2026-03-24 | M2 | Estrutura C# completa: Reflection DI scanner, marker interfaces, Refit clients, Hangfire jobs, repositórios EF Core, testes xUnit | M3-01 |
| 2026-03-24 | M3-M7 | API C# completa com controllers, DTOs, auth. Python: Stats Engine (Poisson, ELO, form, in-play), Odds Engine (Kelly, value, comparator), LLM Providers (Anthropic, OpenRouter, Ollama), FastAPI + RabbitMQ consumers | M8-01 |
| 2026-03-24 | M8 | React Dashboard completo: Vite + TypeScript, Tailwind CSS, design system luxury dark theme, pages Dashboard/History/Settings, componentes (MatchCard, OddBadge, ConfidenceMeter, BankrollChart, StakeModal), Zustand stores, TanStack Query | M9-01 |
| —    | —      | Harness files criados (AGENTS, CONTEXT, TASKS, opencode.json) | M0-01          |


---

## Decisões técnicas registadas

> Decisões importantes tomadas durante o desenvolvimento. Nunca reverter sem discussão.


| ID   | Decisão                                     | Motivo                                                         |
| ---- | ------------------------------------------- | -------------------------------------------------------------- |
| D-01 | Reflection para DI em vez de registo manual | Escalabilidade — novos collectors sem tocar Program.cs         |
| D-02 | ILLMProvider abstraction com factory        | Independência de vendor, dev local grátis com Ollama           |
| D-03 | RabbitMQ para comunicação C# ↔ Python       | Desacoplamento, sem perda de mensagens se Python estiver lento |
| D-04 | Kelly fraccionado a 25%                     | Reduz variância, mais adequado para bancas pequenas            |
| D-05 | Só apostas simples, sem acumuladores        | Acumuladores destroem o edge matemático do value betting       |
| D-06 | Odds em cache Redis (TTL 60s)               | Evitar exceder créditos da The Odds API                        |
| D-07 | ELO separado por superfície no ténis        | Superfície é o factor mais determinante no ténis               |


