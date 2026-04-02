import { useEffect, useMemo, useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { Bot, Loader2, RefreshCw, Sparkles, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAnalyzeMatch } from '@/hooks/useApi'
import { formatDateTime } from '@/lib/utils'
import type { Analysis, Match } from '@/types'

interface MatchAnalysisModalProps {
  match: Match | null
  isOpen: boolean
  onClose: () => void
}

function formatRecommendedMarket(analysis: Analysis): string {
  const rec = analysis.recommendedMarket
  if (!rec) return 'Sem recomendação (sem value claro)'

  const parts = [
    `${rec.market} • ${rec.outcome}`,
    `@${rec.odd?.toFixed?.(2) ?? rec.odd}`,
    `value ${(rec.value * 100).toFixed(1)}%`,
    `conf ${rec.confidence}/10`,
  ]
  return parts.join(' • ')
}

export function MatchAnalysisModal({ match, isOpen, onClose }: MatchAnalysisModalProps) {
  const analyze = useAnalyzeMatch()
  const [analysis, setAnalysis] = useState<Analysis | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  const matchId = match?.id ?? ''

  const headerAccent = useMemo(() => {
    if (!match) return 'from-blue-500 to-blue-600'
    return match.sport === 'tennis' ? 'from-violet-500 to-violet-600' : 'from-blue-500 to-blue-600'
  }, [match])

  useEffect(() => {
    if (!isOpen || !matchId) return

    let cancelled = false
    setErrorMsg(null)
    setAnalysis(null)

    analyze
      .mutateAsync({ matchId })
      .then((res) => {
        if (cancelled) return
        setAnalysis(res)
      })
      .catch((e) => {
        if (cancelled) return
        setErrorMsg(e instanceof Error ? e.message : 'Falha ao analisar o jogo')
      })

    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, matchId])

  if (!match) return null

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
            className="fixed inset-0 bg-black/70 backdrop-blur-sm z-50"
          />

          <motion.div
            initial={{ opacity: 0, scale: 0.96, y: 18 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.96, y: 18 }}
            className="fixed inset-4 md:inset-10 lg:inset-20 bg-navy-900 rounded-2xl border border-navy-700/50 shadow-2xl z-50 overflow-hidden flex flex-col"
          >
            <div className="flex items-center justify-between p-6 border-b border-navy-700/50 bg-navy-900/50">
              <div className="flex items-center gap-4">
                <div className={`p-2 rounded-lg bg-gradient-to-br ${headerAccent}`}>
                  <Sparkles className="w-6 h-6 text-white" />
                </div>
                <div>
                  <h2 className="text-xl font-bold text-slate-100">
                    Análise • {match.homeTeam} vs {match.awayTeam}
                  </h2>
                  <p className="text-sm text-slate-400">
                    {match.competition || 'Competição'} • {formatDateTime(match.commenceTime)}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                {analysis && !analyze.isPending && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="gap-2 border-navy-600 text-slate-200 hover:bg-navy-800/80"
                    disabled={analyze.isPending}
                    onClick={async () => {
                      setErrorMsg(null)
                      setAnalysis(null)
                      try {
                        const res = await analyze.mutateAsync({ matchId, forceRefresh: true })
                        setAnalysis(res)
                      } catch (e) {
                        setErrorMsg(e instanceof Error ? e.message : 'Falha ao analisar o jogo')
                      }
                    }}
                  >
                    <RefreshCw className="w-4 h-4" />
                    Nova análise
                  </Button>
                )}
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={onClose}
                  className="rounded-full hover:bg-navy-700/50"
                >
                  <X className="w-5 h-5" />
                </Button>
              </div>
            </div>

            <div className="flex-1 overflow-y-auto p-6">
              {analyze.isPending && (
                <div className="flex items-center justify-center h-full py-16">
                  <div className="flex flex-col items-center gap-3 text-center">
                    <div className="inline-flex items-center gap-2 text-slate-200">
                      <Loader2 className="w-5 h-5 animate-spin" />
                      <span className="font-medium">A pedir análise à LLM...</span>
                    </div>
                    <p className="text-sm text-slate-500 max-w-md">
                      A calcular probabilidades, value e resumo (se activo) com base nas odds e na BD.
                    </p>
                  </div>
                </div>
              )}

              {!analyze.isPending && errorMsg && (
                <div className="flex items-center justify-center h-full py-16">
                  <div className="text-center max-w-lg">
                    <p className="text-red-400 font-medium mb-2">Falha na análise</p>
                    <p className="text-sm text-slate-400 mb-4">{errorMsg}</p>
                    <Button
                      onClick={() => analyze.mutate({ matchId })}
                      variant="outline"
                      size="sm"
                    >
                      Tentar novamente
                    </Button>
                  </div>
                </div>
              )}

              {!analyze.isPending && !errorMsg && analysis && (
                <div className="space-y-5">
                  {analysis.modelProbabilities?.dataSource && (
                    <div
                      className={`rounded-lg border px-3 py-2 text-sm ${
                        analysis.modelProbabilities.dataSource === 'placeholder_1x2'
                          ? 'border-amber-500/40 bg-amber-500/10 text-amber-100'
                          : 'border-navy-600/60 bg-navy-950/50 text-slate-300'
                      }`}
                    >
                      <span className="text-slate-500">Fonte das probabilidades 1X2: </span>
                      <span className="font-mono">{analysis.modelProbabilities.dataSource}</span>
                      {analysis.modelProbabilities.dataSource === 'placeholder_1x2' && (
                        <span className="block mt-1 text-amber-200/90">
                          Odds 1X2 incompletas na BD — o motor não deve sugerir apostas com base nestes
                          números.
                        </span>
                      )}
                    </div>
                  )}

                  <div className="rounded-xl border border-navy-700/50 bg-navy-950/40 p-4">
                    <div className="flex items-center gap-2 text-slate-200 mb-2">
                      <Bot className="w-4 h-4 text-gold-400" />
                      <p className="font-semibold">Análise</p>
                    </div>
                    <p className="text-slate-300 leading-relaxed">{analysis.reasoning}</p>
                  </div>

                  <div className="rounded-xl border border-navy-700/50 bg-navy-950/40 p-4">
                    <p className="text-sm text-slate-400 mb-1">Recomendação</p>
                    <p className="text-slate-100 font-semibold">{formatRecommendedMarket(analysis)}</p>
                  </div>

                  <div className="rounded-xl border border-navy-700/50 bg-navy-950/20 p-4">
                    <p className="text-xs text-slate-500">
                      Provider: <span className="text-slate-400">{analysis.llmProvider}</span> • Modelo:{' '}
                      <span className="text-slate-400">{analysis.llmModel}</span>
                    </p>
                  </div>
                </div>
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  )
}

