import { motion, AnimatePresence } from 'framer-motion'
import { X, Brain, TrendingUp, Target, Lightbulb, AlertCircle, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { ConfidenceMeter } from './ConfidenceMeter'
import { OddBadge } from './OddBadge'
import { formatCurrency } from '@/lib/utils'
import type { Analysis } from '@/types'

interface AnalysisDetailModalProps {
  analysis?: Analysis | null
  isOpen: boolean
  onClose: () => void
  isLoading?: boolean
  isError?: boolean
}

export function AnalysisDetailModal({ analysis, isOpen, onClose, isLoading, isError }: AnalysisDetailModalProps) {
  const recommendation = analysis?.recommendedMarket
  const hasRecommendation = recommendation !== null

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/70 backdrop-blur-sm z-50"
            onClick={onClose}
          />

          {/* Modal */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            className="fixed inset-4 md:inset-10 lg:inset-20 bg-navy-900 rounded-2xl border border-navy-700/50 shadow-2xl z-50 overflow-hidden flex flex-col"
          >
            {/* Header */}
            <div className="flex items-center justify-between p-6 border-b border-navy-700/50 bg-navy-900/50">
              <div className="flex items-center gap-3">
                <div className="p-2 rounded-lg bg-gradient-to-br from-purple-500 to-purple-600">
                  <Brain className="w-6 h-6 text-white" />
                </div>
                <div>
                  <h2 className="text-xl font-bold text-slate-100">
                    Análise da IA
                  </h2>
                  {analysis && (
                    <p className="text-sm text-slate-400">
                      {analysis.homeTeam} vs {analysis.awayTeam}
                    </p>
                  )}
                </div>
              </div>
              <Button
                variant="ghost"
                size="icon"
                onClick={onClose}
                className="rounded-full hover:bg-navy-700/50"
              >
                <X className="w-5 h-5" />
              </Button>
            </div>

            {/* Content */}
            <div className="flex-1 overflow-y-auto p-6 space-y-6">
              {/* Loading state */}
              {(isLoading || !analysis) ? (
                <div className="flex flex-col items-center justify-center py-16 gap-4">
                  {isError ? (
                    <>
                      <AlertCircle className="w-10 h-10 text-red-400" />
                      <p className="text-red-400">Erro ao gerar análise</p>
                      <p className="text-xs text-slate-500">Verifique se o Ollama está a correr</p>
                    </>
                  ) : (
                    <>
                      <Loader2 className="w-10 h-10 text-purple-400 animate-spin" />
                      <p className="text-slate-400">A gerar análise com IA...</p>
                      <p className="text-xs text-slate-500">Pode demorar alguns segundos</p>
                    </>
                  )}
                </div>
              ) : (
                <>
                  {/* Reasoning Section */}
                  {analysis.reasoning && (
                    <Card className="border-purple-500/30 bg-purple-500/5">
                      <CardHeader className="pb-3">
                        <CardTitle className="text-lg font-semibold text-slate-200 flex items-center gap-2">
                          <Lightbulb className="w-5 h-5 text-purple-400" />
                          Justificação da Análise
                        </CardTitle>
                      </CardHeader>
                      <CardContent>
                        <p className="text-slate-300 leading-relaxed whitespace-pre-wrap">
                          {analysis.reasoning}
                        </p>
                      </CardContent>
                    </Card>
                  )}

                  {/* Recommendation Section */}
                  {hasRecommendation && recommendation && (
                    <Card className="border-gold-500/30 bg-gold-500/5">
                      <CardHeader className="pb-3">
                        <CardTitle className="text-lg font-semibold text-slate-200 flex items-center gap-2">
                          <TrendingUp className="w-5 h-5 text-gold-400" />
                          Recomendação
                        </CardTitle>
                      </CardHeader>
                      <CardContent className="space-y-4">
                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                          <div className="p-3 rounded-lg bg-navy-800/50">
                            <p className="text-xs text-slate-400 mb-1">Mercado</p>
                            <p className="font-semibold text-slate-200">{recommendation.market}</p>
                          </div>
                          <div className="p-3 rounded-lg bg-navy-800/50">
                            <p className="text-xs text-slate-400 mb-1">Outcome</p>
                            <p className="font-semibold text-slate-200">{recommendation.outcome}</p>
                          </div>
                          <div className="p-3 rounded-lg bg-navy-800/50">
                            <p className="text-xs text-slate-400 mb-1">Casa</p>
                            <p className="font-semibold text-slate-200">{recommendation.bookmaker}</p>
                          </div>
                          <div className="p-3 rounded-lg bg-navy-800/50">
                            <p className="text-xs text-slate-400 mb-1">Odd</p>
                            <OddBadge odd={recommendation.odd} value={recommendation.value} size="md" />
                          </div>
                        </div>

                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                          <div className="p-3 rounded-lg bg-navy-800/50">
                            <p className="text-xs text-slate-400 mb-1">Prob. Modelo</p>
                            <p className="font-semibold text-emerald-400">
                              {(recommendation.modelProbability * 100).toFixed(1)}%
                            </p>
                          </div>
                          <div className="p-3 rounded-lg bg-navy-800/50">
                            <p className="text-xs text-slate-400 mb-1">Prob. Implícita</p>
                            <p className="font-semibold text-slate-200">
                              {(recommendation.impliedProbability * 100).toFixed(1)}%
                            </p>
                          </div>
                          <div className="p-3 rounded-lg bg-navy-800/50">
                            <p className="text-xs text-slate-400 mb-1">Value</p>
                            <p className={`font-semibold ${recommendation.value > 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                              +{(recommendation.value * 100).toFixed(1)}%
                            </p>
                          </div>
                          <div className="p-3 rounded-lg bg-navy-800/50">
                            <p className="text-xs text-slate-400 mb-1">Stake Sugerida</p>
                            <p className="font-semibold text-gold-400">
                              {formatCurrency(recommendation.stakeEuros)}
                            </p>
                          </div>
                        </div>

                        <div className="flex items-center justify-between pt-2">
                          <div className="flex items-center gap-2">
                            <Target className="w-4 h-4 text-slate-400" />
                            <span className="text-sm text-slate-400">Confiança:</span>
                            <ConfidenceMeter confidence={recommendation.confidence} size="sm" />
                          </div>
                          <Badge variant="outline" className="border-navy-700 text-slate-400">
                            Kelly: {(recommendation.kellyFraction * 100).toFixed(1)}%
                          </Badge>
                        </div>
                      </CardContent>
                    </Card>
                  )}

                  {/* Alternative Markets */}
                  {analysis.alternativeMarkets && analysis.alternativeMarkets.length > 0 && (
                    <Card className="border-navy-700/50">
                      <CardHeader className="pb-3">
                        <CardTitle className="text-lg font-semibold text-slate-200">
                          Mercados Alternativos
                        </CardTitle>
                      </CardHeader>
                      <CardContent>
                        <div className="space-y-2">
                          {analysis.alternativeMarkets.map((alt) => (
                            <div
                              key={`${alt.market}-${alt.outcome}`}
                              className="flex items-center justify-between p-3 rounded-lg bg-navy-800/30"
                            >
                              <div>
                                <p className="font-medium text-slate-200">
                                  {alt.market} - {alt.outcome}
                                </p>
                                <p className="text-sm text-slate-400">Odd: {alt.odd.toFixed(2)}</p>
                              </div>
                              <div className="text-right">
                                <p className={`text-sm font-medium ${alt.value > 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                                  Value: {(alt.value * 100).toFixed(1)}%
                                </p>
                                <ConfidenceMeter confidence={alt.confidence} size="sm" />
                              </div>
                            </div>
                          ))}
                        </div>
                      </CardContent>
                    </Card>
                  )}

                  {/* Context Flags */}
                  {analysis.contextFlags && Object.keys(analysis.contextFlags).length > 0 && (
                    <Card className="border-navy-700/50">
                      <CardHeader className="pb-3">
                        <CardTitle className="text-lg font-semibold text-slate-200 flex items-center gap-2">
                          <AlertCircle className="w-5 h-5 text-amber-400" />
                          Contexto Adicional
                        </CardTitle>
                      </CardHeader>
                      <CardContent>
                        <div className="flex flex-wrap gap-2">
                          {Object.entries(analysis.contextFlags).map(([key, value]) => (
                            <Badge
                              key={key}
                              variant="outline"
                              className="border-navy-700 text-slate-300"
                            >
                              {key}: {String(value)}
                            </Badge>
                          ))}
                        </div>
                      </CardContent>
                    </Card>
                  )}

                  {/* Model Info */}
                  <div className="flex items-center justify-between text-xs text-slate-500 pt-4 border-t border-navy-700/50">
                    <div>
                      <p>Modelo: {analysis.llmModel}</p>
                      <p>Provider: {analysis.llmProvider}</p>
                    </div>
                    <div className="text-right">
                      <p>Gerado em: {new Date(analysis.generatedAt).toLocaleString('pt-PT')}</p>
                    </div>
                  </div>
                </>
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  )
}
