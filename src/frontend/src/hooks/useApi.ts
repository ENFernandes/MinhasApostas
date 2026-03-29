import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { Match, Analysis, BetResult, PerformanceStats, Odds } from '@/types'

const API_BASE = '/api'
const ANALYSIS_BASE = '/analysis'

// API Key from environment variable
const API_KEY = import.meta.env.VITE_API_KEY || 'dev-api-key-123'

// Default headers for C# backend
const defaultHeaders = {
  'X-API-Key': API_KEY,
  'Content-Type': 'application/json',
}

// Matches
export function useMatches(from?: Date, to?: Date, sport?: string) {
  const params = new URLSearchParams()
  const now = new Date()
  const defaultFrom = now // momento atual, não meia-noite
  const defaultTo = new Date()
  defaultTo.setDate(defaultTo.getDate() + 14)
  
  const fromDate = from || defaultFrom
  const toDate = to || defaultTo
  
  params.append('from', fromDate.toISOString())
  params.append('to', toDate.toISOString())
  if (sport && sport !== 'all') params.append('sport', sport)
  
  return useQuery({
    queryKey: ['matches', { from, to, sport }],
    queryFn: async (): Promise<Match[]> => {
      const res = await fetch(`${API_BASE}/matches/upcoming?${params}`, {
        headers: defaultHeaders,
      })
      if (!res.ok) {
        const errorText = await res.text()
        throw new Error(`Failed to fetch matches: ${errorText}`)
      }
      const data = await res.json()
      // Map competitionName to competition for frontend compatibility
      return data.map((m: any) => ({
        ...m,
        competition: m.competitionName || m.competition
      }))
    },
    refetchInterval: 60000, // Refetch every minute
  })
}

export function useMatch(matchId: string) {
  return useQuery({
    queryKey: ['match', matchId],
    queryFn: async (): Promise<Match> => {
      const res = await fetch(`${API_BASE}/matches/${matchId}`, {
        headers: defaultHeaders,
      })
      if (!res.ok) throw new Error('Failed to fetch match')
      return res.json()
    },
    enabled: !!matchId,
  })
}

export function useMatchOdds(matchId: string) {
  return useQuery({
    queryKey: ['match-odds', matchId],
    queryFn: async (): Promise<Odds[]> => {
      const res = await fetch(`${API_BASE}/matches/${matchId}/odds`, {
        headers: defaultHeaders,
      })
      if (!res.ok) throw new Error('Failed to fetch odds')
      return res.json()
    },
    enabled: !!matchId,
    refetchInterval: 30000, // Refetch every 30 seconds for live odds
  })
}

// Analysis
export function useAnalyzeMatch() {
  const queryClient = useQueryClient()
  
  return useMutation({
    mutationFn: async ({ matchId }: { matchId: string; data?: any }): Promise<Analysis> => {
      const res = await fetch(`${ANALYSIS_BASE}/match/${matchId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      })
      if (!res.ok) throw new Error('Analysis failed')
      return res.json()
    },
    onSuccess: (data) => {
      queryClient.setQueryData(['analysis', data.matchId], data)
    },
  })
}

export function useLatestAnalysis(matchId: string) {
  return useQuery({
    queryKey: ['analysis', matchId],
    queryFn: async (): Promise<Analysis> => {
      const res = await fetch(`${ANALYSIS_BASE}/match/${matchId}/latest`, {
        headers: { 'Content-Type': 'application/json' },
      })
      if (!res.ok) throw new Error('Failed to fetch analysis')
      return res.json()
    },
    enabled: !!matchId,
  })
}

// Bets
export function useRegisterBet() {
  const queryClient = useQueryClient()
  
  return useMutation({
    mutationFn: async (data: any) => {
      const res = await fetch(`${API_BASE}/bets`, {
        method: 'POST',
        headers: defaultHeaders,
        body: JSON.stringify(data),
      })
      if (!res.ok) throw new Error('Failed to register bet')
      return res.json()
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bets'] })
      queryClient.invalidateQueries({ queryKey: ['performance'] })
    },
  })
}

export function useBetHistory() {
  return useQuery({
    queryKey: ['bets'],
    queryFn: async (): Promise<BetResult[]> => {
      const res = await fetch(`${API_BASE}/recommendations`, {
        headers: defaultHeaders,
      })
      if (!res.ok) throw new Error('Failed to fetch bet history')
      return res.json()
    },
  })
}

export function usePendingBets() {
  return useQuery({
    queryKey: ['bets', 'pending'],
    queryFn: async (): Promise<BetResult[]> => {
      const res = await fetch(`${API_BASE}/recommendations?status=PENDING`, {
        headers: defaultHeaders,
      })
      if (!res.ok) throw new Error('Failed to fetch pending bets')
      return res.json()
    },
    refetchInterval: 60000, // Refetch every minute
  })
}

// Performance
export function usePerformanceStats() {
  return useQuery({
    queryKey: ['performance'],
    queryFn: async (): Promise<PerformanceStats> => {
      const res = await fetch(`${API_BASE}/stats/performance`, {
        headers: defaultHeaders,
      })
      if (!res.ok) throw new Error('Failed to fetch performance stats')
      return res.json()
    },
  })
}

// Recommendations
export function useRecommendations(status: string = 'PENDING') {
  return useQuery({
    queryKey: ['recommendations', status],
    queryFn: async () => {
      const res = await fetch(`${ANALYSIS_BASE}/recommendations?status=${status}`, {
        headers: { 'Content-Type': 'application/json' },
      })
      if (!res.ok) throw new Error('Failed to fetch recommendations')
      return res.json()
    },
    refetchInterval: 30000, // Refetch every 30 seconds
  })
}
