import { motion } from 'framer-motion'
import { Building2, TrendingUp, AlertCircle } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { ValueBadge, getOddClassification } from './ValueBadge'
import { formatCurrency } from '@/lib/utils'
import { useAppStore } from '@/stores/appStore'

interface Odd {
  id: string
  bookmaker: string
  market: string
  outcome: string
  odd: number
  impliedProbability: number
}

interface OddsTableProps {
  market: string
  odds: Odd[]
  modelProbability?: number
  confidence?: number
  showAnalysis?: boolean
}

export function OddsTable({ 
  market, 
  odds, 
  modelProbability, 
  confidence = 5,
  showAnalysis = false 
}: OddsTableProps) {
  const { settings } = useAppStore()

  const oddsWithAnalysis = odds.map(odd => {
    if (!modelProbability) return { ...odd, analysis: null }
    
    const impliedProb = 1 / odd.odd
    const value = (modelProbability - impliedProb) / impliedProb
    const kelly = (modelProbability * odd.odd - 1) / (odd.odd - 1)
    
    const classification = getOddClassification(value, confidence, kelly)
    const stake = classification.stakePercentage * settings.bankroll

    return {
      ...odd,
      analysis: {
        value,
        kelly,
        ...classification,
        stake,
      }
    }
  }).sort((a, b) => {
    if (!a.analysis || !b.analysis) return 0
    return b.analysis.value - a.analysis.value
  })

  return (
    <Card className="border-navy-700/50 bg-navy-900/50">
      <CardHeader className="pb-3">
        <CardTitle className="text-lg font-semibold text-slate-200 flex items-center justify-between">
          <span className="flex items-center gap-2">
            <Building2 className="w-5 h-5 text-gold-400" />
            {market}
          </span>
          {showAnalysis && modelProbability && (
            <Badge variant="outline" className="border-navy-700 text-slate-400">
              <TrendingUp className="w-3 h-3 mr-1" />
              Prob. Modelo: {(modelProbability * 100).toFixed(1)}%
            </Badge>
          )}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Table>
          <TableHeader>
            <TableRow className="border-navy-700/50 hover:bg-transparent">
              <TableHead className="text-slate-400">Casa</TableHead>
              <TableHead className="text-slate-400">Resultado</TableHead>
              <TableHead className="text-slate-400 text-right">Odd</TableHead>
              {showAnalysis && (
                <>
                  <TableHead className="text-slate-400 text-right">Value</TableHead>
                  <TableHead className="text-slate-400 text-center">Classificação</TableHead>
                  <TableHead className="text-slate-400 text-right">Stake</TableHead>
                </>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {oddsWithAnalysis.map((odd, i) => (
              <motion.tr
                key={odd.id}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: i * 0.05 }}
                className={`border-navy-700/30 ${odd.analysis?.isValid ? 'bg-emerald-500/5' : ''}`}
              >
                <TableCell className="text-slate-300">{odd.bookmaker}</TableCell>
                <TableCell className="font-medium text-slate-200">{odd.outcome}</TableCell>
                <TableCell className="text-right font-mono text-slate-200">
                  {odd.odd.toFixed(2)}
                </TableCell>
                {showAnalysis && (
                  <>
                    <TableCell className={`text-right font-mono ${odd.analysis && odd.analysis.value > 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                      {odd.analysis ? `${(odd.analysis.value * 100).toFixed(1)}%` : '-'}
                    </TableCell>
                    <TableCell className="text-center">
                      {odd.analysis ? (
                        <ValueBadge
                          isValid={odd.analysis.isValid}
                          value={odd.analysis.value}
                          confidence={confidence}
                          kellyFraction={odd.analysis.stakePercentage}
                          size="sm"
                        />
                      ) : (
                        <Badge variant="outline" className="border-slate-700 text-slate-500">
                          <AlertCircle className="w-3 h-3 mr-1" />
                          Sem análise
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right font-mono">
                      {odd.analysis?.isValid ? (
                        <span className="text-gold-400">
                          {formatCurrency(odd.analysis.stake)}
                        </span>
                      ) : (
                        <span className="text-slate-600">-</span>
                      )}
                    </TableCell>
                  </>
                )}
              </motion.tr>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  )
}
