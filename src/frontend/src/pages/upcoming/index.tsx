import { useState, useMemo } from 'react'
import { motion } from 'framer-motion'
import { 
  Calendar, 
  BarChart3, 
  ChevronRight,
  Clock,
  Trophy,
  Search
} from 'lucide-react'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useMatches } from '@/hooks/useApi'
import { MatchStatsModal } from '@/components/MatchStatsModal'
import { useAppStore } from '@/stores/appStore'
import { formatTime, formatDateShort } from '@/lib/utils'
import type { Match } from '@/types'

export default function UpcomingMatchesPage() {
  const { selectedSport, setSelectedSport } = useAppStore()
  const [fromDate, setFromDate] = useState<string>(() => {
    const today = new Date()
    return today.toISOString().split('T')[0]
  })
  const [toDate, setToDate] = useState<string>(() => {
    const nextWeek = new Date()
    nextWeek.setDate(nextWeek.getDate() + 7)
    return nextWeek.toISOString().split('T')[0]
  })
  const [selectedMatch, setSelectedMatch] = useState<Match | null>(null)
  const [isModalOpen, setIsModalOpen] = useState(false)

  const fromDateTime = useMemo(() => {
    if (!fromDate) return undefined
    const date = new Date(fromDate)
    date.setHours(0, 0, 0, 0)
    return date
  }, [fromDate])

  const toDateTime = useMemo(() => {
    if (!toDate) return undefined
    const date = new Date(toDate)
    date.setHours(23, 59, 59, 999)
    return date
  }, [toDate])

  const { data: matches, isLoading } = useMatches(fromDateTime, toDateTime)

  const filteredMatches = useMemo(() => {
    if (!matches) return []
    if (selectedSport === 'all') return matches
    return matches.filter((m) => m.sport === selectedSport)
  }, [matches, selectedSport])

  const handleMatchClick = (match: Match) => {
    setSelectedMatch(match)
    setIsModalOpen(true)
  }

  const formatMatchTime = (dateString: string) => {
    return formatTime(dateString)
  }

  const formatDateRange = () => {
    if (!fromDate || !toDate) return ''
    const from = formatDateShort(fromDate)
    const to = formatDateShort(toDate)
    return `${from} - ${to}`
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-background via-background to-navy-950">
      {/* Header */}
      <header className="sticky top-0 z-40 border-b border-navy-700/50 bg-background/80 backdrop-blur-xl">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-xl bg-gradient-to-br from-purple-500 to-purple-600">
                <Calendar className="w-6 h-6 text-white" />
              </div>
              <div>
                <h1 className="text-2xl font-display font-bold gradient-text">
                  Jogos Futuros
                </h1>
                <p className="text-xs text-muted-foreground">
                  {formatDateRange() || 'Selecione um período'}
                </p>
              </div>
            </div>

            <div className="hidden md:flex items-center gap-4">
              <div className="text-right">
                <p className="text-xs text-muted-foreground">Total Jogos</p>
                <p className="text-lg font-semibold text-purple-400">
                  {filteredMatches.length}
                </p>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="container mx-auto px-4 py-8">
        {/* Date Range Picker */}
        <Card className="mb-6 border-navy-700/50 bg-navy-900/50">
          <CardContent className="p-4">
            <div className="flex flex-col sm:flex-row items-start sm:items-end gap-4">
              <div className="flex-1">
                <label className="text-slate-400 mb-2 block">Desde</label>
                <input
                  type="date"
                  value={fromDate}
                  onChange={(e) => setFromDate(e.target.value)}
                  className="w-full px-3 py-2 bg-navy-800 border border-navy-700 rounded-md text-slate-200 focus:outline-none focus:border-purple-500"
                />
              </div>
              <div className="flex-1">
                <label className="text-slate-400 mb-2 block">Até</label>
                <input
                  type="date"
                  value={toDate}
                  onChange={(e) => setToDate(e.target.value)}
                  className="w-full px-3 py-2 bg-navy-800 border border-navy-700 rounded-md text-slate-200 focus:outline-none focus:border-purple-500"
                />
              </div>
              <Button className="bg-purple-500 hover:bg-purple-400 text-white">
                <Search className="w-4 h-4 mr-2" />
                Buscar
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Filters */}
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-8">
          <Tabs value={selectedSport} onValueChange={(v) => setSelectedSport(v as any)}>
            <TabsList className="bg-navy-800/50 border border-navy-700">
              <TabsTrigger value="all" className="data-[state=active]:bg-purple-500 data-[state=active]:text-white">
                Todos
              </TabsTrigger>
              <TabsTrigger value="football" className="data-[state=active]:bg-purple-500 data-[state=active]:text-white">
                Futebol
              </TabsTrigger>
              <TabsTrigger value="tennis" className="data-[state=active]:bg-purple-500 data-[state=active]:text-white">
                Ténis
              </TabsTrigger>
            </TabsList>
          </Tabs>
        </div>

        {/* Matches List */}
        {isLoading ? (
          <div className="space-y-4">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="h-24 rounded-lg bg-navy-800/50 animate-pulse" />
            ))}
          </div>
        ) : (
          <div className="space-y-4">
            {filteredMatches.map((match, index) => (
              <motion.div
                key={match.id}
                initial={{ opacity: 0, x: -20 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: index * 0.05 }}
              >
                <Card 
                  className="border-navy-700/50 bg-navy-900/50 hover:bg-navy-800/50 transition-all cursor-pointer group"
                  onClick={() => handleMatchClick(match)}
                >
                  <CardContent className="p-4">
                    <div className="flex items-center justify-between">
                      {/* Time */}
                      <div className="flex items-center gap-3 w-24">
                        <div className="flex items-center gap-1.5 text-purple-400">
                          <Clock className="w-4 h-4" />
                          <span className="font-mono font-medium">
                            {formatMatchTime(match.commenceTime)}
                          </span>
                        </div>
                      </div>

                      {/* Teams */}
                      <div className="flex-1 px-4">
                        <div className="flex items-center justify-between">
                          <div className="flex-1">
                            <p className="font-semibold text-slate-200 group-hover:text-white transition-colors">
                              {match.homeTeam}
                            </p>
                          </div>
                          <div className="px-4">
                            <span className="text-slate-500 font-mono">vs</span>
                          </div>
                          <div className="flex-1 text-right">
                            <p className="font-semibold text-slate-200 group-hover:text-white transition-colors">
                              {match.awayTeam}
                            </p>
                          </div>
                        </div>
                      </div>

                      {/* Competition & Stats Button */}
                      <div className="flex items-center gap-3">
                        {match.competition && (
                          <Badge variant="outline" className="border-navy-700 text-slate-400 hidden sm:inline-flex">
                            <Trophy className="w-3 h-3 mr-1" />
                            {match.competition}
                          </Badge>
                        )}
                        <Button 
                          variant="ghost" 
                          size="sm" 
                          className="text-purple-400 hover:text-purple-300 hover:bg-purple-500/10"
                        >
                          <BarChart3 className="w-4 h-4 mr-2" />
                          <span className="hidden sm:inline">Estatísticas</span>
                          <ChevronRight className="w-4 h-4" />
                        </Button>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            ))}
          </div>
        )}

        {/* Empty State */}
        {!isLoading && filteredMatches.length === 0 && (
          <div className="text-center py-16">
            <Calendar className="w-16 h-16 mx-auto text-navy-600 mb-4" />
            <h3 className="text-lg font-medium text-muted-foreground">
              Sem jogos no período seleccionado
            </h3>
            <p className="text-sm text-muted-foreground mt-1">
              Tente seleccionar um período diferente
            </p>
          </div>
        )}
      </main>

      {/* Stats Modal */}
      <MatchStatsModal
        match={selectedMatch}
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
      />
    </div>
  )
}
