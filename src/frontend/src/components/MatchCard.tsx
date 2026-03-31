import { useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { Trophy, TrendingUp, Clock, ChevronRight } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { OddBadge } from './OddBadge'
import { ConfidenceMeter } from './ConfidenceMeter'
import { useAppStore } from '@/stores/appStore'
import type { Match, Analysis } from '@/types'
import { formatDate, formatCurrency } from '@/lib/utils'

interface MatchCardProps {
  match: Match
  analysis?: Analysis
  onViewDetails?: () => void
}

export function MatchCard({ match, analysis, onViewDetails }: MatchCardProps) {
  const openStakeModal = useAppStore((state) => state.openStakeModal)
  const [isHovered, setIsHovered] = useState(false)

  const hasRecommendation = analysis?.recommendedMarket !== null
  const recommendation = analysis?.recommendedMarket

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4 }}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      <Card className="relative overflow-hidden bg-gradient-to-br from-card to-navy-900/50 border-navy-700/50 hover:border-gold-500/30 transition-all duration-300">
        {/* Background glow effect */}
        <AnimatePresence>
          {hasRecommendation && (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: isHovered ? 0.1 : 0 }}
              exit={{ opacity: 0 }}
              // This overlay is purely visual; it must not block clicks on the action buttons.
              className="absolute inset-0 z-0 pointer-events-none bg-gradient-to-r from-gold-500/20 via-transparent to-gold-500/20"
            />
          )}
        </AnimatePresence>

        <CardContent className="relative z-10 p-5">
          {/* Header */}
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-2">
              <span className={`
                px-2 py-0.5 rounded text-xs font-medium uppercase tracking-wider
                ${match.sport === 'football' 
                  ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' 
                  : 'bg-purple-500/10 text-purple-400 border border-purple-500/20'}
              `}>
                {match.sport}
              </span>
              {match.competition && (
                <span className="text-xs text-muted-foreground">
                  {match.competition}
                </span>
              )}
            </div>
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Clock className="w-3.5 h-3.5" />
              {formatDate(match.commenceTime)}
            </div>
          </div>

          {/* Teams */}
          <div className="space-y-3 mb-4">
            <div className="flex items-center justify-between">
              <span className="text-lg font-medium">{match.homeTeam}</span>
              {match.homeScore !== undefined && (
                <span className="text-2xl font-bold text-gold-400">{match.homeScore}</span>
              )}
            </div>
            <div className="flex items-center justify-between">
              <span className="text-lg font-medium">{match.awayTeam}</span>
              {match.awayScore !== undefined && (
                <span className="text-2xl font-bold text-gold-400">{match.awayScore}</span>
              )}
            </div>
          </div>

          {/* Recommendation */}
          {hasRecommendation && recommendation && (
            <div className="mb-4 p-3 rounded-lg bg-gradient-to-r from-gold-500/5 to-transparent border border-gold-500/20">
              <div className="flex items-center gap-2 mb-2">
                <Trophy className="w-4 h-4 text-gold-400" />
                <span className="text-sm font-medium text-gold-400">
                  Recomendação
                </span>
              </div>
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-muted-foreground">
                    {recommendation.outcome} @ {recommendation.bookmaker}
                  </p>
                  <ConfidenceMeter 
                    confidence={recommendation.confidence} 
                    size="sm" 
                    showLabel={false}
                    className="mt-1"
                  />
                </div>
                <div className="text-right">
                  <OddBadge 
                    odd={recommendation.odd} 
                    value={recommendation.value}
                    size="md"
                  />
                  <p className="text-xs text-muted-foreground mt-1">
                    Stake: {formatCurrency(recommendation.stakeEuros)}
                  </p>
                </div>
              </div>
            </div>
          )}

          {/* Actions */}
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              className="flex-1 border-navy-600 hover:bg-navy-800"
              onClick={onViewDetails}
            >
              <TrendingUp className="w-4 h-4 mr-2" />
              Análise
            </Button>
            {hasRecommendation && (
              <Button
                size="sm"
                className="flex-1 bg-gradient-to-r from-gold-500 to-gold-400 text-navy-950 font-semibold hover:from-gold-400 hover:to-gold-300"
                onClick={() => openStakeModal(match.id, match, analysis)}
              >
                Apostar
                <ChevronRight className="w-4 h-4 ml-1" />
              </Button>
            )}
          </div>
        </CardContent>
      </Card>
    </motion.div>
  )
}
