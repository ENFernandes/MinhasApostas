-- Ciclo de vida das recomendações: adiciona campos de resultado
-- O utilizador pode registar manualmente WON/LOST/VOID após o jogo

ALTER TABLE recommendations
    ADD COLUMN IF NOT EXISTS bet_outcome VARCHAR(10)
        CHECK (bet_outcome IN ('WON', 'LOST', 'VOID')),
    ADD COLUMN IF NOT EXISTS outcome_recorded_at TIMESTAMP WITH TIME ZONE,
    ADD COLUMN IF NOT EXISTS outcome_source VARCHAR(20) DEFAULT 'manual';
    -- 'manual' = utilizador registou | 'auto' = verificado via API de resultados

COMMENT ON COLUMN recommendations.bet_outcome IS 'Resultado real da aposta: WON, LOST ou VOID';
COMMENT ON COLUMN recommendations.outcome_recorded_at IS 'Quando o resultado foi registado';
COMMENT ON COLUMN recommendations.outcome_source IS 'Como o resultado foi registado: manual ou auto';

CREATE INDEX IF NOT EXISTS idx_recommendations_outcome ON recommendations(bet_outcome)
    WHERE bet_outcome IS NOT NULL;
