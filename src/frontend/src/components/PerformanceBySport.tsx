import { motion } from 'framer-motion'
import { Trophy, Target, TrendingUp, DollarSign } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCurrency, formatPercentage } from '@/lib/utils'

interface PerformanceBySportProps {
  data: {
    sport: string
    totalBets: number
    winRate: number
    profitLoss: number
    roi: number
  }[]
}

const sportLabels: Record<string, string> = {
  'football': 'Futebol',
  'tennis': 'Ténis',
  'basketball': 'Basquetebol',
}

const sportIcons: Record<string, string> = {
  'football': '⚽',
  'tennis': '🎾',
  'basketball': '🏀',
}

export function PerformanceBySport({ data }: PerformanceBySportProps) {
  if (!data || data.length === 0) {
    return (
      <Card className="border-navy-700/50">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Trophy className="w-5 h-5 text-gold-400" />
            Performance por Desporto
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground text-center py-8">
            Sem dados suficientes para análise por desporto
          </p>
        </CardContent>
      </Card>
      )
  }

  const sortedData = [...data].sort((a, b) => b.roi - a.roi)

  return (
    <Card className="border-navy-700/50">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-lg">
          <Trophy className="w-5 h-5 text-gold-400" />
          Performance por Desporto
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {sortedData.map((sport, index) => (
            <motion.div
              key={sport.sport}
              initial={{ opacity: 0, scale: 0.9 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ delay: index * 0.1 }}
              className={`p-4 rounded-lg border ${sport.roi >= 0 ? 'border-emerald-500/30 bg-emerald-500/5' : 'border-red-500/30 bg-red-500/5'}`}
            >
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-3">
                  <span className="text-2xl">{sportIcons[sport.sport] || '🏆'}</span>
                  <h4 className="font-semibold text-slate-200">
                    {sportLabels[sport.sport] || sport.sport}
                  </h4>
                </div>
                <div className={`text-right ${sport.roi >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                  <p className="text-2xl font-bold">{formatPercentage(sport.roi)}</p>
                  <p className="text-xs text-slate-400">ROI</p>
                </div>
              </div>
              
              <div className="grid grid-cols-3 gap-3 text-sm">
                <div className="text-center p-2 rounded bg-navy-800/50">
                  <Target className="w-4 h-4 mx-auto mb-1 text-slate-400" />
                  <p className="font-medium text-slate-200">{sport.totalBets}</p>
                  <p className="text-xs text-slate-500">Apostas</p>
                </div>
                
                <div className="text-center p-2 rounded bg-navy-800/50">
                  <TrendingUp className="w-4 h-4 mx-auto mb-1 text-slate-400" />
                  <p className="font-medium text-slate-200">{formatPercentage(sport.winRate)}</p>
                  <p className="text-xs text-slate-500">Win Rate</p>
                </div>
                
                <div className="text-center p-2 rounded bg-navy-800/50">
                  <DollarSign className="w-4 h-4 mx-auto mb-1 text-slate-400" />
                  <p className={`font-medium ${sport.profitLoss >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                    {sport.profitLoss >= 0 ? '+' : ''}{formatCurrency(sport.profitLoss)}
                  </p>
                  <p className="text-xs text-slate-500">P&L</p>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
