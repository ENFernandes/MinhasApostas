
import { motion } from 'framer-motion'
import { 
  TrendingUp, 
  TrendingDown, 
  Target, 
  Percent,
  Wallet,
  Calendar,
  Clock,
  AlertCircle
} from 'lucide-react'

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { BankrollChart } from '@/components/BankrollChart'
import { PerformanceByMarket } from '@/components/PerformanceByMarket'
import { PerformanceBySport } from '@/components/PerformanceBySport'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Badge } from '@/components/ui/badge'
import { usePerformanceStats, useBetHistory, usePendingBets } from '@/hooks/useApi'
import { formatCurrency, formatPercentage } from '@/lib/utils'
import type { BetResult } from '@/types'

function calculateChartData(bets: BetResult[] | undefined) {
  if (!bets || bets.length === 0) return []
  
  const sortedBets = [...bets].sort((a, b) => 
    new Date(a.settledAt).getTime() - new Date(b.settledAt).getTime()
  )
  
  let cumulativeValue = 100
  const chartData = sortedBets.map((bet) => {
    cumulativeValue += bet.profitLoss
    return {
      date: bet.settledAt,
      value: cumulativeValue,
    }
  })
  
  return chartData
}

export default function HistoryPage() {
  const { data: stats } = usePerformanceStats()
  const { data: betHistory, isLoading: isLoadingBets } = useBetHistory()
  const { data: pendingBets, isLoading: isLoadingPending } = usePendingBets()

  const chartData = calculateChartData(betHistory)

  // Calculate total at risk
  const totalAtRisk = pendingBets?.reduce((sum, bet) => sum + bet.stakeActual, 0) || 0

  return (
    <div className="min-h-screen bg-gradient-to-br from-background via-background to-navy-950">
      {/* Header */}
      <header className="border-b border-navy-700/50 bg-background/80 backdrop-blur-xl">
        <div className="container mx-auto px-4 py-6">
          <h1 className="text-3xl font-display font-bold gradient-text">
            Histórico de Apostas
          </h1>
          <p className="text-muted-foreground mt-1">
            Acompanhe o seu desempenho e evolução da banca
          </p>
        </div>
      </header>

      <main className="container mx-auto px-4 py-8">
        {/* Stats Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0 }}
          >
            <Card className="border-navy-700/50 bg-gradient-to-br from-card to-navy-900/30">
              <CardContent className="p-6">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm text-muted-foreground">P&L Total</p>
                    <p className={`text-2xl font-bold ${(stats?.totalProfitLoss ?? 0) >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                      {(stats?.totalProfitLoss ?? 0) >= 0 ? '+' : ''}{formatCurrency(stats?.totalProfitLoss ?? 0)}
                    </p>
                  </div>
                  <div className={`p-3 rounded-full ${(stats?.totalProfitLoss ?? 0) >= 0 ? 'bg-emerald-500/10' : 'bg-red-500/10'}`}>
                    {(stats?.totalProfitLoss ?? 0) >= 0 ? (
                      <TrendingUp className="w-6 h-6 text-emerald-400" />
                    ) : (
                      <TrendingDown className="w-6 h-6 text-red-400" />
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
          >
            <Card className="border-navy-700/50 bg-gradient-to-br from-card to-navy-900/30">
              <CardContent className="p-6">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm text-muted-foreground">ROI</p>
                    <p className="text-2xl font-bold text-gold-400">
                      {formatPercentage(stats?.roi ?? 0)}
                    </p>
                  </div>
                  <div className="p-3 rounded-full bg-gold-500/10">
                    <Percent className="w-6 h-6 text-gold-400" />
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
          >
            <Card className="border-navy-700/50 bg-gradient-to-br from-card to-navy-900/30">
              <CardContent className="p-6">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm text-muted-foreground">Win Rate</p>
                    <p className="text-2xl font-bold text-navy-200">
                      {formatPercentage(stats?.winRate ?? 0)}
                    </p>
                  </div>
                  <div className="p-3 rounded-full bg-navy-700/50">
                    <Target className="w-6 h-6 text-navy-300" />
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.3 }}
          >
            <Card className="border-navy-700/50 bg-gradient-to-br from-card to-navy-900/30">
              <CardContent className="p-6">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm text-muted-foreground">Total Apostas</p>
                    <p className="text-2xl font-bold text-navy-200">
                      {stats?.totalBets ?? 0}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {stats?.winningBets ?? 0}V / {stats?.losingBets ?? 0}D
                    </p>
                  </div>
                  <div className="p-3 rounded-full bg-navy-700/50">
                    <Wallet className="w-6 h-6 text-navy-300" />
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>
        </div>

        {/* Chart */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.4 }}
          className="mb-8"
        >
          <BankrollChart data={chartData} />
        </motion.div>

        {/* Performance Breakdown */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.5 }}
          >
            <PerformanceBySport data={stats?.bySport || []} />
          </motion.div>
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.6 }}
          >
            <PerformanceByMarket data={stats?.byMarket || []} />
          </motion.div>
        </div>

        {/* Pending Bets Summary Card */}
        {pendingBets && pendingBets.length > 0 && (
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.5 }}
            className="mb-6"
          >
            <Card className="border-amber-500/30 bg-amber-500/5">
              <CardContent className="p-6">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="p-2 rounded-lg bg-amber-500/10">
                      <AlertCircle className="w-6 h-6 text-amber-400" />
                    </div>
                    <div>
                      <p className="text-sm text-slate-400">Apostas em Aberto</p>
                      <p className="text-2xl font-bold text-slate-200">
                        {pendingBets.length} apostas
                      </p>
                    </div>
                  </div>
                  <div className="text-right">
                    <p className="text-sm text-slate-400">Total em Risco</p>
                    <p className="text-2xl font-bold text-amber-400">
                      {formatCurrency(totalAtRisk)}
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>
        )}

        {/* Tabs for History and Pending */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.6 }}
        >
          <Tabs defaultValue="history" className="w-full">
            <TabsList className="bg-navy-800/50 border border-navy-700 mb-6">
              <TabsTrigger value="history" className="data-[state=active]:bg-gold-500 data-[state=active]:text-navy-950">
                <Calendar className="w-4 h-4 mr-2" />
                Histórico
              </TabsTrigger>
              <TabsTrigger value="pending" className="data-[state=active]:bg-amber-500 data-[state=active]:text-navy-950">
                <Clock className="w-4 h-4 mr-2" />
                Pendentes
                {pendingBets && pendingBets.length > 0 && (
                  <Badge variant="secondary" className="ml-2 bg-amber-500/20 text-amber-400">
                    {pendingBets.length}
                  </Badge>
                )}
              </TabsTrigger>
            </TabsList>

            <TabsContent value="history">
              <Card className="border-navy-700/50">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <Calendar className="w-5 h-5 text-gold-400" />
                    Apostas Recentes
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="overflow-x-auto">
                    <table className="w-full">
                      <thead>
                        <tr className="border-b border-navy-700">
                          <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Data</th>
                          <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Jogo</th>
                          <th className="text-center py-3 px-4 text-sm font-medium text-muted-foreground">Resultado</th>
                          <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">P&L</th>
                        </tr>
                      </thead>
                      <tbody>
                        {isLoadingBets ? (
                          <tr>
                            <td colSpan={4} className="py-8 text-center text-muted-foreground">
                              A carregar histórico...
                            </td>
                          </tr>
                        ) : !betHistory || betHistory.length === 0 ? (
                          <tr>
                            <td colSpan={4} className="py-8 text-center text-muted-foreground">
                              Nenhuma aposta registada
                            </td>
                          </tr>
                        ) : (
                          betHistory.map((bet, index) => (
                            <motion.tr
                              key={bet.id}
                              initial={{ opacity: 0, x: -20 }}
                              animate={{ opacity: 1, x: 0 }}
                              transition={{ delay: 0.1 * index }}
                              className="border-b border-navy-800 hover:bg-navy-800/30 transition-colors"
                            >
                              <td className="py-4 px-4 text-sm">
                                {new Date(bet.settledAt).toLocaleDateString('pt-PT')}
                              </td>
                              <td className="py-4 px-4 font-medium">
                                {bet.match?.homeTeam} vs {bet.match?.awayTeam}
                              </td>
                              <td className="py-4 px-4 text-center">
                                <span className={`inline-flex px-2 py-1 rounded text-xs font-medium ${
                                  bet.outcome === 'WIN'
                                    ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'
                                    : bet.outcome === 'VOID'
                                    ? 'bg-amber-500/10 text-amber-400 border border-amber-500/20'
                                    : 'bg-red-500/10 text-red-400 border border-red-500/20'
                                }`}>
                                  {bet.outcome === 'WIN' ? 'Vitória' : bet.outcome === 'VOID' ? 'Anulada' : 'Derrota'}
                                </span>
                              </td>
                              <td className={`py-4 px-4 text-right font-mono font-medium ${
                                bet.profitLoss >= 0 ? 'text-emerald-400' : 'text-red-400'
                              }`}>
                                {bet.profitLoss >= 0 ? '+' : ''}{formatCurrency(bet.profitLoss)}
                              </td>
                            </motion.tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="pending">
              <Card className="border-navy-700/50">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <Clock className="w-5 h-5 text-amber-400" />
                    Apostas Pendentes
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="overflow-x-auto">
                    <table className="w-full">
                      <thead>
                        <tr className="border-b border-navy-700">
                          <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Jogo</th>
                          <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Aposta</th>
                          <th className="text-center py-3 px-4 text-sm font-medium text-muted-foreground">Odd</th>
                          <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Stake</th>
                          <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Potencial</th>
                        </tr>
                      </thead>
                      <tbody>
                        {isLoadingPending ? (
                          <tr>
                            <td colSpan={5} className="py-8 text-center text-muted-foreground">
                              A carregar apostas pendentes...
                            </td>
                          </tr>
                        ) : !pendingBets || pendingBets.length === 0 ? (
                          <tr>
                            <td colSpan={5} className="py-8 text-center text-muted-foreground">
                              Nenhuma aposta pendente
                            </td>
                          </tr>
                        ) : (
                          pendingBets.map((bet, index) => (
                            <motion.tr
                              key={bet.id}
                              initial={{ opacity: 0, x: -20 }}
                              animate={{ opacity: 1, x: 0 }}
                              transition={{ delay: 0.1 * index }}
                              className="border-b border-navy-800 hover:bg-navy-800/30 transition-colors"
                            >
                              <td className="py-4 px-4 font-medium">
                                {bet.match?.homeTeam} vs {bet.match?.awayTeam}
                              </td>
                              <td className="py-4 px-4 text-sm">
                                {bet.recommendation?.market} - {bet.recommendation?.outcome}
                              </td>
                              <td className="py-4 px-4 text-center font-mono">
                                {bet.oddActual.toFixed(2)}
                              </td>
                              <td className="py-4 px-4 text-right font-mono text-amber-400">
                                {formatCurrency(bet.stakeActual)}
                              </td>
                              <td className="py-4 px-4 text-right font-mono text-emerald-400">
                                +{formatCurrency(bet.stakeActual * (bet.oddActual - 1))}
                              </td>
                            </motion.tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                </CardContent>
              </Card>
            </TabsContent>
          </Tabs>
        </motion.div>
      </main>
    </div>
  )
}
