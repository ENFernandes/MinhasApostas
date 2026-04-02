import { useQuery } from '@tanstack/react-query'

interface SystemStatus {
  jobsRunningNow: number
  lastCollectionRun: string | null
  matchesCollectedToday: number
  queueDepth: number
  overallStatus: 'idle' | 'collecting' | 'analysing' | 'error'
}

interface StatusBarProps {
  isStreamConnected: boolean
}

const API_KEY = import.meta.env.VITE_API_KEY || 'dev-api-key-123'

function getRelativeTime(iso: string | null): string {
  if (!iso) return '—'
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)
  if (diff < 60) return 'agora'
  if (diff < 3600) return `há ${Math.floor(diff / 60)} min`
  if (diff < 86400) return `há ${Math.floor(diff / 3600)} h`
  return `há ${Math.floor(diff / 86400)} d`
}

const STATUS_CONFIG = {
  collecting: {
    dot: 'bg-emerald-400',
    pulse: true,
    label: 'A recolher dados...',
    labelColor: 'text-emerald-400',
  },
  analysing: {
    dot: 'bg-amber-400',
    pulse: true,
    label: 'A analisar...',
    labelColor: 'text-amber-400',
  },
  idle: {
    dot: 'bg-slate-500',
    pulse: false,
    label: 'Sistema inativo',
    labelColor: 'text-slate-500',
  },
  error: {
    dot: 'bg-red-500',
    pulse: false,
    label: 'Erro',
    labelColor: 'text-red-400',
  },
} as const

export default function StatusBar({ isStreamConnected }: StatusBarProps) {
  const { data, isError } = useQuery<SystemStatus>({
    queryKey: ['system-status'],
    queryFn: async () => {
      const res = await fetch('/api/system/status', {
        headers: { 'X-API-Key': API_KEY, 'Content-Type': 'application/json' },
      })
      if (!res.ok) throw new Error('status fetch failed')
      return res.json()
    },
    refetchInterval: 30_000,
  })

  const status = isError ? 'error' : (data?.overallStatus ?? 'idle')
  const cfg = STATUS_CONFIG[status]

  return (
    <div
      className="w-full flex items-center gap-x-4 px-3 h-6 text-[11px] font-mono select-none"
      style={{ background: '#0d1117', borderTop: '1px solid #21262d' }}
    >
      {/* Status dot + label */}
      <span className="flex items-center gap-1.5 shrink-0">
        <span className="relative flex h-2 w-2">
          {cfg.pulse && (
            <span
              className={`animate-ping absolute inline-flex h-full w-full rounded-full opacity-60 ${cfg.dot}`}
            />
          )}
          <span className={`relative inline-flex rounded-full h-2 w-2 ${cfg.dot}`} />
        </span>
        <span className={`${cfg.labelColor} tracking-wide`}>{cfg.label}</span>
      </span>

      <span className="text-slate-600">│</span>

      {/* Last update */}
      <span className="text-slate-500 shrink-0">
        <span className="text-slate-600">update</span>{' '}
        <span className="text-slate-400">{getRelativeTime(data?.lastCollectionRun ?? null)}</span>
      </span>

      <span className="text-slate-600">│</span>

      {/* Matches today */}
      <span className="text-slate-500 shrink-0">
        <span className="text-slate-600">jogos</span>{' '}
        <span className="text-slate-300">{data?.matchesCollectedToday ?? '—'}</span>
      </span>

      {/* Queue depth — only when non-zero */}
      {(data?.queueDepth ?? 0) > 0 && (
        <>
          <span className="text-slate-600">│</span>
          <span className="text-slate-500 shrink-0">
            <span className="text-slate-600">queue</span>{' '}
            <span className="text-amber-400">{data!.queueDepth}</span>
          </span>
        </>
      )}

      {/* Spacer */}
      <span className="flex-1" />

      {/* SSE stream indicator */}
      <span className="flex items-center gap-1 shrink-0">
        <span
          className={`inline-flex h-1.5 w-1.5 rounded-full ${
            isStreamConnected ? 'bg-emerald-500' : 'bg-slate-600'
          }`}
        />
        <span className={isStreamConnected ? 'text-emerald-600' : 'text-slate-600'}>
          {isStreamConnected ? 'stream' : 'offline'}
        </span>
      </span>
    </div>
  )
}
