"""
load_historical.py — Carregamento único de dados históricos

Executa uma vez para popular as tabelas:
  - historical_matches   (football-data.co.uk CSVs)
  - historical_events    (StatsBomb Open Data)
  - player_elo_history   (Jeff Sackmann ATP/WTA CSVs)

Uso:
  python db/seeds/load_historical.py --sport all
  python db/seeds/load_historical.py --sport football
  python db/seeds/load_historical.py --sport tennis
  python db/seeds/load_historical.py --sport statsbomb

Tempo estimado: 10-20 minutos dependendo da ligação.

Requisitos:
  pip install pandas sqlalchemy psycopg2-binary statsbombpy tqdm python-dotenv
  Variáveis de ambiente: POSTGRES_* (lidas de `.env` na raiz do repo)
"""

import argparse
import asyncio
import os
import sys
from pathlib import Path

import pandas as pd
import structlog
from sqlalchemy import create_engine, text
from sqlalchemy.engine import Engine
from tqdm import tqdm

log = structlog.get_logger()

# Raiz do repositório (db/seeds/ -> db/ -> raiz)
_REPO_ROOT = Path(__file__).resolve().parent.parent.parent


def _load_dotenv() -> None:
    """Carrega `.env` na raiz do projeto (não é automático no Python)."""
    env_path = _REPO_ROOT / ".env"
    try:
        from dotenv import load_dotenv

        load_dotenv(env_path)
    except ImportError:
        log.warning(
            "python-dotenv não instalado — instala com: pip install python-dotenv",
            env_file=str(env_path),
        )


_load_dotenv()

# ─────────────────────────────────────────
# Configuração (após .env)
# ─────────────────────────────────────────

POSTGRES_URL = (
    f"postgresql://{os.getenv('POSTGRES_USER')}:{os.getenv('POSTGRES_PASSWORD')}"
    f"@{os.getenv('POSTGRES_HOST', 'localhost')}:{os.getenv('POSTGRES_PORT', '5432')}"
    f"/{os.getenv('POSTGRES_DB', 'sportsbetting')}"
)

HISTORICAL_DATA_PATH = Path(os.getenv("HISTORICAL_DATA_PATH", "./data/historical"))

# Ligas football-data.co.uk por código
# Formato URL: https://www.football-data.co.uk/mmz4281/{SSSS}/{LEAGUE}.csv
# {SSSS} = época ex: 2324 para 2023/24
FOOTBALL_LEAGUES = {
    "E0": "Premier League",
    "E1": "Championship",
    "SP1": "La Liga",
    "D1": "Bundesliga",
    "I1": "Serie A",
    "F1": "Ligue 1",
    "P1": "Primeira Liga",
    "N1": "Eredivisie",
    "B1": "Pro League",
    "T1": "Süper Lig",
}

# Épocas a carregar (mais recentes primeiro)
SEASONS = ["2425", "2324", "2223", "2122", "2021", "1920", "1819", "1718"]

# Competições StatsBomb com dados open
STATSBOMB_COMPETITIONS = [
    {"competition_id": 2, "season_id": 44},   # Premier League 2003/04
    {"competition_id": 11, "season_id": 90},  # La Liga 2020/21
    {"competition_id": 37, "season_id": 90},  # Liga Nos 2020/21
    {"competition_id": 43, "season_id": 3},   # FIFA World Cup 2018
    {"competition_id": 16, "season_id": 4},   # Champions League 2018/19
]


# ─────────────────────────────────────────
# Loader: football-data.co.uk
# ─────────────────────────────────────────

