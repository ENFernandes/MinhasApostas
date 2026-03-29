# Guia de Integração: API-Tennis.com (v2.9.4)

## 📌 Visão Geral
Cobertura exaustiva de Ténis, incluindo ATP, WTA, Challenger e torneios ITF. Oferece dados ponto a ponto em direto.

## 🔐 Autenticação
A autenticação é feita via **Query Parameter**.
- **Param:** `APIkey`
- **Exemplo:** `https://api.api-tennis.com/tennis/?method=get_livescore&APIkey={YOUR_API_KEY}`

## 🌐 Métodos Disponíveis (Parâmetro `method=`)

| Método | Função | Parâmetros Chave |
| :--- | :--- | :--- |
| `get_events` | Tipos de eventos (ATP Singles, etc). | N/A |
| `get_tournaments` | Lista torneios disponíveis. | N/A |
| `get_fixtures` | Calendário/Resultados passados. | `date_start`, `date_stop` |
| `get_livescore` | Pontuação em direto. | `match_key` (opcional) |
| `get_H2H` | Confronto direto entre dois jogadores. | `first_player_key`, `second_player_key` |
| `get_players` | Perfil e estatísticas do jogador. | `player_key` |
| `get_odds` | Odds pré-jogo. | `match_key` ou data |

## 🌍 Configuração Regional
- **Timezone:** Pode ser ajustado com `&timezone=Europe/Lisbon`. O padrão é `Europe/Berlin`.

## 💡 Notas para IA
- **Resposta JSON:** Os dados úteis estão sempre dentro da chave `result`. Se `success` for `0`, verifica a mensagem de erro.
- **Dados ao Vivo:** O campo `pointbypoint` dentro de `get_livescore` fornece o histórico de cada ponto do set atual, ideal para dashboards de alta precisão.