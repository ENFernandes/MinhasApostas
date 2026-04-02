-- TimescaleDB: converter tabelas de séries temporais em hypertables
-- Executar depois das tabelas já existirem (após V001..V012)
-- O TimescaleDB é um drop-in replacement do PostgreSQL — não altera nenhuma API existente.

CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;

-- Odds: dezenas de updates por dia por jogo → ideal para hypertable
-- Particiona por `collected_at` em chunks de 1 dia
SELECT create_hypertable(
    'odds',
    by_range('collected_at', INTERVAL '1 day'),
    if_not_exists => TRUE,
    migrate_data  => TRUE
);

-- Match stats: capturadas ao longo do jogo → ideal para análise temporal
SELECT create_hypertable(
    'match_stats',
    by_range('collected_at', INTERVAL '1 day'),
    if_not_exists => TRUE,
    migrate_data  => TRUE
);

-- Índice composto para queries de movimento de odds (steam detection)
CREATE INDEX IF NOT EXISTS idx_odds_match_time
    ON odds (match_id, collected_at DESC);

-- View para aceder rapidamente ao histórico de flutuação de odds de um jogo
CREATE OR REPLACE VIEW odds_movement AS
SELECT
    match_id,
    bookmaker,
    market,
    outcome,
    odd_decimal,
    collected_at,
    odd_decimal - LAG(odd_decimal) OVER (
        PARTITION BY match_id, bookmaker, market, outcome
        ORDER BY collected_at
    ) AS odd_change
FROM odds
ORDER BY match_id, bookmaker, market, outcome, collected_at;
