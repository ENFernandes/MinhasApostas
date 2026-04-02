import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'

const ANALYSIS_BASE = '/analysis'

export function useRecommendationsStream(): { isConnected: boolean } {
  const queryClient = useQueryClient()
  const [isConnected, setIsConnected] = useState(false)
  const esRef = useRef<EventSource | null>(null)

  useEffect(() => {
    const es = new EventSource(`${ANALYSIS_BASE}/stream/recommendations`)
    esRef.current = es

    es.addEventListener('open', () => {
      setIsConnected(true)
    })

    es.addEventListener('recommendation', () => {
      queryClient.invalidateQueries({ queryKey: ['recommendations'] })
    })

    es.addEventListener('error', () => {
      setIsConnected(false)
    })

    return () => {
      es.close()
      esRef.current = null
      setIsConnected(false)
    }
  }, [queryClient])

  return { isConnected }
}
