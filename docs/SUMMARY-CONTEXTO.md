# SUMMARY-CONTEXTO.md

Este ficheiro agrega um resumo operacional do projeto e aponta para o contexto de domínio.
É uma “âncora” rápida para sessões de desenvolvimento.

## Resumo do projeto

Ver `docs/SUMMARY.md`.

## Contexto de domínio (regras de negócio)

Ver `docs/CONTEXT.md`.

## Últimas alterações relevantes (2026-03-30)

- **Frontend**
  - Modal `Registar Aposta` (`StakeModal`) centrado via wrapper flex para evitar conflito de `transform` com animações (posição incorreta corrigida).
  - `StakeModal` agora é renderizado via **portal para o `document.body`** e suporta **scroll/altura máxima** para não ficar fora do ecrã em viewports pequenos.
  - Filtros avançados no `Dashboard` corrigidos para usar `rec.odd` (com fallback para `odd_decimal`) — agora filtros de odds funcionam.
  - `Dashboard`: normalização do payload de `/analysis/recommendations` para o shape de `Analysis` (`recommendedMarket`) para que o `MatchCard` consiga mostrar claramente **qual** é a recomendação. Contador de “Recomendações” agora conta apenas recomendações visíveis na lista de jogos.
  - `Histórico`: tratamento defensivo de datas (`settledAt`) nulas para evitar crashes ao renderizar/ordenar.
  - `Odds`: secção “Recomendação do Modelo” agora tolera payloads sem `odd_decimal`/`stake_euros` (fallbacks), evitando crash ao renderizar `.toFixed()`.
  - `Odds`: lista de jogos passa a mostrar apenas matches com odds (backend: `GET /api/matches/upcoming?onlyWithOdds=true`) e os odds inválidos são ignorados para evitar `toFixed` em `undefined`.
 - `Odds`: corrigido erro TS `JSX expressions must have one parent element` em `pages/odds/index.tsx` (envolvimento num fragment `<>...</>`), desbloqueando build no Docker.

