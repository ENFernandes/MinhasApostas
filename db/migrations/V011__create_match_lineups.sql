-- V011__create_match_lineups.sql
-- Tabela para guardar lineups confirmadas de jogos de futebol.
-- Preenchida pelo job FootballLineupsJob via api-football.

CREATE TABLE IF NOT EXISTS match_lineups (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id        UUID NOT NULL REFERENCES matches(id) ON DELETE CASCADE,
    team_id         UUID NOT NULL REFERENCES teams(id),
    formation       VARCHAR(10),                    -- "4-3-3", "4-4-2", etc.
    players         JSONB NOT NULL DEFAULT '[]'::jsonb,   -- [{name, position, number}]
    substitutes     JSONB NOT NULL DEFAULT '[]'::jsonb,
    confirmed_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(match_id, team_id)
);

CREATE INDEX idx_match_lineups_match_id ON match_lineups (match_id);
CREATE INDEX idx_match_lineups_confirmed_at ON match_lineups (confirmed_at DESC);
