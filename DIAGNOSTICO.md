# Script de Diagnóstico - Verificar conectividade Backend

## 1. Verificar se os containers estão a correr
```bash
docker ps
```

Deverias ver:
- postgres:16
- rabbitmq:3.13
- redis:7
- datacollector (C#)
- analysis-engine (Python)
- frontend (React)

## 2. Testar endpoints manualmente

### Testar Data Collector (C#)
```bash
curl http://localhost:8080/api/matches/upcoming
```

### Testar Analysis Engine (Python)
```bash
curl http://localhost:8090/health
```

## 3. Verificar logs dos serviços

### Data Collector
```bash
docker logs datacollector
```

### Analysis Engine
```bash
docker logs analysis-engine
```

## 4. Verificar se há dados na BD

### Entrar no PostgreSQL
```bash
docker exec -it postgres psql -U postgres -d sportsbetting
```

### Verificar tabelas
```sql
\dt
SELECT COUNT(*) FROM matches;
SELECT COUNT(*) FROM recommendations;
```

## 5. Problemas comuns

### Problema: Portas em uso
**Solução:** Verifica se as portas 8080, 8090, 3000 estão livres:
```bash
netstat -ano | findstr :8080
netstat -ano | findstr :8090
netstat -ano | findstr :3000
```

### Problema: Backend não iniciou corretamente
**Solução:** Reinicia os serviços:
```bash
docker-compose down
docker-compose up -d
```

### Problema: CORS
**Solução:** Verifica se o backend aceita requests do frontend (localhost:3000)

## 6. Quick Fix - Popular BD com dados de teste

Se a BD estiver vazia, executa este script SQL:

```sql
-- Inserir alguns jogos de teste
INSERT INTO matches (id, external_id, sport, competition_name, home_team, away_team, commence_time, status, created_at)
VALUES 
  (gen_random_uuid(), 'test-1', 'football', 'Premier League', 'Manchester United', 'Liverpool', NOW() + INTERVAL '2 hours', 'scheduled', NOW()),
  (gen_random_uuid(), 'test-2', 'football', 'La Liga', 'Real Madrid', 'Barcelona', NOW() + INTERVAL '4 hours', 'scheduled', NOW()),
  (gen_random_uuid(), 'test-3', 'tennis', 'Wimbledon', 'Nadal', 'Djokovic', NOW() + INTERVAL '6 hours', 'scheduled', NOW());

-- Inserir uma recomendação de teste
INSERT INTO recommendations (id, match_id, market, outcome, bookmaker, odd_decimal, model_probability, implied_probability, value, kelly_fraction, stake_euros, confidence, reasoning, status, created_at)
SELECT 
  gen_random_uuid(),
  id,
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
  'Test recommendation',
  'PENDING',
  NOW()
FROM matches LIMIT 1;
```

## 7. Verificar no Browser

Abre o DevTools (F12) → tab Network → verifica se há requests falhados (em vermelho).

Erros comuns:
- `404 Not Found` → Endpoint não existe
- `500 Internal Server Error` → Erro no backend
- `CORS error` → Problema de permissões
- `ERR_CONNECTION_REFUSED` → Backend não está a correr
