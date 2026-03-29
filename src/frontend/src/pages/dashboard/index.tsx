import { useMemo } from 'react'
import { motion } from 'framer-motion'
import { 
  Trophy, 
  TrendingUp, 
  Sparkles
} from 'lucide-react'
import { MatchCard } from '@/components/MatchCard'
import { StakeModal } from '@/components/StakeModal'
import { AnalysisDetailModal } from '@/components/AnalysisDetailModal'
import { AdvancedFilters } from '@/components/AdvancedFilters'
import { useMatches, useRecommendations, useLatestAnalysis, useAnalyzeMatch } from '@/hooks/useApi'
import { useAppStore } from '@/stores/appStore'
import { formatCurrency } from '@/lib/utils'

export default function DashboardPage() {
  const {
    selectedSport,
    settings,
    isAnalysisModalOpen,
    selectedAnalysisMatchId,
    openAnalysisModal,
    closeAnalysisModal,
    minOddFilter,
    maxOddFilter,
    minValueFilter,
    minConfidenceFilter,
  } = useAppStore()
  const { data: matches, isLoading: matchesLoading } = useMatches()
  const { data: recommendations, isLoading: recsLoading } = useRecommendations()
  const { data: selectedAnalysis, isLoading: isAnalysisLoading, isError: isAnalysisError } = useLatestAnalysis(selectedAnalysisMatchId || '')
  const { mutate: runAnalysis, isPending: isRunningAnalysis, isError: isAnalysisRunError } = useAnalyzeMatch()

  const isLoading = matchesLoading || recsLoading

  // Create a map of recommendations by match_id for easy lookup
  const recommendationsMap = useMemo(() => {
    if (!recommendations) return {}
    return recommendations.reduce((acc: Record<string, any>, rec: any) => {
      acc[rec.match_id] = rec
      return acc
    }, {} as Record<string, any>)
  }, [recommendations])

  // Apply advanced filters
  const filteredMatches = useMemo(() => {
    if (!matches) return []
    
    return matches.filter((match) => {
      // Filter by sport
      if (selectedSport !== 'all' && match.sport !== selectedSport) {
        return false
      }

      // Get recommendation for this match
      const rec = recommendationsMap[match.id]
      if (!rec) return true // Keep matches without recommendations

      // Filter by min odd
      if (minOddFilter !== null && rec.odd_decimal < minOddFilter) {
        return false
      }

      // Filter by max odd
      if (maxOddFilter !== null && rec.odd_decimal > maxOddFilter) {
        return false
      }

      // Filter by min value
      if (minValueFilter !== null && rec.value < minValueFilter) {
        return false
      }

      // Filter by min confidence
      if (minConfidenceFilter !== null && rec.confidence < minConfidenceFilter) {
        return false
      }

      return true
    })
  }, [matches, selectedSport, recommendationsMap, minOddFilter, maxOddFilter, minValueFilter, minConfidenceFilter])

  const stats = {
    todayBets: recommendations?.length || 0,
    totalProfit: 0, // Would calculate from bet history
    winRate: 0,
    roi: 0,
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-background via-background to-navy-950">
      {/* Header */}
      <header className="sticky top-0 z-40 border-b border-navy-700/50 bg-background/80 backdrop-blur-xl">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-xl bg-gradient-to-br from-gold-500 to-gold-600">
                <Trophy className="w-6 h-6 text-navy-950" />
              </div>
              <div>
                <h1 className="text-2xl font-display font-bold gradient-text">
                  Minhas Apostas
                </h1>
                <p className="text-xs text-muted-foreground">AI-Powered Betting</p>
              </div>
            </div>

            {/* Stats Overview */}
            <div className="hidden md:flex items-center gap-6">
              <div className="text-right">
                <p className="text-xs text-muted-foreground">Banca</p>
                <p className="text-lg font-semibold text-gold-400">
                  {formatCurrency(settings.bankroll)}
                </p>
              </div>
              <div className="text-right">
                <p className="text-xs text-muted-foreground">Recomendações</p>
                <p className="text-lg font-semibold text-navy-200">
                  {stats.todayBets}
                </p>
              </div>
              <div className="text-right">
                <p className="text-xs text-muted-foreground">P&L Hoje</p>
                <p className={`text-lg font-semibold ${stats.totalProfit >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                  {stats.totalProfit >= 0 ? '+' : ''}{formatCurrency(stats.totalProfit)}
                </p>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="container mx-auto px-4 py-8">
        {/* Advanced Filters */}
        <div className="mb-6">
          <AdvancedFilters />
        </div>

        {/* Section Title */}
        <div className="flex items-center gap-2 mb-6">
          <Sparkles className="w-5 h-5 text-gold-400" />
          <h2 className="text-xl font-display font-semibold">Oportunidades do Dia</h2>
          <span className="px-2 py-0.5 rounded-full bg-gold-500/10 text-gold-400 text-xs font-medium border border-gold-500/20">
            {filteredMatches.length} jogos
          </span>
        </div>

        {/* Matches Grid */}
        {isLoading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-64 rounded-lg bg-navy-800/50 animate-pulse" />
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {filteredMatches.map((match, index) => (
              <motion.div
                key={match.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.1 }}
              >
                <MatchCard
                  match={match}
                  analysis={recommendationsMap[match.id]}
                  onViewDetails={() => {
                    openAnalysisModal(match.id)
                    runAnalysis({ matchId: match.id, data: {} })
                  }}
                />
              </motion.div>
            ))}
          </div>
        )}

        {/* Empty State */}
        {!isLoading && filteredMatches.length === 0 && (
          <div className="text-center py-16">
            <TrendingUp className="w-16 h-16 mx-auto text-navy-600 mb-4" />
            <h3 className="text-lg font-medium text-muted-foreground">
              Nenhuma oportunidade encontrada
            </h3>
            <p className="text-sm text-muted-foreground mt-1">
              Tente ajustar os filtros ou volte mais tarde
            </p>
          </div>
        )}
      </main>

      {/* Modals */}
      <StakeModal />
      <AnalysisDetailModal
        analysis={selectedAnalysis}
        isOpen={isAnalysisModalOpen}
        onClose={closeAnalysisModal}
        isLoading={isRunningAnalysis || (isAnalysisLoading && !isAnalysisError)}
        isError={isAnalysisRunError || isAnalysisError}
      />
    </div>
  )
}
