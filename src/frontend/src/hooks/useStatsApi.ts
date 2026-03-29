import { useQuery } from '@tanstack/react-query'
import type { TeamForm, HeadToHeadStats } from '@/types/stats'

const API_BASE = '/api'

const API_KEY = import.meta.env.VITE_API_KEY || 'dev-api-key-123'

const defaultHeaders = {
  'X-API-Key': API_KEY,
  'Content-Type': 'application/json',
}

interface TeamFormDto {
  teamName: string
  last10Games: Array<{
    date: string
    opponent: string
    isHome: boolean
    goalsScored: number
    goalsConceded: number
    result: string
  }>
  avgGoalsScoredHome: number
  avgGoalsScoredAway: number
  avgGoalsConcededHome: number
  avgGoalsConcededAway: number
}

interface HeadToHeadDto {
  homeTeam: string
  awayTeam: string
  homeTeamWins: number
  awayTeamWins: number
  draws: number
  totalMatches: number
  homeTeamGoals: number
  awayTeamGoals: number
  matches: Array<{
    date: string
    opponent: string
    isHome: boolean
    goalsScored: number
    goalsConceded: number
    result: string
  }>
}

function mapTeamFormDto(dto: TeamFormDto): TeamForm {
  const results = dto.last10Games || []
  return {
    teamName: dto.teamName,
    last10Games: results.map(m => ({
      date: m.date,
      competition: '',
      opponent: m.opponent,
      isHome: m.isHome,
      goalsScored: m.goalsScored,
      goalsConceded: m.goalsConceded,
      result: m.result as 'W' | 'D' | 'L'
    })),
    wins: results.filter(r => r.result === 'W').length,
    draws: results.filter(r => r.result === 'D').length,
    losses: results.filter(r => r.result === 'L').length,
    goalsScored: results.reduce((sum, m) => sum + m.goalsScored, 0),
    goalsConceded: results.reduce((sum, m) => sum + m.goalsConceded, 0),
    avgGoalsScored: dto.avgGoalsScoredHome || 0,
    avgGoalsConceded: dto.avgGoalsConcededHome || 0,
  }
}

function mapHeadToHeadDto(dto: HeadToHeadDto): HeadToHeadStats {
  const matches = dto.matches || []
  return {
    homeTeamWins: dto.homeTeamWins || 0,
    draws: dto.draws || 0,
    awayTeamWins: dto.awayTeamWins || 0,
    totalMatches: dto.totalMatches || 0,
    homeTeamGoals: dto.homeTeamGoals || 0,
    awayTeamGoals: dto.awayTeamGoals || 0,
    matches: matches.map(m => ({
      date: m.date,
      competition: '',
      homeTeam: m.isHome ? dto.homeTeam : dto.awayTeam,
      awayTeam: m.isHome ? dto.awayTeam : dto.homeTeam,
      homeScore: m.isHome ? m.goalsScored : m.goalsConceded,
      awayScore: m.isHome ? m.goalsConceded : m.goalsScored,
      result: m.result as 'H' | 'A' | 'D'
    }))
  }
}

async function fetchTeamForm(teamName: string, sport: string = 'football'): Promise<TeamForm> {
  const params = new URLSearchParams({
    teamName,
    sport
  })
  
  const res = await fetch(`${API_BASE}/teams/form?${params}`, {
    headers: defaultHeaders,
  })
  
  if (!res.ok) {
    throw new Error('Failed to fetch team form')
  }
  
  const data: TeamFormDto = await res.json()
  return mapTeamFormDto(data)
}

async function fetchHeadToHead(homeTeam: string, awayTeam: string, sport: string = 'football'): Promise<HeadToHeadStats> {
  const params = new URLSearchParams({
    homeTeam,
    awayTeam,
    sport
  })
  
  const res = await fetch(`${API_BASE}/teams/head-to-head?${params}`, {
    headers: defaultHeaders,
  })
  
  if (!res.ok) {
    throw new Error('Failed to fetch head-to-head stats')
  }
  
  const data: HeadToHeadDto = await res.json()
  return mapHeadToHeadDto(data)
}

export const useTeamForm = (teamName: string, _leagueId?: number) => {
  return useQuery({
    queryKey: ['teamForm', teamName],
    queryFn: async (): Promise<TeamForm> => {
      return fetchTeamForm(teamName)
    },
    enabled: !!teamName,
    staleTime: 5 * 60 * 1000,
  })
}

export const useHeadToHead = (homeTeam: string, awayTeam: string) => {
  return useQuery({
    queryKey: ['headToHead', homeTeam, awayTeam],
    queryFn: async (): Promise<HeadToHeadStats> => {
      return fetchHeadToHead(homeTeam, awayTeam)
    },
    enabled: !!homeTeam && !!awayTeam,
    staleTime: 5 * 60 * 1000,
  })
}

export const useMatchStats = (homeTeam: string, awayTeam: string, _leagueId?: number) => {
  const homeFormQuery = useTeamForm(homeTeam)
  const awayFormQuery = useTeamForm(awayTeam)
  const h2hQuery = useHeadToHead(homeTeam, awayTeam)

  return {
    homeForm: homeFormQuery,
    awayForm: awayFormQuery,
    headToHead: h2hQuery,
    isLoading: homeFormQuery.isLoading || awayFormQuery.isLoading || h2hQuery.isLoading,
    isError: homeFormQuery.isError || awayFormQuery.isError || h2hQuery.isError,
  }
}
