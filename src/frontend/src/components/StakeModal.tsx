import { useState, useEffect } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { X, Wallet, Calculator, Trophy } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useAppStore } from '@/stores/appStore'
import { useRegisterBet } from '@/hooks/useApi'
import { formatCurrency } from '@/lib/utils'

export function StakeModal() {
  const {
    isStakeModalOpen,
    selectedMatchId,
    stakeModalMatch,
    stakeModalRecommendation,
    closeStakeModal,
    settings
  } = useAppStore()
  const registerBet = useRegisterBet()
  const [stake, setStake] = useState<number>(0)
  const [odd, setOdd] = useState<number>(0)

  const rec = stakeModalRecommendation
  const match = stakeModalMatch

  useEffect(() => {
    if (rec) {
      const suggestedOdd = rec.odd_decimal || rec.odd || 0
      setOdd(suggestedOdd)
    }
  }, [rec])

  const handleSubmit = async () => {
    if (!selectedMatchId || stake <= 0 || odd <= 0) return

    await registerBet.mutateAsync({
      recommendationId: rec?.id || '00000000-0000-0000-0000-000000000000',
      matchId: selectedMatchId,
      market: rec?.market || 'N/A',
      outcome: rec?.outcome || 'N/A',
      bookmaker: rec?.bookmaker || 'N/A',
      stakeActual: stake,
      oddActual: odd,
      outcome2: 'PENDING',
      profitLoss: 0,
    })

    closeStakeModal()
    setStake(0)
    setOdd(0)
  }

  return (
    <AnimatePresence>
      {isStakeModalOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50"
            onClick={closeStakeModal}
          />

          {/* Modal */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            className="fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-full max-w-md z-50"
          >
            <Card className="border-gold-500/20 shadow-2xl shadow-gold-500/10">
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle className="flex items-center gap-2 text-xl">
                  <Wallet className="w-5 h-5 text-gold-400" />
                  Registar Aposta
                </CardTitle>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8"
                  onClick={closeStakeModal}
                >
                  <X className="w-4 h-4" />
                </Button>
              </CardHeader>

              <CardContent className="space-y-5">
                {/* Match Context */}
                {match && (
                  <div className="p-3 rounded-lg bg-navy-800/50 border border-navy-700">
                    <p className="text-xs text-slate-400 mb-1">Jogo</p>
                    <p className="font-semibold text-slate-200">
                      {match.homeTeam} vs {match.awayTeam}
                    </p>
                  </div>
                )}

                {/* Recommendation Context */}
                {rec && (
                  <div className="p-3 rounded-lg bg-gold-500/5 border border-gold-500/20">
                    <div className="flex items-center gap-2 mb-2">
                      <Trophy className="w-4 h-4 text-gold-400" />
                      <span className="text-xs font-medium text-gold-400">Recomendação da IA</span>
                    </div>
                    <div className="grid grid-cols-3 gap-2 text-sm">
                      <div>
                        <p className="text-xs text-slate-400">Mercado</p>
                        <p className="font-medium text-slate-200">{rec.market}</p>
                      </div>
                      <div>
                        <p className="text-xs text-slate-400">Outcome</p>
                        <p className="font-medium text-slate-200">{rec.outcome}</p>
                      </div>
                      <div>
                        <p className="text-xs text-slate-400">Casa</p>
                        <p className="font-medium text-slate-200">{rec.bookmaker || 'N/A'}</p>
                      </div>
                    </div>
                  </div>
                )}

                {/* Bankroll Info */}
                <div className="p-4 rounded-lg bg-navy-800/50 border border-navy-700">
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-sm text-muted-foreground">Banca Disponível</span>
                    <span className="text-lg font-semibold text-gold-400">
                      {formatCurrency(settings.bankroll)}
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-muted-foreground">Stake Máxima (5%)</span>
                    <span className="text-sm font-medium">
                      {formatCurrency(settings.bankroll * 0.05)}
                    </span>
                  </div>
                </div>

                {/* Odd Input */}
                <div className="space-y-2">
                  <label className="text-sm font-medium flex items-center gap-2">
                    Odd
                    {rec && (
                      <span className="text-xs text-slate-400">(sugerida: {(rec.odd_decimal || rec.odd || 0).toFixed(2)})</span>
                    )}
                  </label>
                  <input
                    type="number"
                    value={odd || ''}
                    onChange={(e) => setOdd(parseFloat(e.target.value) || 0)}
                    className="w-full px-4 py-3 rounded-lg bg-navy-800 border border-navy-700
                               focus:border-gold-500 focus:ring-1 focus:ring-gold-500
                               transition-all outline-none text-lg"
                    placeholder="1.50"
                    min="1"
                    step="0.01"
                  />
                </div>

                {/* Stake Input */}
                <div className="space-y-2">
                  <label className="text-sm font-medium flex items-center gap-2">
                    <Calculator className="w-4 h-4 text-gold-400" />
                    Valor da Aposta
                  </label>
                  <div className="relative">
                    <span className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground">
                      €
                    </span>
                    <input
                      type="number"
                      value={stake || ''}
                      onChange={(e) => setStake(parseFloat(e.target.value) || 0)}
                      className="w-full pl-8 pr-4 py-3 rounded-lg bg-navy-800 border border-navy-700
                                 focus:border-gold-500 focus:ring-1 focus:ring-gold-500
                                 transition-all outline-none text-lg"
                      placeholder="0.00"
                      min="0"
                      step="0.50"
                    />
                  </div>

                  {/* Quick stake buttons */}
                  <div className="flex gap-2">
                    {[5, 10, 25, 50].map((amount) => (
                      <button
                        key={amount}
                        onClick={() => setStake(amount)}
                        className="flex-1 py-2 px-3 rounded-md bg-navy-800 hover:bg-navy-700
                                   border border-navy-700 hover:border-gold-500/50
                                   transition-all text-sm font-medium"
                      >
                        €{amount}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Potential return */}
                {stake > 0 && odd > 0 && (
                  <div className="p-3 rounded-lg bg-emerald-500/5 border border-emerald-500/20">
                    <div className="flex items-center justify-between">
                      <span className="text-sm text-slate-400">Retorno Potencial</span>
                      <span className="font-semibold text-emerald-400">
                        {formatCurrency(stake * odd)}
                      </span>
                    </div>
                    <div className="flex items-center justify-between mt-1">
                      <span className="text-sm text-slate-400">Lucro Potencial</span>
                      <span className="font-semibold text-emerald-400">
                        +{formatCurrency(stake * odd - stake)}
                      </span>
                    </div>
                  </div>
                )}

                {/* Submit */}
                <Button
                  className="w-full h-12 bg-gradient-to-r from-gold-500 to-gold-400
                             text-navy-950 font-semibold text-lg
                             hover:from-gold-400 hover:to-gold-300
                             disabled:opacity-50 disabled:cursor-not-allowed"
                  onClick={handleSubmit}
                  disabled={stake <= 0 || odd <= 0 || registerBet.isPending}
                >
                  {registerBet.isPending ? 'A registar...' : 'Confirmar Aposta'}
                </Button>
              </CardContent>
            </Card>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  )
}
