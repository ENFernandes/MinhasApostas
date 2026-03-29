.PHONY: help up down logs reset-db shell-db shell-rabbit migrate seed \
        build-collector build-engine build-frontend build-all \
        test-collector test-engine test-frontend test-all \
        lint-collector lint-engine lint-frontend lint-all \
        ollama-pull opencode

# ─────────────────────────────────────────
# Configuração
# ─────────────────────────────────────────
COMPOSE = docker compose
COLLECTOR_CONTAINER = sba-collector
ENGINE_CONTAINER    = sba-analysis
POSTGRES_CONTAINER  = sba-postgres
RABBIT_CONTAINER    = sba-rabbitmq

# Cores para output
GREEN  = \033[0;32m
YELLOW = \033[0;33m
CYAN   = \033[0;36m
RESET  = \033[0m

# ─────────────────────────────────────────
# Help
# ─────────────────────────────────────────
help: ## Mostra este menu de ajuda
	@echo ""
	@echo "$(CYAN)Sports Betting AI — Comandos disponíveis$(RESET)"
	@echo ""
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-22s$(RESET) %s\n", $$1, $$2}'
	@echo ""

# ─────────────────────────────────────────
# Infraestrutura
# ─────────────────────────────────────────
up: ## Inicia todos os serviços (docker compose up)
	@echo "$(CYAN)A iniciar todos os serviços...$(RESET)"
	@test -f .env || (echo "$(YELLOW)AVISO: .env não encontrado. Copia .env.example para .env e preenche os valores.$(RESET)" && exit 1)
	$(COMPOSE) up -d
	@echo "$(GREEN)Serviços iniciados!$(RESET)"
	@echo "  Frontend:      http://localhost:3000"
	@echo "  Collector API: http://localhost:8080"
	@echo "  Analysis API:  http://localhost:8090"
	@echo "  RabbitMQ UI:   http://localhost:15672"
	@echo "  Swagger:       http://localhost:8080/swagger"

up-infra: ## Inicia apenas a infraestrutura (postgres, rabbitmq, redis)
	$(COMPOSE) up -d postgres rabbitmq redis
	@echo "$(GREEN)Infraestrutura iniciada!$(RESET)"

down: ## Para todos os serviços
	$(COMPOSE) down
	@echo "$(GREEN)Serviços parados.$(RESET)"

down-volumes: ## Para todos os serviços e apaga volumes (CUIDADO: apaga dados!)
	@echo "$(YELLOW)ATENÇÃO: Isto apaga todos os dados da BD, RabbitMQ e Redis!$(RESET)"
	@read -p "Tens a certeza? (s/N): " confirm && [ "$$confirm" = "s" ] || exit 1
	$(COMPOSE) down -v
	@echo "$(GREEN)Serviços parados e volumes apagados.$(RESET)"

restart: ## Reinicia todos os serviços
	$(COMPOSE) restart

logs: ## Mostra logs de todos os serviços (Ctrl+C para sair)
	$(COMPOSE) logs -f

logs-collector: ## Logs do C# Data Collector
	$(COMPOSE) logs -f data-collector

logs-engine: ## Logs do Python Analysis Engine
	$(COMPOSE) logs -f analysis-engine

logs-frontend: ## Logs do React Frontend
	$(COMPOSE) logs -f frontend

status: ## Estado de todos os containers
	$(COMPOSE) ps

# ─────────────────────────────────────────
# Base de dados
# ─────────────────────────────────────────
shell-db: ## Abre shell psql na base de dados
	$(COMPOSE) exec postgres psql -U $${POSTGRES_USER:-sba_user} -d $${POSTGRES_DB:-sportsbetting}

migrate: ## Aplica migrações pendentes
	@echo "$(CYAN)A aplicar migrações...$(RESET)"
	$(COMPOSE) exec data-collector dotnet ef database update
	@echo "$(GREEN)Migrações aplicadas.$(RESET)"

reset-db: ## Reset completo da BD (apaga e recria)
	@echo "$(YELLOW)ATENÇÃO: Isto apaga todos os dados da base de dados!$(RESET)"
	@read -p "Tens a certeza? (s/N): " confirm && [ "$$confirm" = "s" ] || exit 1
	$(COMPOSE) stop data-collector analysis-engine
	$(COMPOSE) exec postgres psql -U $${POSTGRES_USER:-sba_user} -c "DROP DATABASE IF EXISTS $${POSTGRES_DB:-sportsbetting};"
	$(COMPOSE) exec postgres psql -U $${POSTGRES_USER:-sba_user} -c "CREATE DATABASE $${POSTGRES_DB:-sportsbetting};"
	$(COMPOSE) start data-collector analysis-engine
	@echo "$(GREEN)Base de dados recriada.$(RESET)"

seed: ## Insere dados de configuração default (tabela config)
	@echo "$(CYAN)A inserir seed data...$(RESET)"
	$(COMPOSE) exec postgres psql -U $${POSTGRES_USER:-sba_user} -d $${POSTGRES_DB:-sportsbetting} \
		-f /docker-entrypoint-initdb.d/seed.sql
	@echo "$(GREEN)Seed data inserido.$(RESET)"

backup-db: ## Faz backup da BD para ./backups/
	@mkdir -p backups
	$(COMPOSE) exec postgres pg_dump -U $${POSTGRES_USER:-sba_user} $${POSTGRES_DB:-sportsbetting} \
		> backups/backup_$$(date +%Y%m%d_%H%M%S).sql
	@echo "$(GREEN)Backup criado em backups/$(RESET)"

# ─────────────────────────────────────────
# RabbitMQ
# ─────────────────────────────────────────
shell-rabbit: ## Abre shell no container RabbitMQ
	$(COMPOSE) exec rabbitmq bash

rabbit-queues: ## Lista filas e mensagens pendentes
	$(COMPOSE) exec rabbitmq rabbitmqctl list_queues name messages consumers

# ─────────────────────────────────────────
# Build
# ─────────────────────────────────────────
build-collector: ## Build da imagem Docker do C# Collector
	$(COMPOSE) build data-collector

build-engine: ## Build da imagem Docker do Python Engine
	$(COMPOSE) build analysis-engine

build-frontend: ## Build da imagem Docker do React Frontend
	$(COMPOSE) build frontend

build-all: ## Build de todas as imagens Docker
	$(COMPOSE) build

# ─────────────────────────────────────────
# Testes
# ─────────────────────────────────────────
test-collector: ## Corre testes do C# Data Collector
	@echo "$(CYAN)A correr testes C#...$(RESET)"
	cd src/DataCollector && dotnet test --logger "console;verbosity=normal"

test-engine: ## Corre testes do Python Analysis Engine
	@echo "$(CYAN)A correr testes Python...$(RESET)"
	cd src/analysis-engine && python -m pytest -v

test-frontend: ## Corre testes do React Frontend
	@echo "$(CYAN)A correr testes React...$(RESET)"
	cd src/frontend && npm run test

test-all: test-collector test-engine test-frontend ## Corre todos os testes
	@echo "$(GREEN)Todos os testes passaram!$(RESET)"

test-integration: ## Corre testes de integração (requer APIs externas configuradas)
	@echo "$(YELLOW)Testes de integração — requer API keys válidas no .env$(RESET)"
	cd src/DataCollector && dotnet test --filter "Category=Integration"
	cd src/analysis-engine && python -m pytest -v -m integration

# ─────────────────────────────────────────
# Linting
# ─────────────────────────────────────────
lint-collector: ## Lint do C# (dotnet format + analyzers)
	cd src/DataCollector && dotnet format --verify-no-changes

lint-engine: ## Lint do Python (ruff + mypy)
	cd src/analysis-engine && ruff check . && mypy app --strict

lint-frontend: ## Lint do React (eslint + prettier)
	cd src/frontend && npm run lint && npm run format:check

lint-all: lint-collector lint-engine lint-frontend ## Lint em todos os serviços
	@echo "$(GREEN)Lint passou em todos os serviços!$(RESET)"

# ─────────────────────────────────────────
# Ollama (LLM local)
# ─────────────────────────────────────────
ollama-pull: ## Descarrega o modelo Ollama definido em LLM_MODEL
	@MODEL=$${LLM_MODEL:-llama3.1:8b}; \
	echo "$(CYAN)A descarregar modelo Ollama: $$MODEL$(RESET)"; \
	ollama pull $$MODEL; \
	echo "$(GREEN)Modelo $$MODEL pronto!$(RESET)"

ollama-list: ## Lista modelos Ollama disponíveis localmente
	ollama list

ollama-test: ## Testa que o Ollama está a responder
	@curl -s http://$${OLLAMA_BASE_URL:-localhost:11434}/api/tags | python3 -m json.tool

# ─────────────────────────────────────────
# OpenCode
# ─────────────────────────────────────────
opencode: ## Inicia o OpenCode CLI no projecto
	@echo "$(CYAN)A iniciar OpenCode...$(RESET)"
	@test -f opencode.json || (echo "$(YELLOW)opencode.json não encontrado!$(RESET)" && exit 1)
	opencode

opencode-install: ## Instala o OpenCode CLI globalmente
	npm install -g opencode-ai@latest
	@echo "$(GREEN)OpenCode instalado! Versão: $$(opencode --version)$(RESET)"

# ─────────────────────────────────────────
# Setup inicial
# ─────────────────────────────────────────
setup: ## Setup completo do projecto (primeira vez)
	@echo "$(CYAN)Setup inicial do Sports Betting AI...$(RESET)"
	@echo ""
	@echo "1. A verificar dependências..."
	@command -v docker >/dev/null 2>&1 || (echo "$(YELLOW)Docker não encontrado. Instala em https://docker.com$(RESET)" && exit 1)
	@command -v node >/dev/null 2>&1 || (echo "$(YELLOW)Node.js não encontrado. Instala em https://nodejs.org$(RESET)" && exit 1)
	@echo "   $(GREEN)Docker e Node.js encontrados$(RESET)"
	@echo ""
	@echo "2. A criar .env a partir de .env.example..."
	@test -f .env && echo "   $(YELLOW).env já existe, a ignorar$(RESET)" || (cp .env.example .env && echo "   $(GREEN).env criado — preenche as API keys!$(RESET)")
	@echo ""
	@echo "3. A instalar OpenCode..."
	@command -v opencode >/dev/null 2>&1 && echo "   $(GREEN)OpenCode já instalado$(RESET)" || (npm install -g opencode-ai@latest && echo "   $(GREEN)OpenCode instalado$(RESET)")
	@echo ""
	@echo "4. A iniciar infraestrutura..."
	$(COMPOSE) up -d postgres rabbitmq redis
	@echo ""
	@echo "$(GREEN)Setup concluído!$(RESET)"
	@echo ""
	@echo "Próximos passos:"
	@echo "  1. Edita o ficheiro .env com as tuas API keys"
	@echo "  2. Se usas Ollama: make ollama-pull"
	@echo "  3. Inicia tudo: make up"
	@echo "  4. Abre o OpenCode: make opencode"
