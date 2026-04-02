-- V015__create_historical_tennis_matches.sql
-- Tabela de jogos históricos de ténis (ATP + WTA)
-- Fonte: tennis-data.co.uk (ficheiros .xlsx anuais)
-- Carregada via: python db/seeds/load_historical.py --sport tennis
--
-- ATP:  http://www.tennis-data.co.uk/{YEAR}/{YEAR}.xlsx
-- WTA:  http://www.tennis-data.co.uk/{YEAR}w/{YEAR}w.xlsx

CREATE TABLE IF NOT EXISTS historical_tennis_matches (
    id              BIGSERIAL PRIMARY KEY,

    -- Torneio
    location        TEXT,
    tournament      TEXT,
    match_date      DATE,
    series_tier     VARCHAR(30),        -- ATP: Series (ATP250, ATP500, Grand Slam…) / WTA: Tier (WTA250, WTA500…)
    court           VARCHAR(20),        -- Outdoor / Indoor
    surface         VARCHAR(20),        -- Hard / Clay / Grass / Carpet

    -- Jogo
    match_num       SMALLINT,           -- número sequencial do jogo no ficheiro
    round           TEXT,               -- 1st Round, 2nd Round, QF, SF, F, …
    best_of         SMALLINT,           -- 3 ou 5

    -- Resultado
    winner          TEXT NOT NULL,
    loser           TEXT NOT NULL,
    winner_rank     INTEGER,
    loser_rank      INTEGER,
    winner_pts      INTEGER,
    loser_pts       INTEGER,

    -- Marcadores por set (ATP até 5 sets; WTA até 3 sets)
    w1              SMALLINT,   l1  SMALLINT,
    w2              SMALLINT,   l2  SMALLINT,
    w3              SMALLINT,   l3  SMALLINT,
    w4              SMALLINT,   l4  SMALLINT,   -- apenas ATP
    w5              SMALLINT,   l5  SMALLINT,   -- apenas ATP
    wsets           SMALLINT,
    lsets           SMALLINT,
    comment         TEXT,                       -- Completed / Retired / Walkover / Default

    -- Odds de mercado
    b365w           DECIMAL(8, 3),  b365l   DECIMAL(8, 3),
    psw             DECIMAL(8, 3),  psl     DECIMAL(8, 3),
    maxw            DECIMAL(8, 3),  maxl    DECIMAL(8, 3),
    avgw            DECIMAL(8, 3),  avgl    DECIMAL(8, 3),
    bfew            DECIMAL(8, 3),  bfel    DECIMAL(8, 3),

    -- Metadados
    tour            VARCHAR(5)  NOT NULL,               -- ATP / WTA
    source_year     SMALLINT,                           -- ano do ficheiro origem
    source          VARCHAR(50) DEFAULT 'tennis-data.co.uk',
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

-- Índices para queries de H2H e performance por jogador/superfície
CREATE INDEX idx_tennis_hist_winner   ON historical_tennis_matches (winner, surface, match_date);
CREATE INDEX idx_tennis_hist_loser    ON historical_tennis_matches (loser,  surface, match_date);
CREATE INDEX idx_tennis_hist_date     ON historical_tennis_matches (match_date DESC);
CREATE INDEX idx_tennis_hist_tour     ON historical_tennis_matches (tour, match_date DESC);
CREATE INDEX idx_tennis_hist_tourn    ON historical_tennis_matches (tournament, match_date DESC);

-- Fuzzy search por nome de jogador
CREATE INDEX idx_tennis_hist_winner_lower ON historical_tennis_matches (lower(winner) text_pattern_ops);
CREATE INDEX idx_tennis_hist_loser_lower  ON historical_tennis_matches (lower(loser)  text_pattern_ops);

-- Evitar duplicados em re-carregamentos
CREATE UNIQUE INDEX idx_tennis_hist_unique
    ON historical_tennis_matches (tour, tournament, match_date, winner, loser)
    WHERE match_date IS NOT NULL;