def load_football_historical(engine: Engine) -> None:
    """
    Carrega resultados históricos de dezenas de ligas europeias
    desde football-data.co.uk (CSV sem autenticação).
    Popula a tabela historical_matches.
    """
    log.info("A iniciar carregamento football-data.co.uk...")
    total_rows = 0

    for season in tqdm(SEASONS, desc="Épocas"):
        for league_code, league_name in FOOTBALL_LEAGUES.items():
            url = f"https://www.football-data.co.uk/mmz4281/{season}/{league_code}.csv"
            try:
                df = pd.read_csv(url, on_bad_lines="skip", encoding="latin-1")

                # Normalizar colunas essenciais
                df = df.rename(columns={
                    "Date":   "match_date",
                    "HomeTeam": "home_team",
                    "AwayTeam": "away_team",
                    "FTHG":   "home_goals",
                    "FTAG":   "away_goals",
                    "FTR":    "result",      # H/D/A
                    "HS":     "home_shots",
                    "AS":     "away_shots",
                    "HST":    "home_shots_target",
                    "AST":    "away_shots_target",
                    "HC":     "home_corners",
                    "AC":     "away_corners",
                    "HY":     "home_yellows",
                    "AY":     "away_yellows",
                    "HR":     "home_reds",
                    "AR":     "away_reds",
                })

                df["league_code"] = league_code
                df["league_name"] = league_name
                df["season"] = season
                df["source"] = "football-data.co.uk"

                # Guardar apenas colunas que existem
                cols = [c for c in [
                    "match_date", "home_team", "away_team",
                    "home_goals", "away_goals", "result",
                    "home_shots", "away_shots",
                    "home_shots_target", "away_shots_target",
                    "home_corners", "away_corners",
                    "home_yellows", "away_yellows",
                    "home_reds", "away_reds",
                    "league_code", "league_name", "season", "source",
                ] if c in df.columns]

                df = df[cols].dropna(subset=["home_team", "away_team", "home_goals"])

                # Normalizar nomes e datas para alinhar com idx_historical_matches_unique e evitar duplicados óbvios
                df["home_team"] = df["home_team"].astype(str).str.strip()
                df["away_team"] = df["away_team"].astype(str).str.strip()
                df["_match_dt"] = pd.to_datetime(
                    df["match_date"], dayfirst=True, errors="coerce"
                )
                df = df.dropna(subset=["_match_dt"])
                df["match_date"] = df["_match_dt"].dt.strftime("%Y-%m-%d")
                df = df.drop(columns=["_match_dt"])
                df = df.drop_duplicates(
                    subset=["match_date", "home_team", "away_team", "league_code", "season"],
                    keep="first",
                )

                df.to_sql(
                    "historical_matches",
                    engine,
                    if_exists="append",
                    index=False,
                    method="multi",
                    chunksize=500,
                )
                total_rows += len(df)

            except Exception as e:
                log.warning("Falha ao carregar CSV",
                            league=league_code, season=season, error=str(e))

    log.info("football-data.co.uk concluído", total_rows=total_rows)


# ─────────────────────────────────────────
# Loader: StatsBomb Open Data
# ─────────────────────────────────────────

def load_statsbomb_events(engine: Engine) -> None:
    """
    Carrega eventos StatsBomb (cada passe, remate, etc.) para
    as competições configuradas em STATSBOMB_COMPETITIONS.
    Popula a tabela historical_events.

    Usa a biblioteca statsbombpy que lê directamente do GitHub.
    """
    try:
        from statsbombpy import sb  # type: ignore
    except ImportError:
        log.error("statsbombpy não instalado. Executar: pip install statsbombpy")
        return

    log.info("A iniciar carregamento StatsBomb Open Data...")
    total_events = 0

    for comp in tqdm(STATSBOMB_COMPETITIONS, desc="Competições StatsBomb"):
        try:
            matches = sb.matches(
                competition_id=comp["competition_id"],
                season_id=comp["season_id"],
            )

            for _, match in tqdm(
                matches.iterrows(),
                total=len(matches),
                desc=f"Jogos comp={comp['competition_id']}",
                leave=False,
            ):
                try:
                    events = sb.events(match_id=match["match_id"])

                    # Filtrar apenas remates para o modelo xG
                    shots = events[events["type"] == "Shot"].copy()

                    if shots.empty:
                        continue

                    shots["match_id_sb"] = match["match_id"]
                    shots["competition_id"] = comp["competition_id"]
                    shots["season_id"] = comp["season_id"]
                    shots["home_team"] = match["home_team"]
                    shots["away_team"] = match["away_team"]
                    shots["source"] = "statsbomb"

                    # Extrair campos de shot_statsbomb_xg
                    if "shot" in shots.columns:
                        shots["xg"] = shots["shot"].apply(
                            lambda x: x.get("statsbomb_xg", None)
                            if isinstance(x, dict) else None
                        )
                        shots["outcome"] = shots["shot"].apply(
                            lambda x: x.get("outcome", {}).get("name", None)
                            if isinstance(x, dict) else None
                        )
                        shots["technique"] = shots["shot"].apply(
                            lambda x: x.get("technique", {}).get("name", None)
                            if isinstance(x, dict) else None
                        )

                    cols = [c for c in [
                        "match_id_sb", "competition_id", "season_id",
                        "home_team", "away_team",
                        "minute", "second", "team", "player",
                        "xg", "outcome", "technique",
                        "location", "source",
                    ] if c in shots.columns]

                    shots[cols].to_sql(
                        "historical_events",
                        engine,
                        if_exists="append",
                        index=False,
                        method="multi",
                        chunksize=500,
                    )
                    total_events += len(shots)

                except Exception as e:
                    log.warning("Falha ao carregar eventos do jogo",
                                match_id=match.get("match_id"), error=str(e))

        except Exception as e:
            log.warning("Falha ao carregar competição StatsBomb",
                        competition_id=comp["competition_id"], error=str(e))

    log.info("StatsBomb concluído", total_events=total_events)


# ─────────────────────────────────────────
# Loader: Jeff Sackmann ATP/WTA
# ─────────────────────────────────────────

