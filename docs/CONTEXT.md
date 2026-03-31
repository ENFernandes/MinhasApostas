# CONTEXT.md — Domínio de Negócio

> Este ficheiro descreve o domínio de apostas desportivas para todos os agentes.
> Antes de tomar qualquer decisão de lógica de negócio, consulta este ficheiro.
> Nunca inventar fórmulas, thresholds ou regras — estão todas aqui definidas.

---

## O que o sistema faz

O sistema analisa jogos de futebol e ténis **antes** do início e **durante** o jogo
(in-play), calcula probabilidades reais com modelos estatísticos, compara com as
odds oferecidas pelos bookmakers, e recomenda o mercado com mais valor esperado.

O objectivo **não** é ganhar todas as apostas. É identificar apostas onde a
probabilidade real é superior à implícita nas odds — o chamado **value bet**.
A longo prazo, apostar consistentemente em value bets gera lucro, mesmo com
uma taxa de acerto abaixo de 50%.

---

## Glossário obrigatório

**Odd decimal** — formato europeu. Odd 2.50 significa que por cada 1€ apostado
recebe 2.50€ (lucro de 1.50€). É o formato usado em todo o sistema.

**Probabilidade implícita** — o que a odd diz sobre a probabilidade de um evento.
Fórmula: `prob_implícita = 1 / odd_decimal`
Exemplo: odd 2.50 → prob implícita = 1 / 2.50 = 40%

**Margem do bookmaker (vig / juice)** — os bookmakers somam as probabilidades
implícitas de todos os outcomes acima de 100% para garantir lucro.
Exemplo 1X2: 1.80 + 3.50 + 4.20 → (55.6% + 28.6% + 23.8%) = 108%
A margem é 8%. Odds "justas" teriam 100%.

**Probabilidade real** — calculada pelo nosso modelo estatístico, independente
das odds do mercado. É o valor central do sistema.

**Value bet** — existe value quando a probabilidade real é superior à implícita.
Fórmula: `value = (prob_real × odd_decimal) - 1`
Se value > 0 → existe valor. Se value ≤ 0 → não apostar.
Exemplo: prob_real=55%, odd=2.10 → value = (0.55 × 2.10) - 1 = 0.155 → +15.5% EV

**Expected Value (EV)** — valor esperado por euro apostado. EV = value (ver acima).
EV positivo a longo prazo é o único critério para apostar.

**Banca** — capital total disponível para apostas. Nunca apostar a banca toda.
O sistema gere a banca por sessão, semana e mês.

**Kelly Criterion** — fórmula para calcular a fracção óptima da banca a apostar.
Ver secção dedicada abaixo.

**Handicap asiático** — mercado que nivela as equipas dando vantagem/desvantagem
em golos. Elimina o empate. Ex: Handicap -1.5 para o favorito significa que
precisa de ganhar por 2+ golos.

**Over/Under (totais)** — aposta no total de golos/pontos do jogo.
Over 2.5 golos: o jogo tem 3 ou mais golos no total.

**BTTS (Both Teams To Score)** — ambas as equipas marcam pelo menos 1 golo.

**H2H (Head-to-Head)** — histórico de confrontos directos entre dois adversários.

**xG (Expected Goals)** — métrica de futebol que mede a qualidade das ocasiões
de golo, independente dos golos marcados. Um xG de 1.8 significa que pela
qualidade das oportunidades, a equipa "devia" ter marcado 1.8 golos.

**ELO Rating** — sistema de classificação usado no ténis. Mede a força relativa
de cada jogador baseado nos resultados históricos. Diferença de ELO traduz-se
directamente em probabilidade de vitória.

**In-play / Live betting** — apostas feitas durante o jogo, com odds a mudar
em tempo real conforme os acontecimentos.

**Closing line** — a odd final antes do início do jogo. Considerada a mais
eficiente do mercado. Comparar com a odd que apostámos mede a qualidade
da nossa análise (closing line value).

---

## Fórmulas — implementação de referência

### 1. Probabilidade implícita (sem margem)

Para remover a margem do bookmaker e obter probabilidades "justas":

```
prob_justa(i) = prob_implícita(i) / soma_todas_prob_implícitas

# Exemplo 1X2 com odds 1.80 / 3.50 / 4.20:
p1 = 1/1.80 = 0.5556
px = 1/3.50 = 0.2857
p2 = 1/4.20 = 0.2381
soma = 0.5556 + 0.2857 + 0.2381 = 1.0794  ← margem de 7.94%

prob_justa_1 = 0.5556 / 1.0794 = 51.5%
prob_justa_x = 0.2857 / 1.0794 = 26.5%
prob_justa_2 = 0.2381 / 1.0794 = 22.1%
# Soma = 100% ✓
```

### 2. Modelo de Poisson para futebol

