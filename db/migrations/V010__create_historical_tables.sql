-- V010__create_historical_tables.sql
-- Tabelas para dados históricos das fontes externas gratuitas.
-- Carregadas uma vez via: python db/seeds/load_historical.py

-- ─────────────────────────────────────────
-- Tabela: historical_matches
-- Fonte: football-data.co.uk CSVs
-- Contém resultados históricos de 40+ ligas desde 1993.
-- Usada para calibrar modelo Poisson (lambda golos por equipa).
-- ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS historical_matches (
    id                  BIGSERIAL PRIMARY KEY,
    match_date          TEXT,               -- formato variável conforme fonte
    home_team           TEXT NOT NULL,
    away_team           TEXT NOT NULL,
    home_goals          SMALLINT,
    away_goals          SMALLINT,
    result              CHAR(1),            -- H / D / A
    home_shots          SMALLINT,
    away_shots          SMALLINT,
    home_shots_target   SMALLINT,
    away_shots_target   SMALLINT,
    home_corners        SMALLINT,
    away_corners        SMALLINT,
    home_yellows        SMALLINT,
    away_yellows        SMALLINT,
    home_reds           SMALLINT,
    away_reds           SMALLINT,
    league_code         VARCHAR(10) NOT NULL,
    league_name         VARCHAR(100),
    season              VARCHAR(6) NOT NULL, -- ex: 2324
    source              VARCHAR(50) DEFAULT 'football-data.co.uk',
    created_at          TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_historical_matches_home_team
    ON historical_matches (home_team, league_code, season);

CREATE INDEX idx_historical_matches_away_team
    ON historical_matches (away_team, league_code, season);

CREATE INDEX idx_historical_matches_league_season
    ON historical_matches (league_code, season);

-- Evitar duplicados em re-carregamentos
CREATE UNIQUE INDEX idx_historical_matches_unique
    ON historical_matches (home_team, away_team, match_date, league_code, season);


-- ─────────────────────────────────────────
-- Tabela: historical_events
-- Fonte: StatsBomb Open Data (GitHub)
-- Contém eventos ao nível do remate com coordenadas e xG.
-- Usada para treinar e validar o modelo de xG.
-- ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS historical_events (
    id              BIGSERIAL PRIMARY KEY,
    match_id_sb     BIGINT NOT NULL,        -- ID do jogo no StatsBomb
    competition_id  INTEGER NOT NULL,
    season_id       INTEGER NOT NULL,
    home_team       TEXT,
    away_team       TEXT,
    minute          SMALLINT,
    second          SMALLINT,
    team            TEXT,
    player          TEXT,
    xg              DECIMAL(6, 4),          -- probabilidade de golo (0.00-1.00)
    outcome         VARCHAR(50),            -- Goal / Saved / Off Target / Blocked / etc.
    technique       VARCHAR(50),            -- Normal / Header / Volley / etc.
    location        JSONB,                  -- [x, y] no campo (0-120, 0-80)
    source          VARCHAR(50) DEFAULT 'statsbomb',
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_historical_events_match
    ON historical_events (match_id_sb, competition_id, season_id);

CREATE INDEX idx_historical_events_player
    ON historical_events (player, team);

CREATE INDEX idx_historical_events_xg
    ON historical_events (xg)
    WHERE xg IS NOT NULL;


-- ─────────────────────────────────────────
-- Tabela: player_elo_history
-- Fonte: Jeff Sackmann tennis_atp / tennis_wta (GitHub)
-- Contém histórico de ELO por jogador, superfície e data.
-- Usada para calcular ELO actual e tendência de forma.
-- ─────────────────────────────────────────
CREATE TABLE IF NOT EXISTS player_elo_history (
    id              BIGSERIAL PRIMARY KEY,
    player_name     TEXT NOT NULL,
    surface         VARCHAR(20) NOT NULL,   -- Hard / Clay / Grass / Carpet
    elo             DECIMAL(8, 1) NOT NULL,
    tour            VARCHAR(5),             -- ATP / WTA
    match_date      TEXT,                   -- formato YYYYMMDD do Sackmann
    won             BOOLEAN,
    source          VARCHAR(50) DEFAULT 'sackmann',
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_elo_history_player_surface
    ON player_elo_history (player_name, surface);

CREATE INDEX idx_elo_history_latest
    ON player_elo_history (player_name, surface, match_date DESC);


-- ─────────────────────────────────────────
-- View: latest_player_elo
-- ELO mais recente por jogador e superfície.
-- Usada directamente pelo Python Engine para obter ELO actual.
-- ─────────────────────────────────────────
CREATE OR REPLACE VIEW latest_player_elo AS
SELECT DISTINCT ON (player_name, surface)
    player_name,
    surface,
    elo,
    tour,
    match_date AS last_match_date
FROM player_elo_history
ORDER BY player_name, surface, match_date DESC NULLS LAST;


-- ─────────────────────────────────────────
-- View: team_historical_averages
-- Médias de golos por equipa e liga para calibração Poisson.
-- Usada quando equipa tem < 5 jogos na época actual.
-- ─────────────────────────────────────────
CREATE OR REPLACE VIEW team_historical_averages AS
SELECT
    home_team AS team_name,
    league_code,
    'home' AS venue,
    AVG(home_goals)         AS avg_goals_scored,
    AVG(away_goals)         AS avg_goals_conceded,
    STDDEV(home_goals)      AS stddev_goals_scored,
    COUNT(*)                AS sample_size,
    MAX(season)             AS latest_season
FROM historical_matches
WHERE home_goals IS NOT NULL
GROUP BY home_team, league_code

UNION ALL

SELECT
    away_team AS team_name,
    league_code,
    'away' AS venue,
    AVG(away_goals)         AS avg_goals_scored,
    AVG(home_goals)         AS avg_goals_conceded,
    STDDEV(away_goals)      AS stddev_goals_scored,
    COUNT(*)                AS sample_size,
    MAX(season)             AS latest_season
FROM historical_matches
WHERE away_goals IS NOT NULL
GROUP BY away_team, league_code;
