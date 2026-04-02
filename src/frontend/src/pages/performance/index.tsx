import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'

interface AiPerformance {
  totalResolved: number
  wins: number
  losses: number
  voids: number
  winRate: number
  yield: number
  totalStaked: number
  totalProfit: number
  bestStreak: number
  worstStreak: number
  simulatedKellyProfit: number
}

const API_KEY = import.meta.env.VITE_API_KEY || 'dev-api-key-123'
const PERIODS = [
  { label: 'Último mês', days: 30 },
  { label: '3 meses',    days: 90 },
  { label: 'Tudo',       days: 365 },
] as const

function fmt(n: number, decimals = 1) {
  return n.toFixed(decimals)
}
function fmtEur(n: number) {
  const sign = n >= 0 ? '+' : ''
  return `${sign}€${Math.abs(n).toFixed(2)}`
}
function fmtPct(n: number) {
  const sign = n >= 0 ? '+' : ''
  return `${sign}${(n * 100).toFixed(1)}%`
}

type Sign = 'pos' | 'neg' | 'neu'
function sign(n: number): Sign {
  return n > 0 ? 'pos' : n < 0 ? 'neg' : 'neu'
}

const COLOR: Record<Sign, string> = {
  pos: 'text-emerald-400',
  neg: 'text-red-400',
  neu: 'text-amber-400',
}
const BG: Record<Sign, string> = {
  pos: 'border-emerald-900/60',
  neg: 'border-red-900/60',
  neu: 'border-slate-700',
}

interface StatProps {
  label: string
  value: string
  sub?: string
  s?: Sign
}

function Stat({ label, value, sub, s = 'neu' }: StatProps) {
  return (
    <div
      className={`border ${BG[s]} p-4 flex flex-col gap-1`}
      style={{ background: '#0a0f14' }}
    >
      <span className="text-[10px] font-mono text-slate-600 tracking-widest uppercase">
        {label}
      </span>
      <span className={`text-2xl font-mono font-bold tabular-nums ${COLOR[s]}`}>
        {value}
      </span>
      {sub && (
        <span className="text-xs font-mono text-slate-500 tabular-nums">{sub}</span>
      )}
    </div>
  )
}

export default function PerformancePage() {
  const [days, setDays] = useState(30)

  const { data, isLoading, isError } = useQuery<AiPerformance>({
    queryKey: ['ai-performance', days],
    queryFn: async () => {
      const res = await fetch(`/api/stats/ai-performance?days=${days}`, {
        headers: { 'X-API-Key': API_KEY, 'Content-Type': 'application/json' },
      })
      if (!res.ok) throw new Error('fetch failed')
      return res.json()
    },
  })

  const d = data ?? {
    totalResolved: 0, wins: 0, losses: 0, voids: 0,
    winRate: 0, yield: 0, totalStaked: 0, totalProfit: 0,
    bestStreak: 0, worstStreak: 0, simulatedKellyProfit: 0,
  }

  const settled = d.wins + d.losses

  return (
    <div className="min-h-screen font-mono" style={{ background: '#080c10', color: '#94a3b8' }}>
      {/* Header bar */}
      <div
        className="flex items-center justify-between px-6 py-3 border-b border-slate-800"
        style={{ background: '#0a0f14' }}
      >
        <div className="flex items-center gap-4">
          <span className="text-[10px] text-slate-600 tracking-widest uppercase">
            SISTEMA&nbsp;/&nbsp;PERFORMANCE
          </span>
          <span className="text-[10px] text-slate-700">│</span>
          <span className="text-[10px] text-slate-600 tabular-nums">
            {new Date().toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}
          </span>
        </div>

        {/* Period selector */}
        <div className="flex">
          {PERIODS.map((p, i) => (
            <button
              key={p.days}
              onClick={() => setDays(p.days)}
              className={[
                'px-3 py-1 text-[10px] tracking-widest uppercase transition-colors duration-100',
                'border-t border-b border-r border-slate-800',
                i === 0 ? 'border-l' : '',
                days === p.days
                  ? 'bg-slate-800 text-amber-400'
                  : 'text-slate-600 hover:text-slate-400 hover:bg-slate-900',
              ].join(' ')}
            >
              {p.label}
            </button>
          ))}
        </div>
      </div>

      {/* Body */}
      <div className="p-6">
        {isError && (
          <p className="text-red-500 text-xs font-mono mb-4">
            ERRO: não foi possível carregar dados de performance
          </p>
        )}

        {isLoading && (
          <p className="text-slate-600 text-xs font-mono mb-4 animate-pulse">
            A CARREGAR...
          </p>
        )}

        {/* Primary metrics row */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-px bg-slate-800 border border-slate-800 mb-px">
          <Stat
            label="Taxa de acerto"
            value={`${fmt(d.winRate * 100)}%`}
            sub={`${d.wins}W / ${d.losses}L de ${settled}`}
            s={d.winRate >= 0.55 ? 'pos' : d.winRate >= 0.45 ? 'neu' : 'neg'}
          />
          <Stat
            label="ROI"
            value={fmtPct(d.yield)}
            sub={`staked €${d.totalStaked.toFixed(2)}`}
            s={sign(d.yield)}
          />
          <Stat
            label="Lucro total"
            value={fmtEur(d.totalProfit)}
            s={sign(d.totalProfit)}
          />
          <Stat
            label="Kelly simulado"
            value={fmtEur(d.simulatedKellyProfit)}
            sub="lucro com Kelly fracionário"
            s={sign(d.simulatedKellyProfit)}
          />
        </div>

        {/* Secondary metrics row */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-px bg-slate-800 border border-slate-800">
          <Stat
            label="Total apostas"
            value={String(d.totalResolved)}
            sub={`${d.voids} anuladas`}
            s="neu"
          />
          <Stat
            label="Vitórias"
            value={String(d.wins)}
            sub={d.wins > 0 ? `última série: ${d.bestStreak}` : '—'}
            s={d.wins > 0 ? 'pos' : 'neu'}
          />
          <Stat
            label="Melhor série"
            value={`${d.bestStreak}W`}
            s={d.bestStreak >= 5 ? 'pos' : 'neu'}
          />
          <Stat
            label="Pior série"
            value={`${d.worstStreak}L`}
            s={d.worstStreak >= 4 ? 'neg' : 'neu'}
          />
        </div>

        {/* Empty state */}
        {!isLoading && d.totalResolved === 0 && (
          <div className="mt-8 border border-slate-800 p-8 text-center" style={{ background: '#0a0f14' }}>
            <p className="text-slate-600 text-xs tracking-widest uppercase">
              SEM DADOS PARA O PERÍODO SELECIONADO
            </p>
            <p className="text-slate-700 text-xs mt-1">
              Regista resultados com os botões WON / LOST nas recomendações
            </p>
          </div>
        )}
      </div>
    </div>
  )
}
