# CLAUDE.md — Sports Betting AI System

> Este ficheiro é lido pelo OpenCode em cada sessão. Define o stack, convenções,
> estrutura do projeto e regras que todos os agentes devem seguir sem excepção.
Ver o `SUMMARY.md` para perceber o estado do projeto.
---

## Visão geral do projeto

Sistema de análise de apostas desportivas com recolha automática de dados,
cálculo estatístico e recomendação por IA. Cobre futebol (football-data.org)
e ténis (api-tennis.com), com odds em tempo real via the-odds-api.com.

O sistema é composto por três serviços independentes que comunicam via
message queue (RabbitMQ) e uma base de dados partilhada (PostgreSQL).

---

## Stack tecnológico

### Serviço 1 — Data Collector (C#)
- Runtime: .NET 10
- Framework API: ASP.NET Core 10 (minimal APIs)
- ORM: Entity Framework Core 10 com Npgsql
- Scheduler: Hangfire com PostgreSQL como backing store
- HTTP Client: Refit (typed HTTP clients para cada API externa)
- Serialização: System.Text.Json
- Testes: xUnit + Moq
- Lint: dotnet-format + Roslyn Analyzers
- Registo de dependências: Reflection (assembly scanning automático)

### Serviço 2 — Analysis Engine (Python)
- Runtime: Python 3.14
- API framework: FastAPI 0.111+
- Cálculo estatístico: scipy, numpy, pandas
- HTTP client: httpx (async)
- Queue consumer: aio-pika (RabbitMQ async)
- LLM Provider: abstracção própria `ILLMProvider` — suporta Anthropic, OpenRouter e Ollama (local)
- Testes: pytest + pytest-asyncio
- Lint: ruff + mypy (strict)

### Serviço 3 — Frontend (React)
- Runtime: Node 20 LTS
- Framework: React 18 + Vite
- State: Zustand
- HTTP: TanStack Query v5
- UI components: shadcn/ui + Tailwind CSS
- Charts: Recharts
- Testes: Vitest + Testing Library
- Lint: ESLint + Prettier

### Infraestrutura
- Containerização: Docker + Docker Compose
- Base de dados: PostgreSQL 16
- Message broker: RabbitMQ 3.13 (management plugin activo)
- Cache: Redis 7 (odds em cache, TTL 60s)
- Reverse proxy: Caddy (desenvolvimento local)

---

## Estrutura de pastas

```
sports-betting-ai/
├── AGENTS.md              ← este ficheiro
├── CONTEXT.md             ← domínio de negócio e glossário
├── TASKS.md               ← backlog e estado actual
├── opencode.json          ← configuração MCPs
├── docker-compose.yml     ← todos os serviços
├── .env.example           ← variáveis de ambiente (nunca .env no git)
│
├── src/
│   ├── DataCollector/              ← projecto C# (.NET 10)
│   │   ├── DataCollector.Api/      ← ASP.NET Core entry point
│   │   │   └── Extensions/         ← Reflection DI scanner (ServiceCollectionExtensions)
│   │   ├── DataCollector.Core/     ← domínio, interfaces, modelos
│   │   │   └── Interfaces/         ← IJobService, IApiClient, IRepository<T>
│   │   ├── DataCollector.Infrastructure/  ← EF Core, Refit clients, Hangfire
│   │   │   ├── Jobs/               ← Hangfire jobs (auto-registados via Reflection)
│   │   │   ├── Clients/            ← Refit clients (auto-registados via Reflection)
│   │   │   └── Repositories/       ← EF repos (auto-registados via Reflection)
│   │   └── DataCollector.Tests/    ← xUnit tests
│   │
│   ├── analysis-engine/            ← serviço Python
│   │   ├── app/
│   │   │   ├── main.py             ← FastAPI entry point
│   │   │   ├── routers/            ← endpoints REST
│   │   │   ├── services/           ← lógica de negócio
│   │   │   │   ├── stats/          ← Poisson, xG, ELO
│   │   │   │   ├── odds/           ← implied prob, value, Kelly
│   │   │   │   └── ai/             ← LLM provider + prompt builder
│   │   │   │       ├── provider.py       ← interface ILLMProvider (Protocol)
│   │   │   │       ├── anthropic.py      ← implementação Anthropic
│   │   │   │       ├── openrouter.py     ← implementação OpenRouter
│   │   │   │       ├── ollama.py         ← implementação Ollama (local)
│   │   │   │       └── factory.py        ← selecciona provider via env var
│   │   │   ├── models/             ← Pydantic schemas
│   │   │   ├── consumers/          ← RabbitMQ consumers
│   │   │   └── db/                 ← SQLAlchemy async + migrações Alembic
│   │   ├── tests/
│   │   └── pyproject.toml
│   │
│   └── frontend/                   ← React + Vite
│       ├── src/
│       │   ├── pages/
│       │   ├── components/
│       │   ├── stores/             ← Zustand stores
│       │   ├── hooks/              ← TanStack Query hooks
│       │   └── lib/                ← utils, formatters
│       └── package.json
│
├── db/
│   └── migrations/                 ← ficheiros SQL versionados
│
└── docs/
    ├── architecture.md
    └── api-contracts.md
```

