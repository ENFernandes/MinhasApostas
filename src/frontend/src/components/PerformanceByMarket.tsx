import { motion } from 'framer-motion'
import { BarChart3, Target, TrendingUp, DollarSign } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatCurrency, formatPercentage } from '@/lib/utils'

interface PerformanceByMarketProps {
  data: {
    market: string
    totalBets: number
    winRate: number
    profitLoss: number
    roi: number
  }[]
}

const marketLabels: Record<string, string> = {
  'h2h': '1X2 - Resultado',
  'totals': 'Over/Under',
  'btts': 'Ambas Marcam',
  'spreads': 'Handicap',
  '1x2': '1X2 - Resultado',
  'over_under': 'Over/Under',
}

export function PerformanceByMarket({ data }: PerformanceByMarketProps) {
  if (!data || data.length === 0) {
    return (
      <Card className="border-navy-700/50">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-lg">
            <BarChart3 className="w-5 h-5 text-gold-400" />
            Performance por Mercado
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground text-center py-8">
            Sem dados suficientes para análise por mercado
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
          <BarChart3 className="w-5 h-5 text-gold-400" />
          Performance por Mercado
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          {sortedData.map((market, index) => (
            <motion.div
              key={market.market}
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: index * 0.1 }}
              className="p-4 rounded-lg bg-navy-800/30 border border-navy-700/30 hover:border-navy-600/50 transition-colors"
            >
              <div className="flex items-center justify-between mb-3">
                <h4 className="font-semibold text-slate-200">
                  {marketLabels[market.market] || market.market}
                </h4>
                <span className={`text-sm font-medium ${market.roi >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                  ROI: {formatPercentage(market.roi)}
                </span>
              </div>
              
              <div className="grid grid-cols-4 gap-4 text-sm">
                <div className="flex items-center gap-2">
                  <Target className="w-4 h-4 text-slate-500" />
                  <div>
                    <p className="text-slate-400">Apostas</p>
                    <p className="font-medium text-slate-200">{market.totalBets}</p>
                  </div>
                </div>
                
                <div className="flex items-center gap-2">
                  <TrendingUp className="w-4 h-4 text-slate-500" />
                  <div>
                    <p className="text-slate-400">Win Rate</p>
                    <p className="font-medium text-slate-200">{formatPercentage(market.winRate)}</p>
                  </div>
                </div>
                
                <div className="flex items-center gap-2 col-span-2">
                  <DollarSign className="w-4 h-4 text-slate-500" />
                  <div>
                    <p className="text-slate-400">P&L</p>
                    <p className={`font-medium ${market.profitLoss >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                      {market.profitLoss >= 0 ? '+' : ''}{formatCurrency(market.profitLoss)}
                    </p>
                  </div>
                </div>
              </div>

              {/* Progress bar for visual ROI comparison */}
              <div className="mt-3">
                <div className="h-2 bg-navy-700 rounded-full overflow-hidden">
                  <motion.div
                    initial={{ width: 0 }}
                    animate={{ width: `${Math.min(Math.max((market.roi + 20) / 40 * 100, 0), 100)}%` }}
                    transition={{ delay: 0.5 + index * 0.1, duration: 0.5 }}
                    className={`h-full ${market.roi >= 0 ? 'bg-emerald-500' : 'bg-red-500'}`}
                  />
                </div>
                <div className="flex justify-between text-xs text-slate-500 mt-1">
                  <span>-20%</span>
                  <span>0%</span>
                  <span>+20%</span>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}
