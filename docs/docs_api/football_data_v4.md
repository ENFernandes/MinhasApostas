# Guia de Integração: Football-Data.org API (v4)

## 📌 Visão Geral
API RESTful focada em dados estruturados de futebol: competições, equipas, jogadores e resultados em tempo real.

## 🔐 Autenticação
A autenticação é feita via **Header HTTP**.
- **Header:** `X-Auth-Token`
- **Exemplo:** `curl -H "X-Auth-Token: {YOUR_API_KEY}" http://api.football-data.org/v4/competitions/`

## 🌐 Endpoints Principais (Base: `http://api.football-data.org/v4/`)

| Recurso | Endpoint | Descrição |
| :--- | :--- | :--- |
| **Áreas** | `/areas/` | Lista países e continentes (IDs necessários para filtros). |
| **Competições** | `/competitions/` | Lista todas as ligas disponíveis no plano. |
| **Classificações** | `/competitions/{id}/standings` | Tabela classificativa atual da liga específica. |
| **Equipas** | `/teams/{id}` | Informação detalhada do clube, estádio e plantel. |
| **Jogos** | `/matches/` | Jogos globais. Aceita filtros `?dateFrom=...&dateTo=...`. |
| **Pessoas** | `/persons/{id}` | Histórico e dados de jogadores ou treinadores. |

## ⚙️ Filtros e Parâmetros
- `status`: `SCHEDULED`, `LIVE`, `IN_PLAY`, `PAUSED`, `FINISHED`, `POSTPONED`, `CANCELLED`.
- `matchday`: Número da jornada (ex: `?matchday=15`).
- `season`: Ano da temporada (ex: `?season=2023`).

## 💡 Notas para IA
- A v4 é a versão atual; a v2 está obsoleta.
- Para obter o plantel completo de uma equipa, utiliza o endpoint de equipas individual, não o de jogos.
- Os limites de taxa (Rate Limits) variam conforme o plano (Free tem limites estritos de requisições por minuto).