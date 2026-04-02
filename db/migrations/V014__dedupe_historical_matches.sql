-- Remove exact duplicate rows in historical_matches (e.g. from multiple seed runs).
-- Schema legado pode não ter coluna id — usa ctid.
-- Preferir ROW_NUMBER em vez de DELETE ... USING (self-join) para evitar O(n²) em tabelas grandes.

DELETE FROM historical_matches
WHERE ctid IN (
    SELECT ctid
    FROM (
        SELECT ctid,
               ROW_NUMBER() OVER (
                   PARTITION BY home_team, away_team, match_date, league_code, season
                   ORDER BY ctid
               ) AS rn
        FROM historical_matches
    ) dedup
    WHERE dedup.rn > 1
);
