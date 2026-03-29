export interface HeadToHeadMatch {
  date: string
  competition: string
  homeTeam: string
  awayTeam: string
  homeScore: number
  awayScore: number
  result: 'H' | 'D' | 'A'
}

export interface TeamFormMatch {
  date: string
  competition: string
  opponent: string
  isHome: boolean
  goalsScored: number
  goalsConceded: number
  result: 'W' | 'D' | 'L'
}

export interface TeamForm {
  teamName: string
  last10Games: TeamFormMatch[]
  wins: number
  draws: number
  losses: number
  goalsScored: number
  goalsConceded: number
  avgGoalsScored: number
  avgGoalsConceded: number
}

export interface HeadToHeadStats {
  homeTeamWins: number
  draws: number
  awayTeamWins: number
  totalMatches: number
  homeTeamGoals: number
  awayTeamGoals: number
  matches: HeadToHeadMatch[]
}

export interface TeamStats {
  form: TeamForm
  homeForm?: TeamForm
  awayForm?: TeamForm
}

export interface MatchStats {
  homeTeam: TeamStats
  awayTeam: TeamStats
  headToHead: HeadToHeadStats
}

export interface Odd {
  id: string
  bookmaker: string
  market: string
  outcome: string
  odd: number
  impliedProbability: number
}

export interface MarketOdds {
  market: string
  outcomes: {
    name: string
    odds: Odd[]
  }[]
}

export interface OddsAnalysis {
  odd: number
  modelProbability: number
  impliedProbability: number
  value: number
  kellyFraction: number
  stakeEuros: number
  isValid: boolean
  confidence: number
  reasoning: string
}

export interface MarketAnalysis {
  market: string
  outcome: string
  bookmaker: string
  odd: number
  analysis: OddsAnalysis
}
