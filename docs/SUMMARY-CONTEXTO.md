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

## Docker vs desenvolvimento local (frontend)

O serviço `frontend` no Docker **não** monta o código-fonte: a imagem corre `npm run build` e o Nginx serve o `dist` **da altura do último `docker compose build frontend`**.

- Se alterares `.tsx`/hooks mas **não** reconstruíres a imagem, o browser em `localhost:3000` (via Compose) pode continuar a mostrar um **bundle antigo** — parecem “bugs” que já corrigiste no disco.
- Para ver alterações de UI no Docker: `docker compose build frontend` (ou `docker compose up -d --build frontend`) após mudanças.
- `npm run dev` (Vite na máquina) usa o código atual **sem** depender desse rebuild; só garante consistência com Docker se também reconstruíres a imagem.

