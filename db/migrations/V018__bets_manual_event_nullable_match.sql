-- Apostas manuais sem jogo na tabela matches: match_id opcional + texto livre
ALTER TABLE bets ALTER COLUMN match_id DROP NOT NULL;

ALTER TABLE bets ADD COLUMN IF NOT EXISTS manual_event_label VARCHAR(500);
ALTER TABLE bets ADD COLUMN IF NOT EXISTS manual_sport VARCHAR(32);

COMMENT ON COLUMN bets.manual_event_label IS 'Descrição livre do evento quando match_id é NULL';
COMMENT ON COLUMN bets.manual_sport IS 'football | tennis | manual (Outro)';
