import { useState, useMemo } from 'react'
import {
  LineChart,
  Filter,
  Star,
  Target,
  TrendingUp
} from 'lucide-react'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useMatches, useRecommendations, useMatchOdds } from '@/hooks/useApi'
import { OddsTable } from '@/components/OddsTable'
import { ValueBadge } from '@/components/ValueBadge'
import { useAppStore } from '@/stores/appStore'
import { formatCurrency, formatTime, formatDateShort } from '@/lib/utils'

export default function OddsAnalyzerPage() {
  const { settings } = useAppStore()
  // Only show matches that already have odds in the DB.
  const { data: matches, isLoading: matchesLoading } = useMatches(undefined, undefined, undefined, true)
  const { data: recommendations, isLoading: recsLoading } = useRecommendations()
  const [selectedMatch, setSelectedMatch] = useState<string | null>(null)
  const [sportFilter, setSportFilter] = useState<'all' | 'football' | 'tennis'>('all')
  const [competitionFilter, setCompetitionFilter] = useState<string>('')
  const [searchFilter, setSearchFilter] = useState<string>('')

  const isLoading = matchesLoading || recsLoading

  const todayMatches = useMemo(() => {
    if (!matches) return []
    return matches
  }, [matches])

  const availableCompetitions = useMemo(() => {
    const comps = new Set(todayMatches.map(m => m.competition).filter(Boolean))
    return Array.from(comps).sort()
  }, [todayMatches])

  const filteredMatches = useMemo(() => {
    return todayMatches.filter((match) => {
      if (sportFilter !== 'all' && match.sport !== sportFilter) return false
      if (competitionFilter && match.competition !== competitionFilter) return false
      if (searchFilter) {
        const q = searchFilter.toLowerCase()
        if (!match.homeTeam.toLowerCase().includes(q) && !match.awayTeam.toLowerCase().includes(q)) return false
      }
      return true
    })
  }, [todayMatches, sportFilter, competitionFilter, searchFilter])

  const selectedMatchData = useMemo(() => {
    if (!selectedMatch || !matches) return null
    return matches.find(m => m.id === selectedMatch)
  }, [selectedMatch, matches])

  const { data: matchOddsData, isLoading: oddsLoading } = useMatchOdds(selectedMatch || '')

  // Group odds by market
  const selectedMatchOdds = useMemo(() => {
    if (!matchOddsData || !Array.isArray(matchOddsData)) return null
    
    const grouped = matchOddsData.reduce((acc: any, odd: any) => {
      const market = odd.market || 'Outros'
      const oddValue = typeof odd.oddDecimal === 'number'
        ? odd.oddDecimal
        : typeof odd.odd === 'number'
          ? odd.odd
          : null

      // Guard against malformed/empty odds payloads to avoid runtime crashes.
      if (oddValue === null || !Number.isFinite(oddValue)) {
        return acc
      }

      if (!acc[market]) {
        acc[market] = []
      }
      acc[market].push({
        id: odd.id || `${odd.bookmaker}-${odd.market}-${odd.outcome}`,
        bookmaker: odd.bookmaker,
        market: odd.market,
        outcome: odd.outcome,
        odd: oddValue,
        impliedProbability: typeof odd.impliedProbability === 'number'
          ? odd.impliedProbability
          : 1 / oddValue,
      })
      return acc
    }, {})
    
    return grouped
  }, [matchOddsData])

  const selectedMatchRec = useMemo(() => {
    if (!selectedMatch || !recommendations) return null
    return recommendations.find((r: any) => r.match_id === selectedMatch)
  }, [selectedMatch, recommendations])

  return (
    <div className="min-h-screen bg-gradient-to-br from-background via-background to-navy-950">
      {/* Header */}
      <header className="sticky top-0 z-40 border-b border-navy-700/50 bg-background/80 backdrop-blur-xl">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-xl bg-gradient-to-br from-emerald-500 to-emerald-600">
                <LineChart className="w-6 h-6 text-white" />
              </div>
              <div>
                <h1 className="text-2xl font-display font-bold gradient-text">
                  Análise de Odds
                </h1>
                <p className="text-xs text-muted-foreground">
                  Compara odds e identifica value bets
                </p>
              </div>
            </div>

            <div className="hidden md:flex items-center gap-4">
              <div className="text-right">
                <p className="text-xs text-muted-foreground">Banca</p>
                <p className="text-lg font-semibold text-gold-400">
                  {formatCurrency(settings.bankroll)}
                </p>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="container mx-auto px-4 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Match Selector */}
          <div className="lg:col-span-1">
            <Card className="border-navy-700/50 bg-navy-900/50">
              <CardHeader className="pb-3 space-y-3">
                <CardTitle className="text-lg font-semibold text-slate-200 flex items-center gap-2">
                  <Filter className="w-5 h-5 text-gold-400" />
                  Próximos Jogos
                </CardTitle>
                {/* Sport filter */}
                <div className="flex gap-1">
                  {(['all', 'football', 'tennis'] as const).map((s) => (
                    <button
                      key={s}
                      onClick={() => setSportFilter(s)}
                      className={`flex-1 py-1 px-2 rounded text-xs font-medium transition-all ${
                        sportFilter === s
                          ? 'bg-gold-500 text-navy-950'
                          : 'bg-navy-800/50 text-slate-400 hover:bg-navy-700/50'
                      }`}
                    >
                      {s === 'all' ? 'Todos' : s === 'football' ? 'Futebol' : 'Ténis'}
                    </button>
                  ))}
                </div>
                {/* Competition filter */}
                {availableCompetitions.length > 0 && (
                  <select
                    value={competitionFilter}
                    onChange={(e) => setCompetitionFilter(e.target.value)}
                    className="w-full px-2 py-1.5 rounded bg-navy-800/50 border border-navy-700 text-xs text-slate-300 focus:outline-none focus:border-gold-500"
                  >
                    <option value="">Todas as competições</option>
                    {availableCompetitions.map((c) => (
                      <option key={c} value={c}>{c}</option>
                    ))}
                  </select>
                )}
                {/* Search */}
                <input
                  type="text"
                  value={searchFilter}
                  onChange={(e) => setSearchFilter(e.target.value)}
                  placeholder="Pesquisar equipa..."
                  className="w-full px-2 py-1.5 rounded bg-navy-800/50 border border-navy-700 text-xs text-slate-300 placeholder:text-slate-500 focus:outline-none focus:border-gold-500"
                />
              </CardHeader>
              <CardContent className="space-y-2">
                {isLoading ? (
                  <div className="space-y-2">
                    {[1, 2, 3].map(i => (
                      <div key={i} className="h-14 rounded-lg bg-navy-800/50 animate-pulse" />
                    ))}
                  </div>
                ) : (
                  filteredMatches.map((match) => (
                    <Button
                      key={match.id}
                      variant={selectedMatch === match.id ? "default" : "ghost"}
                      className={`w-full justify-start text-left h-auto py-3 ${
                        selectedMatch === match.id 
                          ? 'bg-gold-500 text-navy-950 hover:bg-gold-600' 
                          : 'hover:bg-navy-800/50'
                      }`}
                      onClick={() => setSelectedMatch(match.id)}
                    >
                      <div className="flex flex-col">
                        <span className="font-medium">
                          {match.homeTeam} vs {match.awayTeam}
                        </span>
                        <span className="text-xs opacity-70">
                          {formatDateShort(match.commenceTime)} {formatTime(match.commenceTime)}
                        </span>
                      </div>
                    </Button>
                  ))
                )}
              </CardContent>
            </Card>
          </div>

          {/* Odds Analysis */}
          <div className="lg:col-span-2 space-y-6">
            {selectedMatchData ? (
              <>
                {/* Recommended Bet Section */}
                {selectedMatchRec && (
                  <Card className="border-emerald-500/30 bg-emerald-500/5">
                    <CardHeader className="pb-3">
                      <CardTitle className="text-lg font-semibold text-emerald-400 flex items-center gap-2">
                        <Star className="w-5 h-5" />
                        Recomendação do Modelo
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      {(() => {
                        const recOdd: number =
                          typeof selectedMatchRec.odd_decimal === 'number'
                            ? selectedMatchRec.odd_decimal
                            : typeof selectedMatchRec.odd === 'number'
                              ? selectedMatchRec.odd
                              : 0
                        const recStake: number =
                          typeof selectedMatchRec.stake_euros === 'number'
                            ? selectedMatchRec.stake_euros
                            : typeof selectedMatchRec.stakeEuros === 'number'
                              ? selectedMatchRec.stakeEuros
                              : 0

                        return (
                          <>
                            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                              <div className="p-3 rounded-lg bg-navy-800/50">
                                <p className="text-xs text-slate-400">Mercado</p>
                                <p className="font-semibold text-slate-200">{selectedMatchRec.market}</p>
                              </div>
                              <div className="p-3 rounded-lg bg-navy-800/50">
                                <p className="text-xs text-slate-400">Outcome</p>
                                <p className="font-semibold text-slate-200">{selectedMatchRec.outcome}</p>
                              </div>
                              <div className="p-3 rounded-lg bg-navy-800/50">
                                <p className="text-xs text-slate-400">Odd</p>
                                <p className="font-semibold text-gold-400">{recOdd.toFixed(2)}</p>
                              </div>
                              <div className="p-3 rounded-lg bg-navy-800/50">
                                <p className="text-xs text-slate-400">Stake Recomendada</p>
                                <p className="font-semibold text-emerald-400">
                                  {formatCurrency(recStake)}
                                </p>
                              </div>
                            </div>
                            <div className="mt-4 flex items-center justify-between">
                              <div className="flex items-center gap-4">
                                <ValueBadge
                                  isValid={selectedMatchRec.value > 0}
                                  value={selectedMatchRec.value}
                                  confidence={selectedMatchRec.confidence}
                                  kellyFraction={selectedMatchRec.kelly_fraction}
                                  size="md"
                                />
                                <Badge variant="outline" className="border-navy-700 text-slate-400">
                                  <Target className="w-3 h-3 mr-1" />
                                  Confiança: {selectedMatchRec.confidence}/10
                                </Badge>
                              </div>
                              <div className="text-right">
                                <p className="text-xs text-slate-400">Value Esperado</p>
                                <p className="text-sm font-medium text-emerald-400">
                                  +{(selectedMatchRec.value * 100).toFixed(1)}%
                                </p>
                              </div>
                            </div>
                          </>
                        )
                      })()}
                    </CardContent>
                  </Card>
                )}

                {/* All Markets */}
                <Tabs defaultValue="main" className="w-full">
                  <TabsList className="bg-navy-800/50 border border-navy-700">
                    <TabsTrigger value="main" className="data-[state=active]:bg-gold-500 data-[state=active]:text-navy-950">
                      <TrendingUp className="w-4 h-4 mr-2" />
                      Principais Mercados
                    </TabsTrigger>
                    <TabsTrigger value="all" className="data-[state=active]:bg-gold-500 data-[state=active]:text-navy-950">
                      <LineChart className="w-4 h-4 mr-2" />
                      Todos os Mercados
                    </TabsTrigger>
                  </TabsList>

                  <TabsContent value="main" className="mt-4 space-y-4">
                    {oddsLoading ? (
                      <div className="flex items-center justify-center py-12">
                        <div className="flex flex-col items-center gap-3">
                          <div className="w-8 h-8 border-2 border-gold-400 border-t-transparent rounded-full animate-spin" />
                          <p className="text-sm text-slate-400">A carregar odds...</p>
                        </div>
                      </div>
                    ) : selectedMatchOdds ? (
                      <>
                        {(selectedMatchOdds['1X2'] || selectedMatchOdds['h2h']) && (
                          <OddsTable
                            market="1X2 - Resultado Final"
                            odds={selectedMatchOdds['1X2'] || selectedMatchOdds['h2h']}
                            modelProbability={selectedMatchRec?.model_probabilities?.home || 0.45}
                            confidence={selectedMatchRec?.confidence || 6}
                            showAnalysis={true}
                          />
                        )}
                        {selectedMatchOdds['totals'] && (
                          <OddsTable
                            market="Over/Under"
                            odds={selectedMatchOdds['totals']}
                            modelProbability={selectedMatchRec?.model_probabilities?.over_2_5 || 0.52}
                            confidence={selectedMatchRec?.confidence || 6}
                            showAnalysis={true}
                          />
                        )}
                      </>
                    ) : (
                      <p className="text-center text-slate-400 py-8">Sem dados de odds disponíveis</p>
                    )}
                  </TabsContent>

                  <TabsContent value="all" className="mt-4 space-y-4">
                    {selectedMatchOdds && Object.entries(selectedMatchOdds).map(([market, odds]) => (
                      <OddsTable
                        key={market}
                        market={market}
                        odds={odds as any}
                        modelProbability={selectedMatchRec?.model_probabilities?.home || selectedMatchRec?.model_probabilities?.home_win}
                        confidence={selectedMatchRec?.confidence || 6}
                        showAnalysis={true}
                      />
                    ))}
                  </TabsContent>
                </Tabs>
              </>
            ) : (
              <Card className="border-navy-700/50 bg-navy-900/50">
                <CardContent className="py-16 text-center">
                  <LineChart className="w-16 h-16 mx-auto text-navy-600 mb-4" />
                  <h3 className="text-lg font-medium text-muted-foreground">
                    Seleciona um jogo
                  </h3>
                  <p className="text-sm text-muted-foreground mt-1">
                    Escolhe um jogo da lista para ver as odds e análise
                  </p>
                </CardContent>
              </Card>
            )}
          </div>
        </div>
      </main>
    </div>
  )
}
