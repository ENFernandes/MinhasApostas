# Sports Betting AI

Sistema de análise de apostas desportivas com recolha automática de dados,
cálculo estatístico e recomendação por IA. Cobre futebol e ténis com odds
em tempo real e análise via LLM (Anthropic, OpenRouter ou Ollama local).

---

## Stack


| Camada          | Tecnologia                                  |
| --------------- | ------------------------------------------- |
| Data Collector  | C# .NET 10 + ASP.NET Core + Hangfire        |
| Analysis Engine | Python 3.14 + FastAPI + scipy/numpy         |
| LLM             | Anthropic / OpenRouter / Ollama (pluggable) |
| Frontend        | React 18 + Vite + shadcn/ui                 |
| Base de dados   | PostgreSQL 16                               |
| Message broker  | RabbitMQ 3.13                               |
| Cache           | Redis 7                                     |
| Infra           | Docker Compose + Caddy                      |


---

## Setup em 5 minutos

### Pré-requisitos

- [Docker Desktop](https://docker.com) 24+
- [Node.js](https://nodejs.org) 24 LTS
- [.NET 10 SDK](https://dotnet.microsoft.com)
- [Python 3.14](https://python.org)
- [Ollama](https://ollama.ai) (opcional — para LLM local gratuito)

### 1. Clonar e configurar

```bash
git clone https://github.com/teu-user/sports-betting-ai.git
cd sports-betting-ai

# Setup automático (verifica deps, cria .env, instala OpenCode)
make setup
```

### 2. Preencher as API keys no `.env`

```bash
# APIs gratuitas — registo rápido:
# football-data.org  → https://www.football-data.org/client/register
# api-tennis.com     → https://api-tennis.com/
# the-odds-api.com   → https://the-odds-api.com/account/

nano .env   # ou usar o editor da tua preferência
```

### 3. Escolher o LLM provider

**Opção A — Ollama local (gratuito, recomendado para desenvolvimento)**

```bash
# Instalar Ollama: https://ollama.ai
ollama pull llama3.1:8b

# No .env:
# LLM_PROVIDER=ollama
# LLM_MODEL=llama3.1:8b
```

**Opção B — Anthropic**

```bash
# No .env:
# LLM_PROVIDER=anthropic
# LLM_MODEL=claude-sonnet-4-5
# ANTHROPIC_API_KEY=sk-ant-...
```

**Opção C — OpenRouter (acesso a múltiplos modelos)**

```bash
# No .env:
# LLM_PROVIDER=openrouter
# LLM_MODEL=anthropic/claude-sonnet-4-5
# OPENROUTER_API_KEY=sk-or-...
```

### 4. Iniciar

```bash
make up
```


| Serviço                 | URL                                                            |
| ----------------------- | -------------------------------------------------------------- |
| Frontend                | [http://localhost:3000](http://localhost:3000)                 |
| Collector API + Swagger | [http://localhost:8080/swagger](http://localhost:8080/swagger) |
| Analysis API            | [http://localhost:8090](http://localhost:8090)                 |
| RabbitMQ UI             | [http://localhost:15672](http://localhost:15672)               |


### 5. Abrir o OpenCode

```bash
make opencode
```

---

## Comandos úteis

```bash
make help           # lista todos os comandos
make logs           # logs em tempo real
make status         # estado dos containers
make test-all       # corre todos os testes
make lint-all       # lint em todos os serviços
make shell-db       # shell psql na BD
make rabbit-queues  # estado das filas RabbitMQ
make backup-db      # backup da BD
make down           # para tudo
```

---

## Estrutura do projeto

```
sports-betting-ai/
├── AGENTS.md          ← harness: stack e convenções (ler primeiro)
├── CONTEXT.md         ← harness: domínio e fórmulas de apostas
├── TASKS.md           ← harness: backlog e estado actual
├── opencode.json      ← configuração OpenCode + MCPs
├── docker-compose.yml
├── Makefile
├── .env.example
├── src/
│   ├── DataCollector/ ← C# .NET 10
│   ├── analysis-engine/ ← Python 3.14
│   └── frontend/      ← React 18
├── db/migrations/     ← SQL versionado
└── infra/             ← Caddy, RabbitMQ config
```

---

## Documentação

- [AGENTS.md](./AGENTS.md) — stack, estrutura, convenções de código
- [CONTEXT.md](./CONTEXT.md) — domínio de apostas, fórmulas, regras de negócio
- [TASKS.md](./TASKS.md) — backlog completo por módulo
- [docs/architecture.md](./docs/architecture.md) — diagrama de arquitectura
- [docs/api-contracts.md](./docs/api-contracts.md) — contratos REST

---

