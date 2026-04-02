"""Feature engineering para o modelo ML — transforma dados históricos em feature vectors."""

from dataclasses import dataclass


@dataclass
class MatchFeatures:
    """Feature vector para predição de resultado de jogo de futebol."""

    # Forma recente (últimos N jogos)
    home_goals_scored_avg: float
    home_goals_conceded_avg: float
    away_goals_scored_avg: float
    away_goals_conceded_avg: float

    # Head-to-head
    h2h_home_win_rate: float
    h2h_avg_goals: float

    # ELO (proxy de qualidade relativa)
    elo_diff: float  # home_elo - away_elo

    # Odds do mercado (sinal do mercado)
    implied_prob_home: float
    implied_prob_draw: float
    implied_prob_away: float

    # Contexto
    is_neutral: bool  # campo neutro

    def to_list(self) -> list[float]:
        """Converte para lista ordenada de floats para o modelo."""
        return [
            self.home_goals_scored_avg,
            self.home_goals_conceded_avg,
            self.away_goals_scored_avg,
            self.away_goals_conceded_avg,
            self.h2h_home_win_rate,
            self.h2h_avg_goals,
            self.elo_diff,
            self.implied_prob_home,
            self.implied_prob_draw,
            self.implied_prob_away,
            float(self.is_neutral),
        ]

    @classmethod
    def feature_names(cls) -> list[str]:
        return [
            "home_goals_scored_avg",
            "home_goals_conceded_avg",
            "away_goals_scored_avg",
            "away_goals_conceded_avg",
            "h2h_home_win_rate",
            "h2h_avg_goals",
            "elo_diff",
            "implied_prob_home",
            "implied_prob_draw",
            "implied_prob_away",
            "is_neutral",
        ]


def strength_ratings_from_recent_games(
    home_games: list[dict],
    away_games: list[dict],
    baseline: float = 1500.0,
    scale: float = 85.0,
    max_delta: float = 130.0,
) -> tuple[float, float]:
    """Proxy de força de equipa (escala tipo ELO) a partir do saldo de golos recentes.

    Usa os mesmos jogos que alimentam Poisson/ML (historical_matches). Não confundir com
    ELO de ténis (jogadores); aqui é apenas um sinal de qualidade relativa entre clubes.
    """

    def avg_net(games: list[dict]) -> float:
        if not games:
            return 0.0
        return sum(float(g.get("goals_scored", 0)) - float(g.get("goals_conceded", 0)) for g in games) / len(
            games
        )

    home_net = avg_net(home_games)
    away_net = avg_net(away_games)
    home_rating = baseline + max(-max_delta, min(max_delta, home_net * scale))
    away_rating = baseline + max(-max_delta, min(max_delta, away_net * scale))
    return home_rating, away_rating


def build_features_from_context(
    home_games: list[dict],
    away_games: list[dict],
    h2h_games: list[dict],
    home_elo: float,
    away_elo: float,
    implied_home: float,
    implied_draw: float,
    implied_away: float,
    is_neutral: bool = False,
) -> MatchFeatures:
    """Constrói um MatchFeatures a partir dos dados disponíveis no sistema.

    Args:
        home_games: Jogos recentes da equipa da casa (lista de dicts com goals_scored, goals_conceded).
        away_games: Jogos recentes da equipa de fora.
        h2h_games: Jogos head-to-head entre as duas equipas.
        home_elo: Rating ELO da equipa da casa.
        away_elo: Rating ELO da equipa de fora.
        implied_home / draw / away: Probabilidades implícitas das odds de mercado.
        is_neutral: Se o jogo é em campo neutro.

    Returns:
        MatchFeatures pronto para passar ao modelo ML.
    """

    def avg(games: list[dict], field: str) -> float:
        if not games:
            return 1.25  # valor default ~ média da liga
        return sum(float(g.get(field, 0)) for g in games) / len(games)

    home_scored = avg(home_games, "goals_scored")
    home_conceded = avg(home_games, "goals_conceded")
    away_scored = avg(away_games, "goals_scored")
    away_conceded = avg(away_games, "goals_conceded")

    if h2h_games:
        h2h_home_wins = sum(1 for g in h2h_games if g.get("home_win", False))
        h2h_home_win_rate = h2h_home_wins / len(h2h_games)
        h2h_avg_goals = sum(
            float(g.get("home_goals", 0)) + float(g.get("away_goals", 0))
            for g in h2h_games
        ) / len(h2h_games)
    else:
        h2h_home_win_rate = 0.45  # prior
        h2h_avg_goals = 2.5

    return MatchFeatures(
        home_goals_scored_avg=home_scored,
        home_goals_conceded_avg=home_conceded,
        away_goals_scored_avg=away_scored,
        away_goals_conceded_avg=away_conceded,
        h2h_home_win_rate=h2h_home_win_rate,
        h2h_avg_goals=h2h_avg_goals,
        elo_diff=home_elo - away_elo,
        implied_prob_home=implied_home,
        implied_prob_draw=implied_draw,
        implied_prob_away=implied_away,
        is_neutral=is_neutral,
    )