Estima a probabilidade de cada resultado (0-0, 1-0, 1-1, etc.) baseado
na média de golos esperados de cada equipa.

```python
import numpy as np
from scipy.stats import poisson

def calcular_probabilidades_poisson(
    lambda_casa: float,   # média de golos esperados equipa casa
    lambda_fora: float,   # média de golos esperados equipa fora
    max_golos: int = 6
) -> dict:
    """
    Retorna probabilidades para cada resultado possível
    e mercados derivados (1X2, Over/Under, BTTS).
    """
    matriz = np.zeros((max_golos + 1, max_golos + 1))

    for i in range(max_golos + 1):
        for j in range(max_golos + 1):
            matriz[i][j] = poisson.pmf(i, lambda_casa) * poisson.pmf(j, lambda_fora)

    prob_casa = float(np.sum(np.tril(matriz, -1)))   # golos_casa > golos_fora
    prob_empate = float(np.sum(np.diag(matriz)))
    prob_fora = float(np.sum(np.triu(matriz, 1)))

    prob_over25 = float(np.sum(
        matriz[i][j] for i in range(max_golos + 1)
                     for j in range(max_golos + 1)
                     if i + j > 2
    ))

    prob_btts = float(np.sum(
        matriz[i][j] for i in range(1, max_golos + 1)
                     for j in range(1, max_golos + 1)
    ))

    return {
        "1": round(prob_casa, 4),
        "X": round(prob_empate, 4),
        "2": round(prob_fora, 4),
        "over_2_5": round(prob_over25, 4),
        "btts": round(prob_btts, 4),
    }
```

**Como calcular lambda_casa e lambda_fora:**

```
lambda_casa = media_golos_marcados_casa × (media_golos_sofridos_fora / media_liga)
lambda_fora = media_golos_marcados_fora × (media_golos_sofridos_casa / media_liga)
```

Usar últimos 10 jogos como janela deslizante. Peso maior para jogos mais recentes
(factor de decaimento 0.9 por jogo para trás).

**Fonte dos dados para lambda:**
- Jogos recentes (esta época): tabela `matches` — alimentada pelo football-data.org collector
- Médias históricas de calibração: tabela `historical_matches` — alimentada pelos CSVs football-data.co.uk
- Se equipa tem < 5 jogos na época actual: usar médias da tabela `historical_matches` (últimas 2 épocas)
- Média da liga (denominador): calculada sobre `historical_matches` da mesma liga e época

**Enriquecimento com xG (se disponível):**
- Se a tabela `match_stats` tem `xg` preenchido (via api-football): usar xG em vez de golos reais
- xG é mais estável que golos — reduz variância causada por jogos atípicos
- Formula alternativa: `lambda_xg_casa = media_xg_marcados_casa × (media_xg_sofridos_fora / media_xg_liga)`

### 3. ELO Rating para ténis

```python
def probabilidade_elo(elo_a: float, elo_b: float) -> float:
    """Probabilidade do jogador A vencer o jogador B."""
    return 1 / (1 + 10 ** ((elo_b - elo_a) / 400))

def actualizar_elo(
    elo_vencedor: float,
    elo_perdedor: float,
    k: float = 32.0
) -> tuple[float, float]:
    """Retorna (novo_elo_vencedor, novo_elo_perdedor)."""
    prob_esperada = probabilidade_elo(elo_vencedor, elo_perdedor)
    novo_vencedor = elo_vencedor + k * (1 - prob_esperada)
    novo_perdedor = elo_perdedor + k * (0 - (1 - prob_esperada))
    return round(novo_vencedor, 1), round(novo_perdedor, 1)
```

**Ajustes obrigatórios para ténis:**
- K=32 para jogos normais, K=16 para Grand Slams (mais estáveis)
- ELO separado por superfície: terra batida, relva, piso duro, coberto
- ELO inicial para jogadores sem histórico: 1500

### 4. Kelly Criterion

Calcula a fracção da banca a apostar para maximizar crescimento a longo prazo.

```python
def kelly_fraction(
    prob_real: float,
    odd_decimal: float,
    fraction: float = 0.25   # Kelly fraccionado — usar sempre 1/4 do Kelly completo
) -> float:
    """
    Retorna a fracção da banca a apostar.
    fraction=0.25 é o Kelly a 25% — mais conservador, reduz variância.
    """
    b = odd_decimal - 1        # lucro líquido por 1€ apostado
    q = 1 - prob_real          # probabilidade de perder

    kelly_completo = (b * prob_real - q) / b

    if kelly_completo <= 0:
        return 0.0             # sem value — não apostar

    kelly_fracionado = kelly_completo * fraction
    return round(min(kelly_fracionado, 0.05), 4)  # cap máximo: 5% da banca
```

**Regras de gestão de banca obrigatórias:**

