-- Initial database schema for Sports Betting AI
-- Run this script to create all necessary tables

-- Teams table
CREATE TABLE IF NOT EXISTS teams (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_id VARCHAR(100) NOT NULL,
    sport VARCHAR(50) NOT NULL,
    name VARCHAR(200) NOT NULL,
    short_name VARCHAR(50),
    country VARCHAR(100),
    competition_id UUID,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(external_id, sport)
);

CREATE INDEX IF NOT EXISTS idx_teams_external_sport ON teams(external_id, sport);

-- Competitions table
CREATE TABLE IF NOT EXISTS competitions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_id VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    sport VARCHAR(50) NOT NULL,
    country VARCHAR(100),
    season VARCHAR(20),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Players table
CREATE TABLE IF NOT EXISTS players (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_id VARCHAR(100) NOT NULL,
    sport VARCHAR(50) NOT NULL,
    name VARCHAR(200) NOT NULL,
    country VARCHAR(100),
    team_id UUID,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Matches table
CREATE TABLE IF NOT EXISTS matches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_id VARCHAR(100) NOT NULL,
    sport VARCHAR(50) NOT NULL,
    competition_id UUID,
    home_id UUID NOT NULL,
    away_id UUID NOT NULL,
    commence_time TIMESTAMP WITH TIME ZONE NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'SCHEDULED',
    home_score INTEGER,
    away_score INTEGER,
    home_xg REAL,
    away_xg REAL,
    minute INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(external_id, sport)
);

CREATE INDEX IF NOT EXISTS idx_matches_external_sport ON matches(external_id, sport);
CREATE INDEX IF NOT EXISTS idx_matches_commence_time ON matches(commence_time);
CREATE INDEX IF NOT EXISTS idx_matches_status ON matches(status);

-- Odds table
CREATE TABLE IF NOT EXISTS odds (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL REFERENCES matches(id) ON DELETE CASCADE,
    bookmaker VARCHAR(100) NOT NULL,
    market VARCHAR(50) NOT NULL,
    outcome VARCHAR(50) NOT NULL,
    odd_decimal DECIMAL(10, 4) NOT NULL,
    implied_probability DECIMAL(5, 4),
    captured_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(match_id, bookmaker, market, outcome)
);

CREATE INDEX IF NOT EXISTS idx_odds_match_bookmaker ON odds(match_id, bookmaker, market);

-- Recommendations table
CREATE TABLE IF NOT EXISTS recommendations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL REFERENCES matches(id) ON DELETE CASCADE,
    market VARCHAR(50) NOT NULL,
    outcome VARCHAR(50) NOT NULL,
    bookmaker VARCHAR(100),
    odd_decimal DECIMAL(10, 4) NOT NULL,
    model_probability DECIMAL(5, 4) NOT NULL,
    implied_probability DECIMAL(5, 4),
    value DECIMAL(5, 4),
    kelly_fraction DECIMAL(5, 4),
    stake_euros DECIMAL(10, 2),
    confidence INTEGER CHECK (confidence >= 1 AND confidence <= 10),
    reasoning TEXT,
    status VARCHAR(50) DEFAULT 'PENDING',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_recommendations_match ON recommendations(match_id);
CREATE INDEX IF NOT EXISTS idx_recommendations_status ON recommendations(status);

-- Bets table (for tracking placed bets)
CREATE TABLE IF NOT EXISTS bets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    recommendation_id UUID REFERENCES recommendations(id),
    match_id UUID NOT NULL REFERENCES matches(id),
    market VARCHAR(50) NOT NULL,
    outcome VARCHAR(50) NOT NULL,
    bookmaker VARCHAR(100),
    odd_placed DECIMAL(10, 4) NOT NULL,
    stake_euros DECIMAL(10, 2) NOT NULL,
    result VARCHAR(20), -- PENDING, WIN, LOSS, VOID
    profit_loss DECIMAL(10, 2),
    placed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    settled_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX IF NOT EXISTS idx_bets_match ON bets(match_id);
CREATE INDEX IF NOT EXISTS idx_bets_result ON bets(result);

-- Performance stats materialized view (optional, for performance)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_performance_stats AS
SELECT 
    COUNT(*) as total_bets,
    SUM(CASE WHEN result = 'WIN' THEN 1 ELSE 0 END) as winning_bets,
    SUM(CASE WHEN result = 'LOSS' THEN 1 ELSE 0 END) as losing_bets,
    SUM(profit_loss) as total_profit_loss,
    AVG(CASE WHEN result = 'WIN' THEN 1.0 ELSE 0.0 END) as win_rate,
    SUM(profit_loss) / NULLIF(SUM(stake_euros), 0) as roi
FROM bets
WHERE result IN ('WIN', 'LOSS');

-- Function to refresh materialized view
CREATE OR REPLACE FUNCTION refresh_performance_stats()
RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW mv_performance_stats;
END;
$$ LANGUAGE plpgsql;
