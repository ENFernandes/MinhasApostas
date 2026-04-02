"""
load_historical.py — Carregamento único de dados históricos

Executa uma vez para popular as tabelas:
  - historical_matches          (football-data.co.uk CSVs)
  - historical_events           (StatsBomb Open Data)
  - historical_tennis_matches   (tennis-data.co.uk XLSX — ATP e WTA)

Uso:
  python db/seeds/load_historical.py --sport all
  python db/seeds/load_historical.py --sport football
  python db/seeds/load_historical.py --sport tennis
  python db/seeds/load_historical.py --sport statsbomb

Tempo estimado: 10-20 minutos dependendo da ligação.

Requisitos:
  pip install pandas openpyxl sqlalchemy psycopg2-binary statsbombpy tqdm python-dotenv
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


# ─────────────────────────────────────────
# Normalização de nomes de ténis
# ─────────────────────────────────────────

def _normalize_tennis_name(name: str) -> str:
    """Converte nome de jogador para forma canónica 'surname initial'.

    "Djokovic N."    → "djokovic n"
    "Novak Djokovic" → "djokovic n"
    "De Minaur A."   → "de minaur a"
    """
    name = str(name).strip()
    if not name:
        return name
    parts = [p.rstrip(".").strip() for p in name.split()]
    parts = [p for p in parts if p]
    if not parts:
        return name.lower()
    if len(parts) == 1:
        return parts[0].lower()
    singles = [p for p in parts if len(p) == 1]
    non_singles = [p for p in parts if len(p) > 1]
    if not non_singles:
        return name.lower()
    if singles:
        return " ".join(non_singles).lower() + " " + singles[0].lower()
    return non_singles[-1].lower() + " " + non_singles[0][0].lower()

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

# Anos de ténis a carregar (tennis-data.co.uk)
TENNIS_YEARS = list(range(2000, 2027))

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
SEASONS = ["2526", "2425", "2324", "2223", "2122", "2021", "1920", "1819", "1718"]

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
# Loader: tennis-data.co.uk (ATP e WTA)
# ─────────────────────────────────────────

def load_tennis_historical(engine: Engine) -> None:
    """
    Carrega resultados históricos ATP e WTA de tennis-data.co.uk (ficheiros .xlsx).

    Padrão URL:
      ATP: http://www.tennis-data.co.uk/{YEAR}/{YEAR}.xlsx
      WTA: http://www.tennis-data.co.uk/{YEAR}w/{YEAR}w.xlsx

    Inclui resultados, rankings, superfície, ronda, odds de bookmakers e marcadores.
    Popula a tabela historical_tennis_matches.

    Requisito: pip install openpyxl
    """
    log.info("A iniciar carregamento tennis-data.co.uk (ATP + WTA)...")
    total_rows = 0

    # Mapeamento de colunas XLSX → colunas BD
    # ATP tem "ATP" + "Series"; WTA tem "WTA" + "Tier" — tratados abaixo
    COLS_MAP = {
        "location":  "location",
        "tournament": "tournament",
        "date":      "match_date",
        "court":     "court",
        "surface":   "surface",
        "round":     "round",
        "best_of":   "best_of",        # "Best of" normalizado para "best_of"
        "winner":    "winner",
        "loser":     "loser",
        "wrank":     "winner_rank",
        "lrank":     "loser_rank",
        "wpts":      "winner_pts",
        "lpts":      "loser_pts",
        "w1": "w1", "l1": "l1",
        "w2": "w2", "l2": "l2",
        "w3": "w3", "l3": "l3",
        "w4": "w4", "l4": "l4",        # apenas ATP
        "w5": "w5", "l5": "l5",        # apenas ATP
        "wsets":  "wsets",
        "lsets":  "lsets",
        "comment": "comment",
        "b365w": "b365w", "b365l": "b365l",
        "psw":   "psw",   "psl":   "psl",
        "maxw":  "maxw",  "maxl":  "maxl",
        "avgw":  "avgw",  "avgl":  "avgl",
        "bfew":  "bfew",  "bfel":  "bfel",
    }

    # Colunas numéricas inteiras que podem conter NaN (usar Int64 nullable)
    INT_COLS = [
        "best_of", "winner_rank", "loser_rank", "winner_pts", "loser_pts",
        "w1", "l1", "w2", "l2", "w3", "l3", "w4", "l4", "w5", "l5",
        "wsets", "lsets",
    ]

    tours = [
        ("ATP", ""),   # http://www.tennis-data.co.uk/2024/2024.xlsx
        ("WTA", "w"),  # http://www.tennis-data.co.uk/2024w/2024w.xlsx
    ]

    for year in tqdm(TENNIS_YEARS, desc="Anos"):
        for tour, suffix in tours:
            year_suffix = f"{year}{suffix}"
            url = f"http://www.tennis-data.co.uk/{year_suffix}/{year_suffix}.xlsx"
            try:
                df = pd.read_excel(url, engine="openpyxl")

                if df.empty:
                    continue

                # Normalizar nomes de colunas: lowercase, sem espaços
                df.columns = [str(c).lower().strip().replace(" ", "_") for c in df.columns]

                # ATP tem coluna "atp" com nº sequencial; WTA tem "wta" — ambos → match_num
                if "atp" in df.columns:
                    df = df.rename(columns={"atp": "match_num"})
                elif "wta" in df.columns:
                    df = df.rename(columns={"wta": "match_num"})

                # ATP tem "series"; WTA tem "tier" — unificar em series_tier
                if "series" in df.columns:
                    df = df.rename(columns={"series": "series_tier"})
                elif "tier" in df.columns:
                    df = df.rename(columns={"tier": "series_tier"})

                # Aplicar mapeamento de colunas (só as que existem no ficheiro)
                rename_map = {k: v for k, v in COLS_MAP.items() if k in df.columns}
                df = df.rename(columns=rename_map)

                # Manter apenas colunas que existem na BD (+ match_num e series_tier)
                keep = list(rename_map.values()) + [
                    c for c in ["match_num", "series_tier"] if c in df.columns
                ]
                df = df[[c for c in keep if c in df.columns]]

                # Remover linhas sem winner/loser (cabeçalhos repetidos ou linhas vazias)
                df = df.dropna(subset=["winner", "loser"])

                # Normalizar tipos inteiros (podem ter NaN por linhas incompletas)
                for col in INT_COLS:
                    if col in df.columns:
                        df[col] = pd.to_numeric(df[col], errors="coerce").astype("Int64")

                # match_date: garantir tipo DATE (o pandas lê como Timestamp)
                if "match_date" in df.columns:
                    df["match_date"] = pd.to_datetime(
                        df["match_date"], errors="coerce"
                    ).dt.date

                # Metadados
                df["tour"] = tour
                df["source_year"] = year
                df["source"] = "tennis-data.co.uk"

                # Normalização canónica de nomes (V017)
                if "winner" in df.columns:
                    df["winner_normalized"] = df["winner"].apply(_normalize_tennis_name)
                if "loser" in df.columns:
                    df["loser_normalized"] = df["loser"].apply(_normalize_tennis_name)

                # Deduplicar dentro do ficheiro (proteção extra antes de ir à BD)
                dedup_cols = ["tour", "tournament", "match_date", "winner", "loser"]
                existing = [c for c in dedup_cols if c in df.columns]
                if len(existing) == len(dedup_cols):
                    df = df.drop_duplicates(subset=dedup_cols, keep="first")

                df.to_sql(
                    "historical_tennis_matches",
                    engine,
                    if_exists="append",
                    index=False,
                    method="multi",
                    chunksize=500,
                )
                total_rows += len(df)
                log.debug("Carregado", tour=tour, year=year, rows=len(df))

            except Exception as e:
                log.warning("Falha ao carregar XLSX de ténis",
                            tour=tour, year=year, url=url, error=str(e))

    log.info("tennis-data.co.uk concluído", total_rows=total_rows)


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
        load_tennis_historical(engine)

    log.info("Carregamento histórico completo!")


if __name__ == "__main__":
    main()