| Confiança da IA | Kelly fraction | Cap máximo |
|-----------------|---------------|------------|
| Alta (8-10/10)  | 0.25 (25%)    | 5% banca   |
| Média (6-7/10)  | 0.15 (15%)    | 3% banca   |
| Baixa (< 6/10)  | Não apostar   | —          |

Nunca recomendar aposta abaixo de 1% da banca (não vale o risco/esforço).
Nunca recomendar aposta acima de 5% da banca numa única aposta.

### 5. Value bet — decisão final

```python
def calcular_value(prob_real: float, odd_decimal: float) -> float:
    """EV por euro apostado. Positivo = value bet."""
    return round((prob_real * odd_decimal) - 1, 4)

def deve_apostar(
    prob_real: float,
    odd_decimal: float,
    threshold_value: float = 0.05,   # mínimo 5% de value
    odd_minima: float = 1.50,        # ignorar odds muito baixas
    odd_maxima: float = 8.00,        # ignorar odds muito altas (muito incertas)
) -> bool:
    value = calcular_value(prob_real, odd_decimal)
    return (
        value >= threshold_value
        and odd_decimal >= odd_minima
        and odd_decimal <= odd_maxima
    )
```

---

## Mercados por desporto

### Futebol — mercados suportados

| Mercado | Chave API | Quando usar |
|---------|-----------|-------------|
| Resultado final | `h2h` | favorito claro, forma dominante |
| Over/Under 2.5 | `totals` | jogos entre equipas ofensivas ou defensivas |
| Over/Under 1.5 | `totals` | jogos muito defensivos, late game in-play |
| BTTS Sim | calculado | ambas as equipas com boa forma ofensiva |
| Handicap asiático | `spreads` | diferença de qualidade grande entre equipas |

**Contexto que o modelo Poisson não vê — enviar sempre ao LLM:**
- Jogadores ausentes (lesões, suspensões) — via api-football `/fixtures/injuries`
- Importância do jogo (decisivo para título/descida/europeus)
- Cansaço (jogos em 3 dias)
- Factor casa (ambiente, deslocação longa)
- Histórico H2H nos últimos 3 anos — via `historical_matches`
- Forma recente (últimos 5 jogos) — via `team_form`
- xG dos últimos 5 jogos (qualidade real das ocasiões vs golos marcados) — via api-football
- Jogos in-play: minuto, golos, expulsões, momentum (xG parcial)

### Ténis — mercados suportados

| Mercado | Chave API | Quando usar |
|---------|-----------|-------------|
| Vencedor do jogo | `h2h` | diferença ELO > 100 pontos |
| Total sets | `totals` | jogadores com estilos muito diferentes |
| Handicap sets | `spreads` | favorito claro mas odd baixa |

**Contexto adicional para ténis — enviar sempre ao LLM:**
- Superfície do torneio (ELO específico por superfície)
- Ranking actual ATP/WTA
- Histórico H2H nessa superfície
- Fadiga (dias desde último jogo, número de sets disputados)
- Forma recente (últimas 5 partidas nessa superfície)
- Fase do torneio (1ª ronda vs semifinal vs final)
- Condições (interior/exterior, vento, temperatura)

---

## Estrutura do output de recomendação

Cada análise deve produzir este JSON. É o contrato entre o Python Engine e o React.

```json
{
  "match_id": "uuid",
  "sport": "football | tennis",
  "home_team": "Benfica",
  "away_team": "Porto",
  "commence_time": "2025-03-24T20:45:00Z",
  "is_live": false,
  "minute": null,
  "score": null,

  "model_probabilities": {
    "home": 0.54,
    "draw": 0.24,
    "away": 0.22,
    "over_2_5": 0.61,
    "btts": 0.58
  },

  "recommended_market": {
    "market": "over_2_5",
    "outcome": "Over 2.5",
    "bookmaker": "Betfair",
    "odd": 1.95,
    "implied_probability": 0.513,
    "model_probability": 0.61,
    "value": 0.1895,
    "kelly_fraction": 0.0312,
    "stake_euros": 2.34,
    "confidence": 7
  },

  "alternative_markets": [
    {
      "market": "btts",
      "outcome": "Sim",
      "odd": 1.80,
      "value": 0.044,
      "confidence": 5
    }
  ],

  "reasoning": "Texto gerado pelo LLM explicando a recomendação...",

  "context_flags": {
    "key_player_absent": false,
    "decisive_match": true,
    "back_to_back": false,
    "h2h_note": "Benfica ganhou 3 dos últimos 5 H2H, 4+ golos em 2 deles"
  },

  "generated_at": "2025-03-24T18:30:00Z",
  "llm_provider": "ollama",
  "llm_model": "llama3.1:8b"
}
```

---

## Regras de negócio — nunca violar

