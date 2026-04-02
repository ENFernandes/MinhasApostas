import { motion, AnimatePresence } from 'framer-motion'
import { X, BarChart3 } from 'lucide-react'
import { useMatchStats } from '@/hooks/useStatsApi'
import { useTennisPlayerStats } from '@/hooks/useTennisStatsApi'
import { TeamForm } from './TeamForm'
import { HeadToHeadStats } from './HeadToHeadStats'
import { TennisPlayerStatsCard } from './TennisPlayerStatsCard'
import { Button } from '@/components/ui/button'
import { formatDateTime } from '@/lib/utils'
import type { Match } from '@/types'

interface MatchStatsModalProps {
  match: Match | null
  isOpen: boolean
  onClose: () => void
}

export function MatchStatsModal({ match, isOpen, onClose }: MatchStatsModalProps) {
  const { homeForm, awayForm, headToHead, isLoading, isError } = useMatchStats(
    match?.homeTeam || '',
    match?.awayTeam || '',
    match?.sport || 'football'
  )
  const homeTennis = useTennisPlayerStats(match?.sport === 'tennis' ? match?.homeTeam || '' : '')
  const awayTennis = useTennisPlayerStats(match?.sport === 'tennis' ? match?.awayTeam || '' : '')

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
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            className="fixed inset-4 md:inset-10 lg:inset-20 bg-navy-900 rounded-2xl border border-navy-700/50 shadow-2xl z-50 overflow-hidden flex flex-col"
          >
            {/* Header */}
            <div className="flex items-center justify-between p-6 border-b border-navy-700/50 bg-navy-900/50">
              <div className="flex items-center gap-4">
                <div className="p-2 rounded-lg bg-gold-500/10">
                  <BarChart3 className="w-6 h-6 text-gold-400" />
                </div>
                <div>
                  <h2 className="text-xl font-bold text-slate-100">
                    {match.homeTeam} vs {match.awayTeam}
                  </h2>
                  <p className="text-sm text-slate-400">
                    {match.competition || 'Competição'} • {' '}
                    {formatDateTime(match.commenceTime)}
                  </p>
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
            <div className="flex-1 overflow-y-auto p-6">
              {match.sport === 'tennis' ? (
                homeTennis.isLoading || awayTennis.isLoading ? (
                  <div className="flex items-center justify-center h-full">
                    <div className="flex flex-col items-center gap-3">
                      <div className="w-8 h-8 border-2 border-gold-400 border-t-transparent rounded-full animate-spin" />
                      <p className="text-sm text-slate-400">A carregar estatísticas de ténis...</p>
                    </div>
                  </div>
                ) : homeTennis.isError || awayTennis.isError ? (
                  <div className="flex items-center justify-center h-full">
                    <div className="text-center">
                      <p className="text-red-400 mb-2">Erro ao carregar estatísticas</p>
                      <Button onClick={() => window.location.reload()} variant="outline" size="sm">
                        Tentar novamente
                      </Button>
                    </div>
                  </div>
                ) : (() => {
                  const home = homeTennis.data
                  const away = awayTennis.data
                  const noData =
                    (!home || (!home.recentResults?.length && !Object.keys(home.eloBySurface ?? {}).length)) &&
                    (!away || (!away.recentResults?.length && !Object.keys(away.eloBySurface ?? {}).length))

                  if (noData) {
                    return (
                      <div className="flex items-center justify-center h-full py-16">
                        <div className="text-center">
                          <BarChart3 className="w-12 h-12 mx-auto text-navy-600 mb-3" />
                          <p className="text-slate-400 font-medium">Dados estatísticos não disponíveis</p>
                          <p className="text-sm text-slate-500 mt-1">
                            Ainda não temos histórico local suficiente para este jogador.
                          </p>
                        </div>
                      </div>
                    )
                  }

                  return (
                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                      {home && <TennisPlayerStatsCard sideLabel="Casa" stats={home} />}
                      {away && <TennisPlayerStatsCard sideLabel="Fora" stats={away} />}
                    </div>
                  )
                })()
              ) : isLoading ? (
                <div className="flex items-center justify-center h-full">
                  <div className="flex flex-col items-center gap-3">
                    <div className="w-8 h-8 border-2 border-gold-400 border-t-transparent rounded-full animate-spin" />
                    <p className="text-sm text-slate-400">A carregar estatísticas...</p>
                  </div>
                </div>
              ) : isError ? (
                <div className="flex items-center justify-center h-full">
                  <div className="text-center">
                    <p className="text-red-400 mb-2">Erro ao carregar estatísticas</p>
                    <Button onClick={() => window.location.reload()} variant="outline" size="sm">
                      Tentar novamente
                    </Button>
                  </div>
                </div>
              ) : (() => {
                const noData =
                  (!homeForm.data || homeForm.data.last10Games.length === 0) &&
                  (!awayForm.data || awayForm.data.last10Games.length === 0) &&
                  (!headToHead.data || headToHead.data.totalMatches === 0)
                if (noData) {
                  return (
                    <div className="flex items-center justify-center h-full py-16">
                      <div className="text-center">
                        <BarChart3 className="w-12 h-12 mx-auto text-navy-600 mb-3" />
                        <p className="text-slate-400 font-medium">Dados estatísticos não disponíveis</p>
                        <p className="text-sm text-slate-500 mt-1">
                          A API gratuita não fornece dados para esta competição.
                        </p>
                      </div>
                    </div>
                  )
                }
                return (
                  <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                    <div className="lg:col-span-2">
                      {headToHead.data && headToHead.data.totalMatches > 0 && (
                        <HeadToHeadStats
                          stats={headToHead.data}
                          homeTeamName={match.homeTeam}
                          awayTeamName={match.awayTeam}
                        />
                      )}
                    </div>
                    <div>
                      {homeForm.data && homeForm.data.last10Games.length > 0 && (
                        <TeamForm teamForm={homeForm.data} isHome={true} />
                      )}
                    </div>
                    <div>
                      {awayForm.data && awayForm.data.last10Games.length > 0 && (
                        <TeamForm teamForm={awayForm.data} isHome={false} />
                      )}
                    </div>
                  </div>
                )
              })()}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  )
}
