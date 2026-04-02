-- V017__add_tennis_name_normalization.sql
-- Normalização canónica de nomes de jogadores de ténis
--
-- Problema: dois formatos coexistem na BD
--   historical_tennis_matches : "Djokovic N."  (tennis-data.co.uk)
--   teams (sport='tennis')    : "Novak Djokovic" (api-tennis.com)
--
-- Solução: coluna *_normalized com forma canónica "surname initial"
--   "Djokovic N."    → "djokovic n"
--   "Novak Djokovic" → "djokovic n"
--   "De Minaur A."   → "de minaur a"
-- As queries usam o campo normalizado para match exacto + ILIKE como fallback.

-- ─────────────────────────────────────────────────────────────
-- Função de normalização (IMMUTABLE → pode ser indexada)
-- ─────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION normalize_tennis_name(p_name TEXT)
RETURNS TEXT
LANGUAGE plpgsql IMMUTABLE RETURNS NULL ON NULL INPUT
AS $$
DECLARE
    parts        TEXT[];
    part         TEXT;
    singles      TEXT[] := ARRAY[]::TEXT[];
    non_singles  TEXT[] := ARRAY[]::TEXT[];
BEGIN
    p_name := trim(p_name);
    IF p_name = '' THEN RETURN ''; END IF;

    -- Dividir por espaço e remover pontos finais
    FOREACH part IN ARRAY string_to_array(p_name, ' ') LOOP
        part := trim(trailing '.' from trim(part));
        IF part <> '' THEN
            IF length(part) = 1 THEN
                singles := singles || lower(part);
            ELSE
                non_singles := non_singles || lower(part);
            END IF;
        END IF;
    END LOOP;

    -- Caso sem tokens úteis
    IF array_length(non_singles, 1) IS NULL THEN
        RETURN lower(p_name);
    END IF;

    -- Tem inicial explícita: "Djokovic N." ou "De Minaur A." ou "N. Djokovic"
    IF array_length(singles, 1) IS NOT NULL THEN
        RETURN array_to_string(non_singles, ' ') || ' ' || singles[1];
    END IF;

    -- Nome completo sem inicial: "Novak Djokovic" → último token = apelido, 1ª letra = inicial
    RETURN non_singles[array_length(non_singles, 1)]
        || ' '
        || substring(non_singles[1], 1, 1);
END;
$$;

-- ─────────────────────────────────────────────────────────────
-- historical_tennis_matches — colunas normalizadas
-- ─────────────────────────────────────────────────────────────
ALTER TABLE historical_tennis_matches
    ADD COLUMN IF NOT EXISTS winner_normalized TEXT,
    ADD COLUMN IF NOT EXISTS loser_normalized  TEXT;

-- Backfill dados existentes
UPDATE historical_tennis_matches
SET winner_normalized = normalize_tennis_name(winner),
    loser_normalized  = normalize_tennis_name(loser)
WHERE winner_normalized IS NULL;

-- Índices para lookup exacto por nome normalizado
CREATE INDEX IF NOT EXISTS idx_tennis_hist_winner_norm
    ON historical_tennis_matches (winner_normalized, surface, match_date);

CREATE INDEX IF NOT EXISTS idx_tennis_hist_loser_norm
    ON historical_tennis_matches (loser_normalized, surface, match_date);

-- Índice para H2H exact match
CREATE INDEX IF NOT EXISTS idx_tennis_hist_h2h_norm
    ON historical_tennis_matches (winner_normalized, loser_normalized, match_date DESC);

-- ─────────────────────────────────────────────────────────────
-- teams — coluna normalizada (ténis e futebol)
-- ─────────────────────────────────────────────────────────────
ALTER TABLE teams
    ADD COLUMN IF NOT EXISTS name_normalized TEXT;

-- Backfill só para ténis (futebol usa nome completo e não precisa desta lógica)
UPDATE teams
SET name_normalized = normalize_tennis_name(name)
WHERE sport = 'tennis'
  AND name_normalized IS NULL;

-- Futebol: normalização simples (lowercase trim)
UPDATE teams
SET name_normalized = lower(trim(name))
WHERE sport = 'football'
  AND name_normalized IS NULL;

CREATE INDEX IF NOT EXISTS idx_teams_name_norm
    ON teams (name_normalized, sport);