---

## Convenções de código

### C# — regras obrigatórias
- Namespaces: `SportsBetting.DataCollector.[Layer]`
- Sempre usar `async/await` para I/O — nunca `.Result` ou `.Wait()`
- Injecção de dependências via construtor, nunca `new` directo em serviços
- **Registo via Reflection:** todas as classes que implementem `IJobService`, `IRepository<T>` ou `IApiClient` são auto-descobertas e registadas no DI container por assembly scanning em `ServiceCollectionExtensions`. Nunca registar manualmente no `Program.cs`.
- Marker interfaces obrigatórias para o scanner: `ITransientService`, `IScopedService`, `ISingletonService` — a interface usada determina o lifetime
- Todos os métodos públicos com XML doc (`/// <summary>`)
- Erros de API externas: usar `Result<T, Error>` pattern, nunca lançar exceções não tratadas
- Modelos de BD prefixados com entidade: `MatchEntity`, `OddsEntity`
- DTOs separados dos modelos de BD — nunca expor `Entity` directamente na API
- Cada Refit client numa interface própria: `IFootballDataClient`, `IOddsApiClient`
- Hangfire jobs: classes separadas em `Jobs/`, nunca inline lambdas
- Configuração sempre via `IOptions<T>`, nunca `Configuration["key"]` directo

### Python — regras obrigatórias
- Type hints obrigatórios em todas as funções (mypy strict)
- Pydantic v2 para todos os schemas de entrada e saída
- Async em todo o I/O — nunca requests síncrono
- Funções de cálculo estatístico: puras, sem side effects, testáveis isoladamente
- Separação clara: `services/stats/` só faz matemática, não toca na BD
- Variáveis de ambiente via `pydantic-settings` (classe `Settings`)
- Prompts LLM: strings em ficheiros `.txt` em `app/prompts/`, nunca hardcoded
- **LLM Provider abstraction:** nunca chamar Anthropic, OpenRouter ou Ollama directamente nos serviços — usar sempre `ILLMProvider` (Python Protocol). O provider activo é seleccionado pela variável `LLM_PROVIDER` (`anthropic` | `openrouter` | `ollama`). Trocar de provider não toca em nenhum serviço de análise.
- Logs via `structlog` em formato JSON — nunca `print()`

### React — regras obrigatórias
- Componentes: função com TypeScript, nunca class components
- Sem lógica de negócio em componentes — usar hooks customizados
- TanStack Query para todo o estado de servidor — nunca `useEffect` + `fetch`
- Zustand apenas para estado de UI (filtros, selecções, tema)
- Sem `any` no TypeScript — tipos explícitos ou `unknown` com narrowing
- CSS via Tailwind utility classes — sem ficheiros `.css` salvo globals
- Nomes de componentes: PascalCase, ficheiros: kebab-case
- Cada página tem o seu próprio directório com `index.tsx` + `*.test.tsx`

### Git — regras para todos os agentes
- Commits em inglês, formato Conventional Commits:
  `feat(collector): add football-data.org client`
  `fix(python): correct Kelly fraction calculation`
  `chore(docker): update postgres to 16.3`
- Uma feature = uma branch = um PR
- Nunca commitar `.env`, API keys, ou credenciais
- Testes devem passar antes de qualquer commit

---

## APIs externas — referência rápida

