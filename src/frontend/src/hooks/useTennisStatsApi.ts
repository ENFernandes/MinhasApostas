import { useQuery } from '@tanstack/react-query'

const API_BASE = '/api'
const API_KEY = import.meta.env.VITE_API_KEY || 'dev-api-key-123'

const defaultHeaders = {
  'X-API-Key': API_KEY,
  'Content-Type': 'application/json',
}

export interface TennisRecentResult {
  date: string
  surface: string
  tour: string
  won: boolean
}

export interface TennisPlayerStats {
  playerName: string
  eloBySurface: Record<string, number>
  recentResults: TennisRecentResult[]
}

async function fetchTennisPlayerStats(playerName: string): Promise<TennisPlayerStats> {
  const params = new URLSearchParams({ playerName })
  const res = await fetch(`${API_BASE}/players/tennis/stats?${params}`, {
    headers: defaultHeaders,
  })
  if (!res.ok) {
    const errorText = await res.text()
    throw new Error(errorText || 'Failed to fetch tennis stats')
  }
  return res.json()
}

export function useTennisPlayerStats(playerName: string) {
  return useQuery({
    queryKey: ['tennisStats', playerName],
    queryFn: () => fetchTennisPlayerStats(playerName),
    enabled: !!playerName,
    staleTime: 5 * 60 * 1000,
  })
}

