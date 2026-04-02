-- V016__create_standings_rankings.sql
-- Tabelas para classificações de futebol e rankings de ténis.
-- Actualizadas de 2 em 2 dias via StandingsCollectorJob (Hangfire).
-- Lidas pela LLM para contextualizar análises (posição, pontos, forma).

-- ─────────────────────────────────────────
-- Tabela: football_standings
-- Fonte: football-data.org /competitions/{id}/standings
-- Guarda a tabela classificativa mais recente de cada liga.
-- ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS football_standings (
    id                  BIGSERIAL PRIMARY KEY,
    competition_code    VARCHAR(10) NOT NULL,   -- PL, PD, BL1, SA, FL1, PPL, CL, ...
    competition_name    VARCHAR(100),
    season              VARCHAR(10) NOT NULL,   -- ex: 2024/25
    stage               VARCHAR(50) DEFAULT 'REGULAR_SEASON',
    position            SMALLINT NOT NULL,
    team_name           TEXT NOT NULL,
    team_external_id    INTEGER,
    played              SMALLINT DEFAULT 0,
    won                 SMALLINT DEFAULT 0,
    draw                SMALLINT DEFAULT 0,
    lost                SMALLINT DEFAULT 0,
    goals_for           SMALLINT DEFAULT 0,
    goals_against       SMALLINT DEFAULT 0,
    goal_difference     SMALLINT DEFAULT 0,
    points              SMALLINT DEFAULT 0,
    form                VARCHAR(20),            -- ex: "WWDLW" (últimas 5)
    fetched_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_standings_comp_season ON football_standings (competition_code, season, fetched_at DESC);
CREATE INDEX idx_standings_team       ON football_standings (team_name, competition_code);

-- Vista com a classificação mais recente por liga
CREATE OR REPLACE VIEW latest_football_standings AS
SELECT DISTINCT ON (competition_code, position)
    competition_code, competition_name, season, stage,
    position, team_name, team_external_id,
    played, won, draw, lost,
    goals_for, goals_against, goal_difference, points, form,
    fetched_at
FROM football_standings
ORDER BY competition_code, position, fetched_at DESC;


-- ─────────────────────────────────────────
-- Tabela: tennis_rankings
-- Fonte: api-tennis.com /rankings
-- Guarda o ranking ATP e WTA mais recente.
-- ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS tennis_rankings (
    id                  BIGSERIAL PRIMARY KEY,
    tour                VARCHAR(5) NOT NULL,    -- ATP / WTA
    rank                INTEGER NOT NULL,
    player_name         TEXT NOT NULL,
    player_key          BIGINT,                 -- ID externo da API
    country             VARCHAR(3),             -- código IOC (ex: ESP, USA)
    points              INTEGER,
    fetched_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_tennis_rank_tour_date ON tennis_rankings (tour, fetched_at DESC);
CREATE INDEX idx_tennis_rank_player    ON tennis_rankings (player_name, tour);

-- Vista com o ranking mais recente por tour
CREATE OR REPLACE VIEW latest_tennis_rankings AS
SELECT DISTINCT ON (tour, rank)
    tour, rank, player_name, player_key, country, points, fetched_at
FROM tennis_rankings
ORDER BY tour, rank, fetched_at DESC;