| API | Base URL | Auth | Rate limit |
|-----|----------|------|------------|
| football-data.org | `https://api.football-data.org/v4` | Header `X-Auth-Token` | 10 req/min (free) |
| api-tennis.com | `https://api.api-tennis.com/tennis/` | Query `APIkey` | 200 req/dia (free) |
| the-odds-api.com | `https://api.the-odds-api.com/v4` | Query `apiKey` | 500 créditos/mês (free) |

Todas as API keys são lidas de variáveis de ambiente. Ver `.env.example`.
O C# usa Refit para estes três clients.

---

## LLM Providers — referência rápida

O provider activo é controlado pela variável `LLM_PROVIDER`. Trocar não requer alterações de código.

| Provider | Valor `LLM_PROVIDER` | Base URL | Quando usar |
|----------|----------------------|----------|-------------|
| Anthropic | `anthropic` | `https://api.anthropic.com/v1` | produção, melhor qualidade |
| OpenRouter | `openrouter` | `https://openrouter.ai/api/v1` | flexibilidade, múltiplos modelos, fallback |
| Ollama | `ollama` | `http://localhost:11434/v1` | desenvolvimento local, sem custos, privacidade |

**Modelos recomendados por provider:**
- Anthropic: `claude-sonnet-4-5` (equilíbrio custo/qualidade)
- OpenRouter: `anthropic/claude-sonnet-4-5` ou `google/gemini-2.5-pro` como fallback
- Ollama: `llama3.1:8b` (rápido) ou `mistral:7b` (leve)

O modelo é configurado via `LLM_MODEL` — o provider ignora modelos incompatíveis e usa o default.

---

## Variáveis de ambiente obrigatórias

```env
# APIs de dados desportivos
FOOTBALL_DATA_API_KEY=
TENNIS_API_KEY=
ODDS_API_KEY=

# LLM Provider — escolher um
LLM_PROVIDER=ollama                  # anthropic | openrouter | ollama
LLM_MODEL=llama3.1:8b                # depende do provider escolhido

# Chaves por provider (só preencher o activo)
ANTHROPIC_API_KEY=                   # se LLM_PROVIDER=anthropic
OPENROUTER_API_KEY=                  # se LLM_PROVIDER=openrouter
OLLAMA_BASE_URL=http://localhost:11434  # se LLM_PROVIDER=ollama

# PostgreSQL
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=sportsbetting
POSTGRES_USER=
POSTGRES_PASSWORD=

# RabbitMQ
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USER=
RABBITMQ_PASSWORD=

# Redis
REDIS_URL=redis://localhost:6379

# App
ENVIRONMENT=development
LOG_LEVEL=debug
```

---

## Tópicos RabbitMQ

| Exchange | Routing key | Publicador | Consumidor |
|----------|-------------|------------|------------|
| `sports.events` | `football.match.new` | C# Collector | Python Engine |
| `sports.events` | `tennis.match.new` | C# Collector | Python Engine |
| `sports.events` | `odds.updated` | C# Collector | Python Engine |
| `analysis.results` | `recommendation.ready` | Python Engine | React (via API) |

---

## Regras para agentes OpenCode

### Regra 1 — Lê sempre este ficheiro antes de qualquer acção
Antes de criar, editar ou apagar qualquer ficheiro, confirma que a localização
e convenções respeitam a estrutura definida acima.

### Regra 2 — Modo Plan antes de Build
Para qualquer módulo novo ou refactoring significativo, usa o modo Tab (plan)
para propor a solução. Só avança para build após aprovação explícita.

### Regra 3 — Nunca inventar modelos de dados
O schema da BD está definido em `db/migrations/`. Não criar tabelas ou colunas
novas sem actualizar os ficheiros de migração e o TASKS.md.

### Regra 4 — Testes antes de commit
Após qualquer alteração de código, executar:
- C#: `dotnet test`
- Python: `pytest`
- React: `vitest run`
Se os testes falharem, corrigir antes de commitar.

### Regra 5 — Logging obrigatório em jobs e consumers
Todos os Hangfire jobs (C#) e RabbitMQ consumers (Python) devem ter logs
de início, fim e erro. Facilita o debug em produção.

### Regra 6 — Consultar CONTEXT.md para decisões de domínio
Dúvidas sobre lógica de apostas (como calcular value, quando usar Kelly,
que mercados analisar) — a resposta está no CONTEXT.md, não inventar.

---

## Estado actual do projeto

Ver `TASKS.md` para o estado detalhado de cada módulo.