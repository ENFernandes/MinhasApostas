import { motion } from 'framer-motion'
import { History, Swords, TrendingUp } from 'lucide-react'
import type { HeadToHeadStats as HeadToHeadStatsType } from '@/types/stats'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { formatDate } from '@/lib/utils'

interface HeadToHeadStatsProps {
  stats: HeadToHeadStatsType
  homeTeamName: string
  awayTeamName: string
}

export function HeadToHeadStats({ stats, homeTeamName, awayTeamName }: HeadToHeadStatsProps) {
  const totalGames = stats.totalMatches || 1
  const homeWinPct = (stats.homeTeamWins / totalGames) * 100
  const drawPct = (stats.draws / totalGames) * 100
  const awayWinPct = (stats.awayTeamWins / totalGames) * 100

  return (
    <Card className="border-navy-700/50 bg-navy-900/50">
      <CardHeader className="pb-3">
        <CardTitle className="text-lg font-semibold text-slate-200 flex items-center gap-2">
          <History className="w-5 h-5 text-gold-400" />
          Confrontos Diretos
          <span className="text-sm font-normal text-slate-400">
            ({stats.totalMatches} jogos)
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-3 gap-4">
          <div className="text-center">
            <div className="text-2xl font-bold text-emerald-400">
              {stats.homeTeamWins}
            </div>
            <div className="text-xs text-slate-400 mt-1 truncate">
              {homeTeamName}
            </div>
            <div className="text-xs text-emerald-500/70">
              {homeWinPct.toFixed(0)}%
            </div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-amber-400">
              {stats.draws}
            </div>
            <div className="text-xs text-slate-400 mt-1">Empates</div>
            <div className="text-xs text-amber-500/70">
              {drawPct.toFixed(0)}%
            </div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-red-400">
              {stats.awayTeamWins}
            </div>
            <div className="text-xs text-slate-400 mt-1 truncate">
              {awayTeamName}
            </div>
            <div className="text-xs text-red-500/70">
              {awayWinPct.toFixed(0)}%
            </div>
          </div>
        </div>

        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <div className="w-20 text-xs text-slate-400 truncate">
              {homeTeamName}
            </div>
            <Progress value={homeWinPct} className="flex-1 h-2" />
            <div className="w-12 text-xs text-right text-emerald-400">
              {homeWinPct.toFixed(0)}%
            </div>
          </div>
          <div className="flex items-center gap-2">
            <div className="w-20 text-xs text-slate-400">Empates</div>
            <Progress value={drawPct} className="flex-1 h-2" />
            <div className="w-12 text-xs text-right text-amber-400">
              {drawPct.toFixed(0)}%
            </div>
          </div>
          <div className="flex items-center gap-2">
            <div className="w-20 text-xs text-slate-400 truncate">
              {awayTeamName}
            </div>
            <Progress value={awayWinPct} className="flex-1 h-2" />
            <div className="w-12 text-xs text-right text-red-400">
              {awayWinPct.toFixed(0)}%
            </div>
          </div>
        </div>

        <div className="flex items-center justify-between p-3 rounded-lg bg-navy-800/50">
          <div className="flex items-center gap-2">
            <Swords className="w-4 h-4 text-gold-400" />
            <span className="text-sm text-slate-300">Total de Golos</span>
          </div>
          <div className="text-lg font-bold text-slate-200">
            {stats.homeTeamGoals} - {stats.awayTeamGoals}
          </div>
        </div>

        {stats.matches.length > 0 && (
          <div className="space-y-2">
            <h4 className="text-sm font-medium text-slate-300 flex items-center gap-2">
              <TrendingUp className="w-4 h-4" />
              Últimos confrontos
            </h4>
            <div className="space-y-1.5">
              {stats.matches.slice(0, 3).map((match, i) => (
                <motion.div
                  key={i}
                  initial={{ opacity: 0, x: -10 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: i * 0.1 }}
                  className="flex items-center justify-between p-2 rounded-md bg-navy-800/30 text-sm"
                >
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-slate-500">
                      {formatDate(match.date)}
                    </span>
                    <span className="text-slate-400 text-xs">
                      {match.competition}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className={`text-xs ${match.result === 'H' ? 'text-emerald-400' : match.result === 'A' ? 'text-red-400' : 'text-amber-400'}`}>
                      {match.homeTeam}
                    </span>
                    <span className="font-mono font-medium text-slate-200">
                      {match.homeScore}-{match.awayScore}
                    </span>
                    <span className={`text-xs ${match.result === 'A' ? 'text-emerald-400' : match.result === 'H' ? 'text-red-400' : 'text-amber-400'}`}>
                      {match.awayTeam}
                    </span>
                  </div>
                </motion.div>
              ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
