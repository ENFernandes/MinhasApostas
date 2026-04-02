export interface Match {
  id: string
  sport: 'football' | 'tennis'
  homeTeam: string
  awayTeam: string
  commenceTime: string
  isLive: boolean
  minute?: number
  homeScore?: number
  awayScore?: number
  competition?: string
}

export interface Odds {
  bookmaker: string
  market: string
  outcome: string
  odd: number
  impliedProbability: number
}

export interface ModelProbabilities {
  home: number
  draw: number
  away: number
  over_2_5: number
  btts: number
  /** Origem das probabilidades (ex. xgboost, poisson_historical, placeholder_1x2) */
  dataSource?: string
}

export interface RecommendedMarket {
  market: string
  outcome: string
  bookmaker: string
  odd: number
  impliedProbability: number
  modelProbability: number
  value: number
  kellyFraction: number
  stakeEuros: number
  confidence: number
}

export interface AlternativeMarket {
  market: string
  outcome: string
  odd: number
  value: number
  confidence: number
}

export interface Analysis {
  matchId: string
  sport: string
  homeTeam: string
  awayTeam: string
  commenceTime: string
  isLive: boolean
  modelProbabilities: ModelProbabilities
  recommendedMarket: RecommendedMarket | null
  alternativeMarkets: AlternativeMarket[]
  reasoning: string
  contextFlags: Record<string, any>
  generatedAt: string
  llmProvider: string
  llmModel: string
}

export interface BetResult {
  id: string
  matchId: string
  match: Match
  recommendation: RecommendedMarket
  stakeActual: number
  oddActual: number
  outcome: 'WIN' | 'LOSS' | 'VOID'
  profitLoss: number
  settledAt: string
}

export interface PerformanceStats {
  totalProfitLoss: number
  roi: number
  winRate: number
  totalBets: number
  winningBets: number
  losingBets: number
  byMarket: {
    market: string
    totalBets: number
    winRate: number
    profitLoss: number
    roi: number
  }[]
  bySport: {
    sport: string
    totalBets: number
    winRate: number
    profitLoss: number
    roi: number
  }[]
}

export interface UserSettings {
  bankroll: number
  minValue: number
  minConfidence: number
  maxStakePct: number
  kellyFraction: number
  llmProvider: string
}
