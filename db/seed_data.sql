-- Script para popular a BD com dados de teste
-- Executar: docker exec -i sba-postgres psql -U sba_user -d sportsbetting < seed_data.sql

-- Inserir competicoes
INSERT INTO competitions (id, external_id, name, sport, country, season)
VALUES 
  ('11111111-1111-1111-1111-111111111111', 'premier-league', 'Premier League', 'football', 'England', '2024/2025'),
  ('22222222-2222-2222-2222-222222222222', 'la-liga', 'La Liga', 'football', 'Spain', '2024/2025'),
  ('33333333-3333-3333-3333-333333333333', 'serie-a', 'Serie A', 'football', 'Italy', '2024/2025'),
  ('44444444-4444-4444-4444-444444444444', 'wimbledon', 'Wimbledon', 'tennis', 'UK', '2025'),
  ('55555555-5555-5555-5555-555555555555', 'primeira-liga', 'Primeira Liga', 'football', 'Portugal', '2024/2025')
ON CONFLICT DO NOTHING;

-- Inserir equipas
INSERT INTO teams (id, external_id, sport, name, short_name, country, competition_id)
VALUES 
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'man-utd', 'football', 'Manchester United', 'MUN', 'England', '11111111-1111-1111-1111-111111111111'),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaab', 'liverpool', 'football', 'Liverpool', 'LIV', 'England', '11111111-1111-1111-1111-111111111111'),
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'real-madrid', 'football', 'Real Madrid', 'RMA', 'Spain', '22222222-2222-2222-2222-222222222222'),
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', 'barcelona', 'football', 'Barcelona', 'BAR', 'Spain', '22222222-2222-2222-2222-222222222222'),
  ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'juventus', 'football', 'Juventus', 'JUV', 'Italy', '33333333-3333-3333-3333-333333333333'),
  ('cccccccc-cccc-cccc-cccc-cccccccccccd', 'ac-milan', 'football', 'AC Milan', 'MIL', 'Italy', '33333333-3333-3333-3333-333333333333'),
  ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'alcaraz', 'tennis', 'Carlos Alcaraz', 'ALC', 'Spain', '44444444-4444-4444-4444-444444444444'),
  ('dddddddd-dddd-dddd-dddd-ddddddddddde', 'djokovic', 'tennis', 'Novak Djokovic', 'DJOK', 'Serbia', '44444444-4444-4444-4444-444444444444'),
  ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'benfica', 'football', 'Benfica', 'BEN', 'Portugal', '55555555-5555-5555-5555-555555555555'),
  ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeef', 'porto', 'football', 'Porto', 'POR', 'Portugal', '55555555-5555-5555-5555-555555555555')
ON CONFLICT DO NOTHING;

-- Inserir jogos de teste
INSERT INTO matches (id, external_id, sport, competition_id, home_id, away_id, commence_time, status, created_at, updated_at)
VALUES 
  ('10000000-0000-0000-0000-000000000001', 'test-match-1', 'football', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaab', NOW() + INTERVAL '2 hours', 'SCHEDULED', NOW(), NOW()),
  ('10000000-0000-0000-0000-000000000002', 'test-match-2', 'football', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc', NOW() + INTERVAL '4 hours', 'SCHEDULED', NOW(), NOW()),
  ('10000000-0000-0000-0000-000000000003', 'test-match-3', 'football', '33333333-3333-3333-3333-333333333333', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'cccccccc-cccc-cccc-cccc-cccccccccccd', NOW() + INTERVAL '6 hours', 'SCHEDULED', NOW(), NOW()),
  ('10000000-0000-0000-0000-000000000004', 'test-match-4', 'tennis', '44444444-4444-4444-4444-444444444444', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'dddddddd-dddd-dddd-dddd-ddddddddddde', NOW() + INTERVAL '8 hours', 'SCHEDULED', NOW(), NOW()),
  ('10000000-0000-0000-0000-000000000005', 'test-match-5', 'football', '55555555-5555-5555-5555-555555555555', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeef', NOW() + INTERVAL '10 hours', 'SCHEDULED', NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Inserir odds de teste
INSERT INTO odds (id, match_id, bookmaker, market, outcome, odd_decimal, implied_probability, captured_at)
SELECT 
  gen_random_uuid(),
  m.id,
  'Bet365',
  'h2h',
  'home',
  2.10,
  0.476,
  NOW()
FROM matches m WHERE m.external_id = 'test-match-1'
UNION ALL
SELECT 
  gen_random_uuid(),
  m.id,
  'Bet365',
  'h2h',
  'draw',
  3.40,
  0.294,
  NOW()
FROM matches m WHERE m.external_id = 'test-match-1'
UNION ALL
SELECT 
  gen_random_uuid(),
  m.id,
  'Bet365',
  'h2h',
  'away',
  3.50,
  0.286,
  NOW()
FROM matches m WHERE m.external_id = 'test-match-1'
ON CONFLICT DO NOTHING;

-- Inserir recomendacoes de teste
INSERT INTO recommendations (id, match_id, market, outcome, bookmaker, odd_decimal, model_probability, implied_probability, value, kelly_fraction, stake_euros, confidence, reasoning, status, created_at)
SELECT 
  gen_random_uuid(),
  m.id,
  'h2h',
  'home',
  'Bet365',
  2.10,
  0.55,
  0.476,
  0.155,
  0.031,
  3.10,
  8,
  'Equipa da casa tem melhor forma recente e vantagem do campo.',
  'PENDING',
  NOW()
FROM matches m WHERE m.external_id = 'test-match-1'
UNION ALL
SELECT 
  gen_random_uuid(),
  m.id,
  'h2h',
  'away',
  'Bet365',
  3.50,
  0.35,
  0.286,
  0.224,
  0.041,
  4.10,
  7,
  'Equipa visitante tem historico favoravel nos confrontos diretos.',
  'PENDING',
  NOW()
FROM matches m WHERE m.external_id = 'test-match-2'
ON CONFLICT DO NOTHING;

-- Verificar dados inseridos
SELECT 'Matches count' as info, COUNT(*) as total FROM matches
UNION ALL
SELECT 'Odds count', COUNT(*) FROM odds
UNION ALL
SELECT 'Recommendations count', COUNT(*) FROM recommendations;
