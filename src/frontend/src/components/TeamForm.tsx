import { motion } from 'framer-motion'
import { Trophy, Target, Shield } from 'lucide-react'
import type { TeamForm as TeamFormType } from '@/types/stats'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

interface TeamFormProps {
  teamForm: TeamFormType
  isHome?: boolean
}

const resultColors = {
  W: 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30',
  D: 'bg-amber-500/20 text-amber-400 border-amber-500/30',
  L: 'bg-red-500/20 text-red-400 border-red-500/30',
}

const resultLabels = {
  W: 'V',
  D: 'E',
  L: 'D',
}

export function TeamForm({ teamForm, isHome = true }: TeamFormProps) {
  const recentResults = teamForm.last10Games.slice(0, 5)

  return (
    <Card className="border-navy-700/50 bg-navy-900/50">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-lg font-semibold text-slate-200">
            {teamForm.teamName}
            {isHome && <span className="ml-2 text-xs text-gold-400">(Casa)</span>}
            {!isHome && <span className="ml-2 text-xs text-slate-400">(Fora)</span>}
          </CardTitle>
          <div className="flex gap-1">
            {recentResults.map((match, i) => (
              <motion.div
                key={i}
                initial={{ scale: 0 }}
                animate={{ scale: 1 }}
                transition={{ delay: i * 0.05 }}
                className={`w-7 h-7 rounded-md flex items-center justify-center text-xs font-bold border ${resultColors[match.result]}`}
                title={`${match.opponent} (${match.goalsScored}-${match.goalsConceded})`}
              >
                {resultLabels[match.result]}
              </motion.div>
            ))}
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-3 gap-3">
          <div className="text-center p-3 rounded-lg bg-navy-800/50">
            <Trophy className="w-4 h-4 mx-auto mb-1 text-emerald-400" />
            <div className="text-lg font-bold text-emerald-400">{teamForm.wins}</div>
            <div className="text-xs text-slate-400">Vitórias</div>
          </div>
          <div className="text-center p-3 rounded-lg bg-navy-800/50">
            <div className="w-4 h-4 mx-auto mb-1 rounded-full border-2 border-amber-400" />
            <div className="text-lg font-bold text-amber-400">{teamForm.draws}</div>
            <div className="text-xs text-slate-400">Empates</div>
          </div>
          <div className="text-center p-3 rounded-lg bg-navy-800/50">
            <div className="w-4 h-4 mx-auto mb-1 rounded-full bg-red-400/50" />
            <div className="text-lg font-bold text-red-400">{teamForm.losses}</div>
            <div className="text-xs text-slate-400">Derrotas</div>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="flex items-center gap-2 p-2 rounded-lg bg-navy-800/30">
            <Target className="w-4 h-4 text-gold-400" />
            <div>
              <div className="text-sm font-semibold text-slate-200">
                {teamForm.goalsScored} golos
              </div>
              <div className="text-xs text-slate-400">
                {teamForm.avgGoalsScored.toFixed(1)} por jogo
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2 p-2 rounded-lg bg-navy-800/30">
            <Shield className="w-4 h-4 text-slate-400" />
            <div>
              <div className="text-sm font-semibold text-slate-200">
                {teamForm.goalsConceded} golos
              </div>
              <div className="text-xs text-slate-400">
                {teamForm.avgGoalsConceded.toFixed(1)} sofridos
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-2">
          <h4 className="text-sm font-medium text-slate-300">Últimos 5 jogos</h4>
          <div className="space-y-1.5">
            {teamForm.last10Games.slice(0, 5).map((match, i) => (
              <div
                key={i}
                className="flex items-center justify-between p-2 rounded-md bg-navy-800/30 text-sm"
              >
                <div className="flex items-center gap-2">
                  <span className={`w-5 h-5 rounded text-xs flex items-center justify-center font-medium ${resultColors[match.result].split(' ')[0]} ${resultColors[match.result].split(' ')[1]}`}>
                    {resultLabels[match.result]}
                  </span>
                  <span className="text-slate-300">
                    vs {match.opponent}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-xs text-slate-500">
                    {match.isHome ? 'C' : 'F'}
                  </span>
                  <span className="font-mono font-medium text-slate-200">
                    {match.goalsScored}-{match.goalsConceded}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