def load_tennis_elo(engine: Engine) -> None:
    """
    Carrega resultados históricos ATP e WTA dos CSVs de Jeff Sackmann.
    Calcula e guarda ELO por superfície na tabela player_elo_history.

    Requer que os repositórios estejam clonados em HISTORICAL_DATA_PATH:
      git clone https://github.com/JeffSackmann/tennis_atp data/historical/tennis_atp
      git clone https://github.com/JeffSackmann/tennis_wta data/historical/tennis_wta
    """
    log.info("A iniciar carregamento ELO de ténis (Sackmann CSVs)...")

    surfaces = ["Hard", "Clay", "Grass", "Carpet"]
    K = 32.0
    DEFAULT_ELO = 1500.0

    # ELO state: {player_name: {surface: elo}}
    elo_state: dict[str, dict[str, float]] = {}

    def get_elo(player: str, surface: str) -> float:
        return elo_state.get(player, {}).get(surface, DEFAULT_ELO)

    def update_elo(winner: str, loser: str, surface: str) -> None:
        elo_w = get_elo(winner, surface)
        elo_l = get_elo(loser, surface)
        expected = 1 / (1 + 10 ** ((elo_l - elo_w) / 400))
        delta = K * (1 - expected)

        if winner not in elo_state:
            elo_state[winner] = {}
        if loser not in elo_state:
            elo_state[loser] = {}

        elo_state[winner][surface] = round(elo_w + delta, 1)
        elo_state[loser][surface] = round(elo_l - delta, 1)

    total_matches = 0
    records = []

    for tour in ["atp", "wta"]:
        tour_path = HISTORICAL_DATA_PATH / f"tennis_{tour}"
        if not tour_path.exists():
            log.warning(f"Directório não encontrado: {tour_path}. "
                        f"Executar: git clone https://github.com/JeffSackmann/tennis_{tour} {tour_path}")
            continue

        # Ficheiros de resultados anuais (ex: atp_matches_2023.csv)
        csv_files = sorted(tour_path.glob(f"{tour}_matches_[0-9]*.csv"))

        for csv_file in tqdm(csv_files, desc=f"Ficheiros {tour.upper()}"):
            try:
                df = pd.read_csv(csv_file, low_memory=False)
                df = df.dropna(subset=["winner_name", "loser_name", "surface"])

                for _, row in df.iterrows():
                    surface = row["surface"] if row["surface"] in surfaces else "Hard"
                    update_elo(row["winner_name"], row["loser_name"], surface)
                    total_matches += 1

                    # Guardar snapshot após cada jogo para histórico
                    records.append({
                        "player_name": row["winner_name"],
                        "surface": surface,
                        "elo": elo_state[row["winner_name"]][surface],
                        "tour": tour.upper(),
                        "match_date": row.get("tourney_date"),
                        "won": True,
                    })
                    records.append({
                        "player_name": row["loser_name"],
                        "surface": surface,
                        "elo": elo_state[row["loser_name"]][surface],
                        "tour": tour.upper(),
                        "match_date": row.get("tourney_date"),
                        "won": False,
                    })

                    # Flush em lotes de 10.000
                    if len(records) >= 10_000:
                        pd.DataFrame(records).to_sql(
                            "player_elo_history",
                            engine,
                            if_exists="append",
                            index=False,
                            method="multi",
                            chunksize=500,
                        )
                        records = []

            except Exception as e:
                log.warning("Falha ao processar CSV de ténis",
                            file=str(csv_file), error=str(e))

    # Flush final
    if records:
        pd.DataFrame(records).to_sql(
            "player_elo_history",
            engine,
            if_exists="append",
            index=False,
            method="multi",
            chunksize=500,
        )

    log.info("ELO ténis concluído", total_matches=total_matches)


# ─────────────────────────────────────────
# Entry point
# ─────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Carrega dados históricos para o Sports Betting AI"
    )
    parser.add_argument(
        "--sport",
        choices=["all", "football", "tennis", "statsbomb"],
        default="all",
        help="Fonte a carregar (default: all)",
    )
    args = parser.parse_args()

    # Verificar variáveis de ambiente
    required_env = ["POSTGRES_USER", "POSTGRES_PASSWORD", "POSTGRES_DB"]
    missing = [e for e in required_env if not os.getenv(e)]
    if missing:
        log.error("Variáveis de ambiente em falta", missing=missing)
        sys.exit(1)

    engine = create_engine(POSTGRES_URL, echo=False)

    # Verificar ligação
    try:
        with engine.connect() as conn:
            conn.execute(text("SELECT 1"))
        log.info("Ligação à BD estabelecida", db=os.getenv("POSTGRES_DB"))
    except Exception as e:
        log.error("Falha na ligação à BD", error=str(e))
        sys.exit(1)

    if args.sport in ("all", "football"):
        load_football_historical(engine)

    if args.sport in ("all", "statsbomb"):
        load_statsbomb_events(engine)

    if args.sport in ("all", "tennis"):
        load_tennis_elo(engine)

    log.info("Carregamento histórico completo!")


if __name__ == "__main__":
    main()
