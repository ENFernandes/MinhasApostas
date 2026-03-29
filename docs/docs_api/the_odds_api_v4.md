# Guia de Integração: The Odds API (v4)

## 📌 Visão Geral
Especializada em Odds (probabilidades) de apostas, cobrindo múltiplas casas de apostas (bookmakers) e desportos globais.

## 🔐 Autenticação
A autenticação é feita via **Query Parameter**.
- **Param:** `apiKey`
- **Exemplo:** `https://api.the-odds-api.com/v4/sports/?apiKey={YOUR_API_KEY}`

## 🌐 Fluxo de Implementação (Base: `https://api.the-odds-api.com/v4/`)

### 1. Listar Desportos Ativos
`GET /sports/?apiKey={key}`
- Retorna o `key` do desporto (ex: `soccer_portugal_primeira_liga`) necessário para os passos seguintes.

### 2. Obter Odds em Tempo Real
`GET /sports/{sport_key}/odds/?apiKey={key}&regions={region}&markets={market}`
- **Regions:** `eu` (Europa), `uk`, `us`, `au`.
- **Markets:** `h2h` (vencedor), `spreads` (handicap), `totals` (over/under).

### 3. Scores e Resultados
`GET /sports/{sport_key}/scores/?apiKey={key}`
- Retorna o estado do jogo e pontuação atual para validação de apostas.

## 📊 Formatação de Odds
- `oddsFormat`: `decimal` (padrão 2.50) ou `american` (padrão -150).

## 💡 Notas para IA
- **Gestão de Créditos:** Chamadas ao endpoint `/sports` são gratuitas. Chamadas de Odds consomem créditos baseados no número de mercados e regiões.
- **Cache:** Recomenda-se não fazer pedidos idênticos em intervalos menores que 1 minuto para otimizar o uso da quota.