- **Data Collector (C#)**
  - `TeamStatsService`: corrigida a chamada ao football-data.org para usar o `teamId` numérico (remove prefixo `football-`), permitindo preencher estatísticas de futebol.
  - `TeamStatsService`: implementadas estatísticas de **ténis** (form + head-to-head) via BD (últimos jogos `FINISHED`), para deixar de devolver DTOs vazios.
  - `MatchRepository`: matching de nomes melhorado para **ténis** (abreviações tipo `A. Zakharova` vs `Anastasia Zakharova`, e pares com `/`) para que o `OddsCollectorJob` associe odds ao match existente em vez de criar duplicados `odds-*`.
  - `BetsController`: adicionado endpoint `GET /api/bets/pending` e correção de nomes de equipas/jogadores no DTO (usa `Match.HomeTeam/AwayTeam` em vez de placeholders). `BetRepository` agora inclui as relações necessárias.
  - `TeamStatsService`: melhorado lookup de equipa de futebol com match relaxado (ILike/contains) e correção de precedence ao resolver o nome do adversário; endpoint `/api/teams/form` volta a devolver histórico em vez de 500.
  - `MatchesController/MatchRepository`: suportado `onlyWithOdds=true` no endpoint de upcoming matches para a UI filtrar jogos sem odds.
  - Odds:
    - Descoberta dinâmica de sport keys de ténis via Odds API `/sports` (keys mudam por torneio/época).
    - Inserção de odds alterada para **replace por match** para evitar falhas por constraint unique.
    - Para ténis, se o match não existir, é criado a partir do evento de odds (ex.: “WTA Charleston Open”), permitindo odds de ténis aparecerem na UI.

## Últimas alterações relevantes (2026-03-31)

- **Históricos de ténis (Jeff Sackmann)**
  - Seed `python db/seeds/load_historical.py --sport tennis` executado com sucesso, populando `player_elo_history` e a view `latest_player_elo`.
- **Data Collector (C#)**
  - `TeamsController` (`GET /api/teams/form?sport=tennis`): quando não há jogos `FINISHED` suficientes na tabela `matches`, passa a devolver “form” a partir do histórico `player_elo_history` (últimos 10 registos), desbloqueando estatísticas no modal para jogos de ténis.
  - `PlayersController` (`GET /api/players/tennis/stats`): novo endpoint para estatísticas de ténis por nome, devolve **ELO por superfície** (view `latest_player_elo`) + **resultados recentes W/L** (tabela `player_elo_history`), com matching para nomes abreviados (`J. Apelido`).
- **Infra / Docker Compose**
  - `frontend` passou a usar o mesmo `INTERNAL_API_KEY` (via `VITE_API_KEY`) para autenticar chamadas ao `DataCollector` com header `X-API-Key`, evitando erros ao chamar `/api/matches/upcoming`.
- **Frontend**
  - Páginas `Hoje` e `Futuro`: adicionado botão **Análise** em cada jogo, que chama o endpoint do `analysis-engine` e mostra o resultado num modal.
- **Analysis Engine (Python)**
  - Análise on-demand (`POST /analysis/match/{matchId}`): prompt de **ténis** agora inclui **forma recente (W/L)** e **H2H** quando existirem jogos `FINISHED` na tabela `matches` (além do ELO por superfície).

## Últimas alterações relevantes (2026-04-01)

- **Analysis Engine (Python)**
  - A resposta de análise passou a ser **auditável e determinística**: antes de qualquer recomendação, o motor calcula **probabilidades (stats)**, lê **odds**, calcula **implied probability**, **value**, **Kelly** e **stake**, e só depois decide **boa aposta / má aposta / no bet**.
  - Para **ténis**, o fallback por odds foi corrigido para **2-way `h2h` (sem empate)**; a explicação (`reasoning`) agora inclui claramente os mercados “bons” e “maus” com value e probabilidades.
  - O LLM (quando configurado) passa a ser usado apenas para um **resumo curto** baseado nos números calculados, nunca como fonte de decisão.
  - **Sem odds suficientes na BD** (lista vazia, ou ténis sem `h2h` completo para ambos os jogadores): o sistema **não** chama o LLM para resumo, declara explicitamente que **não é possível calcular value** e **proíbe preços inventados** — evita respostas tipo “suponhamos odd 1.80…”.
  - Variável `LLM_ANALYSIS_SUMMARY` (`.env.example`) permite desligar totalmente o resumo por LLM.
  - Correção do sanitizador de resumo LLM: regex de remoção de blocos ``` passou a usar `\s`/`\S` corretos (antes não limpava bem).

- **504 em `/api/matches/upcoming` (Nginx no frontend)**
  - Causa provável: **timeout do proxy** no `location /api/` (default ~60s) enquanto o collector/Postgres demorava; o `location /analysis/` já tinha 300s.
  - `frontend` Dockerfile: `proxy_read_timeout` / `proxy_send_timeout` **120s** também em `/api/`.
- **Data Collector — `GetUpcomingAsync`**
  - O parâmetro **`from` da API era ignorado** na query (só se aplicava `to` + regra de “agora −30 min”), o que podia aumentar linhas avaliadas e confundir filtros da UI.
  - Corrigido: janela inferior para agendados = `from` quando `from` é futuro; caso contrário combina `from` com tolerância de 30 min (atrasos).
  - Leituras com **`AsNoTracking()`** para menos custo no EF.
  - Parâmetro opcional **`asOfUtc`** (testes) + testes xUnit com EF InMemory em `MatchRepositoryGetUpcomingTests`.

## Últimas alterações relevantes (2026-04-02)

- **Analysis Engine (Python)**
  - **Gates de recomendação (futebol):** se as probabilidades 1X2 forem **placeholder** (faltam linhas 1/X/2 nas odds), `data_source=placeholder_1x2` e **não** se emite recomendação nem alternativas baseadas nesse vector.
  - **Underdog longo:** apostas **1X2** com odd ≥ `FOOTBALL_LONGSHOT_MIN_ODD` (default 6.0, `.env.example`) só são recomendadas se a fonte for **`xgboost`** ou **`poisson_historical`** — evita “value” espúrio com `implied_probability` ou outras fontes fracas.
  - **Força relativa no ML de futebol:** `elo_diff` no feature vector passa a usar **`strength_ratings_from_recent_games`** (saldo de golos em `historical_matches`), em vez de 1500/1500 fixo — proxy de equipa, distinto do ELO de ténis.
  - Testes: `tests/test_recommendation_gate.py`, `tests/test_strength_ratings.py`.
  - `POST /analysis/match/{match_id}` aceita query **`force_refresh=true`** para ignorar o cache Redis do resumo opcional por LLM (após nova geração o TTL em Redis é renovado).
- **Frontend**
  - `MatchAnalysisModal`: mostra **fonte das probabilidades** (`dataSource`); aviso visível para `placeholder_1x2`; título da caixa de texto passou de “Resumo da LLM” para **Análise** (o conteúdo inclui raciocínio determinístico + opcional LLM).
  - `MatchAnalysisModal`: botão **Nova análise** usa `force_refresh=true`; probabilidades e value são sempre recalculados no pedido.
- **Seeds**
  - `db/seeds/load_historical.py` carrega **`.env` na raiz do repo** com `python-dotenv` antes de validar `POSTGRES_*` (execução local sem export manual). Dependência: `pip install python-dotenv`.

## Últimas alterações relevantes (2026-04-02 — H2H futebol e duplicados históricos)

- **Data Collector — confrontos diretos (futebol):** o H2H deixou de depender só da football-data.org com **nomes exactos** (ex.: API/UI “Real Sociedad de Fútbol” vs CSV “Sociedad”). Passa a usar primeiro **`historical_matches`** com `strpos`/substring case-insensitive e **`DISTINCT ON`** para ignorar linhas repetidas; fallback para a API se não houver linhas. Corrigida também a soma de **golos por equipa da UI** no caminho API (antes somava sempre casa/fora do fixture).
- **BD:** migração `V014__dedupe_historical_matches.sql` apaga duplicados exactos (mesmo `home_team`, `away_team`, `match_date`, `league_code`, `season`); usa `ctid` quando a tabela não tem coluna `id`.
- **Seed football:** `load_historical.py` normaliza **datas para `YYYY-MM-DD`**, faz `strip` aos nomes e **`drop_duplicates`** no lote antes do `to_sql` (alinha com o índice único e reduz re-inserções).
- **Frontend:** `HeadToHeadStats` usa **“1 jogo”** vs **“N jogos”** em português.

## Últimas alterações relevantes (2026-04-02 — API ténis / `historical_tennis_matches`)

- **Data Collector:** `HistoricalTennisMatchEntity` e `PlayersController` alinhados ao schema real da tabela `historical_tennis_matches` (V015, fonte tennis-data.co.uk: `winner`/`loser`/`match_date`, sets `w1`–`l5`, etc.). A entidade anterior assumia colunas estilo Sackmann (`winner_name`, `tourney_date`, stats de serviço) **inexistentes** na BD → o EF gerava SQL inválido e `GET /api/players/tennis/stats` respondia **500**. `GET /api/players/tennis/h2h`: contagens de vitórias deixam de usar `EF.Functions.ILike` sobre listas já materializadas (não suportado fora de `IQueryable`). `ServeStats` por superfície fica vazio (esta fonte não traz aces/`svpt` por jogo).
- **LatestPlayerEloEntity:** `last_match_date` mapeado como `string?` (TEXT na view), coerente com `player_elo_history`.
- **Analysis Engine (Python):** `tennis_context.py` — queries a `historical_tennis_matches` actualizadas para `winner`/`loser`/`match_date`/`tournament`; `get_tennis_serve_stats` devolve vazio (sem colunas de serviço na fonte actual).

## Docker vs desenvolvimento local (frontend)

O serviço `frontend` no Docker **não** monta o código-fonte: a imagem corre `npm run build` e o Nginx serve o `dist` **da altura do último `docker compose build frontend`**.

- Se alterares `.tsx`/hooks mas **não** reconstruíres a imagem, o browser em `localhost:3000` (via Compose) pode continuar a mostrar um **bundle antigo** — parecem “bugs” que já corrigiste no disco.
- Para ver alterações de UI no Docker: `docker compose build frontend` (ou `docker compose up -d --build frontend`) após mudanças.
- `npm run dev` (Vite na máquina) usa o código atual **sem** depender desse rebuild; só garante consistência com Docker se também reconstruíres a imagem.

