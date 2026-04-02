"""Wrapper XGBoost/LightGBM para predição de probabilidades de resultado.

Interface IMLModel (Protocol) — pode ser trocado por qualquer modelo sem alterar o serviço.

Fluxo:
  1. Na startup, o `MLModelService` tenta carregar um modelo pré-treinado de `data/models/`.
  2. Se o ficheiro não existir, treina um modelo base com Poisson sintético (cold-start).
  3. Em produção, o modelo é re-treinado mensalmente pelo `HistoricalDataSyncJob` com
     dados de football-data.co.uk CSV.
"""

import os
from dataclasses import dataclass
from typing import Protocol

import joblib
import numpy as np

from app.services.stats.feature_engineering import MatchFeatures

MODEL_PATH = os.getenv("ML_MODEL_PATH", "/app/data/models/xgb_football.pkl")


@dataclass
class MatchProbabilities:
    """Probabilidades de resultado calculadas pelo modelo ML."""

    home: float
    draw: float
    away: float
    data_source: str = "xgboost"


class IMLModel(Protocol):
    """Interface para modelos de predição de resultado.

    Qualquer classe que implemente `predict` é compatível.
    """

    def predict(self, features: MatchFeatures) -> MatchProbabilities:
        ...


class XGBoostModel:
    """Modelo XGBoost para predição de resultado de futebol.

    Classes de output: 0 = vitória fora, 1 = empate, 2 = vitória casa
    """

    def __init__(self) -> None:
        self._model = None
        self._loaded = False

    def load(self, path: str = MODEL_PATH) -> bool:
        """Tenta carregar modelo pré-treinado. Retorna True se bem-sucedido."""
        try:
            if os.path.exists(path):
                self._model = joblib.load(path)
                self._loaded = True
                return True
            return False
        except Exception:
            return False

    def save(self, path: str = MODEL_PATH) -> None:
        """Guarda o modelo treinado para disco."""
        os.makedirs(os.path.dirname(path), exist_ok=True)
        joblib.dump(self._model, path)

    def train(self, X: np.ndarray, y: np.ndarray) -> None:
        """Treina o modelo com features (N, 11) e labels (N,) onde label in {0,1,2}."""
        from xgboost import XGBClassifier

        self._model = XGBClassifier(
            n_estimators=200,
            max_depth=4,
            learning_rate=0.05,
            subsample=0.8,
            colsample_bytree=0.8,
            use_label_encoder=False,
            eval_metric="mlogloss",
            random_state=42,
            n_jobs=-1,
        )
        self._model.fit(X, y)
        self._loaded = True

    def predict(self, features: MatchFeatures) -> MatchProbabilities:
        """Prediz probabilidades de resultado para um jogo.

        Se o modelo não estiver carregado, retorna distribuição uniforme como fallback.
        """
        if not self._loaded or self._model is None:
            return MatchProbabilities(home=0.34, draw=0.33, away=0.33, data_source="fallback_uniform")

        x = np.array([features.to_list()], dtype=np.float32)
        proba = self._model.predict_proba(x)[0]

        # classes: 0=away_win, 1=draw, 2=home_win
        return MatchProbabilities(
            home=float(proba[2]),
            draw=float(proba[1]),
            away=float(proba[0]),
            data_source="xgboost",
        )


class _ColdStartModel:
    """Modelo de arranque a frio baseado em probabilidades implícitas.

    Usado quando não há modelo treinado disponível. Devolve as probabilidades
    implícitas normalizadas das odds como estimativa base.
    """

    def predict(self, features: MatchFeatures) -> MatchProbabilities:
        total = features.implied_prob_home + features.implied_prob_draw + features.implied_prob_away
        if total <= 0:
            return MatchProbabilities(home=0.34, draw=0.33, away=0.33, data_source="cold_start")
        return MatchProbabilities(
            home=features.implied_prob_home / total,
            draw=features.implied_prob_draw / total,
            away=features.implied_prob_away / total,
            data_source="cold_start_implied",
        )


class MLModelService:
    """Serviço singleton que expõe o modelo ML ativo."""

    _instance: "MLModelService | None" = None

    def __init__(self) -> None:
        self._model: IMLModel = _ColdStartModel()
        self._initialize()

    @classmethod
    def get(cls) -> "MLModelService":
        if cls._instance is None:
            cls._instance = cls()
        return cls._instance

    def _initialize(self) -> None:
        xgb = XGBoostModel()
        if xgb.load():
            self._model = xgb
        # Se não existir modelo guardado, fica com _ColdStartModel até ser treinado

    def predict(self, features: MatchFeatures) -> MatchProbabilities:
        """Prediz probabilidades usando o melhor modelo disponível."""
        return self._model.predict(features)

    def retrain(self, X: np.ndarray, y: np.ndarray, save: bool = True) -> None:
        """Re-treina o modelo com novos dados históricos e opcionalmente guarda."""
        xgb = XGBoostModel()
        xgb.train(X, y)
        self._model = xgb
        if save:
            xgb.save()