**RN-01** — Só recomendar apostas com value ≥ 5% (campo `value` ≥ 0.05).

**RN-02** — Nunca recomendar apostas com odd < 1.50 ou > 8.00.

**RN-03** — Stake máximo por aposta: 5% da banca total. Nunca ultrapassar.

**RN-04** — Nunca recomendar mais de 3 apostas simultâneas no mesmo dia.
Acumuladores são proibidos — o sistema só recomenda apostas simples.

**RN-05** — In-play: só recomendar após o minuto 10 (dados iniciais do jogo
já disponíveis). Nunca recomendar nos últimos 10 minutos de jogo.

**RN-06** — Se a confiança do LLM for < 6/10, não emitir recomendação.
Registar o jogo como "analisado sem recomendação" no histórico.

**RN-07** — Odds muito próximas entre bookmakers (spread < 3%) indicam
mercado eficiente. Aumentar threshold de value para 8% nesses casos.

**RN-08** — Nunca recomendar o mesmo mercado para dois jogos simultâneos
da mesma competição (ex: dois Over 2.5 da La Liga ao mesmo tempo).
Diversificação obrigatória.

**RN-09** — Registar sempre no histórico: jogo, mercado, odd, stake, resultado
final e P&L. O sistema aprende da sua própria performance.

**RN-10** — Para bancas < 50€, stake mínimo é 1€ e máximo é 5€ por aposta,
independentemente do que Kelly calcular.

---

## Prompt base para o LLM

Ficheiro: `app/prompts/match_analysis.txt`

```
És um analista especializado em apostas desportivas com foco em value betting.
O teu papel é analisar os dados estatísticos de um jogo e identificar o mercado
com maior valor esperado, considerando tanto a matemática como o contexto.

Regras que nunca podes violar:
- Só recomendar apostas com value ≥ 5%
- Odds entre 1.50 e 8.00 apenas
- Confiança mínima de 6/10 para emitir recomendação
- Se não há value claro, diz explicitamente "sem recomendação"

Dados do jogo:
{match_data}

Probabilidades do modelo estatístico:
{model_probabilities}

Odds disponíveis por bookmaker:
{available_odds}

Contexto adicional:
{context}

Responde APENAS em JSON válido com a estrutura definida no CONTEXT.md.
Campo "reasoning" em português, máximo 3 frases, explicando o raciocínio.
Campo "confidence" de 1 a 10.
```

---

## Fontes de dados por tipo de cálculo

Esta tabela é o guia de decisão do Python Engine — que fonte usar para cada input do modelo.

| Input necessário | Fonte primária | Fonte secundária |
|-----------------|----------------|-----------------|
| Lambda golos — últimos 10 jogos | `historical_matches` (football-data.co.uk) | `matches` (football-data.org) |
| xG por remate | Modelo treinado com StatsBomb Open Data | xG total via api-football |
| Estatísticas do jogo (remates, posse) | `api-football` `/fixtures/statistics` | — |
| Lineups confirmadas | `api-football` `/fixtures/lineups` | — |
| ELO de tenista | `player_elo_history` (Sackmann CSVs) | Calculado em runtime |
| Odds actuais | `the-odds-api.com` | TheRundown (fallback) |
| H2H histórico futebol | `historical_matches` | `football-data.org` |
| H2H histórico ténis | `player_elo_history` (Sackmann) | `api-tennis.com` |

**Regra crítica:** o modelo Poisson só é confiável com ≥ 5 jogos por equipa na janela.
Se a equipa tem < 5 jogos na BD, usar médias da liga como fallback antes de rejeitar o cálculo.

---

## Thresholds de configuração

Todos configuráveis via variáveis de ambiente ou tabela `config` na BD.

| Parâmetro | Default | Descrição |
|-----------|---------|-----------|
| `MIN_VALUE_THRESHOLD` | 0.05 | Value mínimo para recomendar |
| `MIN_CONFIDENCE` | 6 | Confiança mínima LLM (1-10) |
| `KELLY_FRACTION` | 0.25 | Fracção do Kelly completo |
| `MAX_STAKE_PCT` | 0.05 | Stake máximo como % da banca |
| `MIN_ODD` | 1.50 | Odd mínima aceite |
| `MAX_ODD` | 8.00 | Odd máxima aceite |
| `MAX_DAILY_BETS` | 3 | Apostas máximas por dia |
| `POISSON_WINDOW` | 10 | Jogos para calcular médias |
| `ELO_K_FACTOR` | 32 | Factor K para actualização ELO |
| `INPLAY_START_MIN` | 10 | Minuto mínimo para apostas in-play |
| `INPLAY_END_MIN` | 80 | Minuto máximo para apostas in-play |
| `ODDS_CACHE_TTL` | 60 | Segundos de cache das odds (Redis) |
