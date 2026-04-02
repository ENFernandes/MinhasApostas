import { motion } from 'framer-motion'
import { Activity, Layers, Trophy } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import type { TennisPlayerStats } from '@/hooks/useTennisStatsApi'
import { formatDate } from '@/lib/utils'

const surfaceLabel: Record<string, string> = {
  Hard: 'Hard',
  Clay: 'Clay',
  Grass: 'Grass',
  Carpet: 'Carpet',
  Unknown: '—',
}

const resultStyle = (won: boolean) =>
  won
    ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30'
    : 'bg-red-500/20 text-red-300 border-red-500/30'

export function TennisPlayerStatsCard({
  sideLabel,
  stats,
}: {
  sideLabel: string
  stats: TennisPlayerStats
}) {
  const results = stats.recentResults ?? []
  const last5 = results.slice(0, 5)

  const surfaces = Object.entries(stats.eloBySurface ?? {})
    .sort((a, b) => b[1] - a[1])
    .slice(0, 4)

  return (
    <Card className="border-navy-700/50 bg-navy-900/50">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between gap-3">
          <CardTitle className="text-lg font-semibold text-slate-200">
            {stats.playerName}{' '}
            <span className="ml-2 text-xs text-slate-400">({sideLabel})</span>
          </CardTitle>
          <div className="flex gap-1">
            {last5.map((r, i) => (
              <motion.div
                key={`${r.date}-${i}`}
                initial={{ scale: 0 }}
                animate={{ scale: 1 }}
                transition={{ delay: i * 0.05 }}
                className={`w-7 h-7 rounded-md flex items-center justify-center text-xs font-bold border ${resultStyle(
                  r.won
                )}`}
                title={`${r.surface} • ${r.tour} • ${r.won ? 'W' : 'L'}`}
              >
                {r.won ? 'V' : 'D'}
              </motion.div>
            ))}
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div className="flex items-center gap-2 p-2 rounded-lg bg-navy-800/30">
            <Trophy className="w-4 h-4 text-gold-400" />
            <div>
              <div className="text-sm font-semibold text-slate-200">ELO (melhor)</div>
              <div className="text-xs text-slate-400">
                {surfaces.length ? `${surfaces[0][0]} • ${surfaces[0][1].toFixed(1)}` : '—'}
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2 p-2 rounded-lg bg-navy-800/30">
            <Activity className="w-4 h-4 text-emerald-400" />
            <div>
              <div className="text-sm font-semibold text-slate-200">Forma (5)</div>
              <div className="text-xs text-slate-400">
                {last5.filter((x) => x.won).length}V / {last5.filter((x) => !x.won).length}D
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-2">
          <h4 className="text-sm font-medium text-slate-300 flex items-center gap-2">
            <Layers className="w-4 h-4" />
            ELO por superfície
          </h4>
          {surfaces.length ? (
            <div className="grid grid-cols-2 gap-2">
              {surfaces.map(([surface, elo]) => (
                <div key={surface} className="p-2 rounded-md bg-navy-800/30">
                  <div className="text-xs text-slate-400">{surfaceLabel[surface] ?? surface}</div>
                  <div className="font-mono font-semibold text-slate-200">{elo.toFixed(1)}</div>
                </div>
              ))}
            </div>
          ) : (
            <div className="text-sm text-slate-500">Sem ELO disponível.</div>
          )}
        </div>

        <div className="space-y-2">
          <h4 className="text-sm font-medium text-slate-300">Últimos resultados</h4>
          <div className="space-y-1.5">
            {results.slice(0, 5).map((r, i) => (
              <div
                key={`${r.date}-${r.surface}-${i}`}
                className="flex items-center justify-between p-2 rounded-md bg-navy-800/30 text-sm"
              >
                <div className="flex items-center gap-2">
                  <span
                    className={`w-5 h-5 rounded text-xs flex items-center justify-center font-medium border ${resultStyle(
                      r.won
                    )}`}
                  >
                    {r.won ? 'V' : 'D'}
                  </span>
                  <span className="text-slate-300">{surfaceLabel[r.surface] ?? r.surface}</span>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-xs text-slate-500">{r.tour || '—'}</span>
                  <span className="text-xs text-slate-500">{formatDate(r.date)}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

