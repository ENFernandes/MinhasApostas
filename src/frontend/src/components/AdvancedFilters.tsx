import { motion } from 'framer-motion'
import { SlidersHorizontal, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useAppStore } from '@/stores/appStore'

export function AdvancedFilters() {
  const {
    selectedSport,
    setSelectedSport,
    minOddFilter,
    maxOddFilter,
    minValueFilter,
    minConfidenceFilter,
    setMinOddFilter,
    setMaxOddFilter,
    setMinValueFilter,
    setMinConfidenceFilter,
    clearFilters,
  } = useAppStore()

  const hasActiveFilters =
    selectedSport !== 'all' ||
    minOddFilter !== null ||
    maxOddFilter !== null ||
    minValueFilter !== null ||
    minConfidenceFilter !== null

  return (
    <Card className="border-navy-700/50 bg-navy-900/30">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-lg font-semibold text-slate-200 flex items-center gap-2">
            <SlidersHorizontal className="w-5 h-5 text-gold-400" />
            Filtros Avançados
          </CardTitle>
          {hasActiveFilters && (
            <Button
              variant="ghost"
              size="sm"
              onClick={clearFilters}
              className="text-slate-400 hover:text-slate-200"
            >
              <X className="w-4 h-4 mr-1" />
              Limpar
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
          {/* Sport Filter */}
          <div className="space-y-2">
            <label className="text-sm text-slate-400">Desporto</label>
            <select
              value={selectedSport}
              onChange={(e) => setSelectedSport(e.target.value as any)}
              className="w-full px-3 py-2 rounded-lg bg-navy-800 border border-navy-700 text-slate-200 focus:outline-none focus:border-gold-500/50"
            >
              <option value="all">Todos</option>
              <option value="football">Futebol</option>
              <option value="tennis">Ténis</option>
            </select>
          </div>

          {/* Min Odd Filter */}
          <div className="space-y-2">
            <label className="text-sm text-slate-400">Odd Mínima</label>
            <input
              type="number"
              step="0.1"
              min="1.1"
              placeholder="Ex: 1.50"
              value={minOddFilter || ''}
              onChange={(e) => {
                const val = parseFloat(e.target.value)
                setMinOddFilter(isNaN(val) ? null : val)
              }}
              className="w-full px-3 py-2 rounded-lg bg-navy-800 border border-navy-700 text-slate-200 placeholder-slate-600 focus:outline-none focus:border-gold-500/50"
            />
          </div>

          {/* Max Odd Filter */}
          <div className="space-y-2">
            <label className="text-sm text-slate-400">Odd Máxima</label>
            <input
              type="number"
              step="0.1"
              min="1.1"
              placeholder="Ex: 5.00"
              value={maxOddFilter || ''}
              onChange={(e) => {
                const val = parseFloat(e.target.value)
                setMaxOddFilter(isNaN(val) ? null : val)
              }}
              className="w-full px-3 py-2 rounded-lg bg-navy-800 border border-navy-700 text-slate-200 placeholder-slate-600 focus:outline-none focus:border-gold-500/50"
            />
          </div>

          {/* Min Value Filter */}
          <div className="space-y-2">
            <label className="text-sm text-slate-400">Value Mínimo (%)</label>
            <input
              type="number"
              step="0.5"
              min="0"
              placeholder="Ex: 5"
              value={minValueFilter ? minValueFilter * 100 : ''}
              onChange={(e) => {
                const val = parseFloat(e.target.value)
                setMinValueFilter(isNaN(val) ? null : val / 100)
              }}
              className="w-full px-3 py-2 rounded-lg bg-navy-800 border border-navy-700 text-slate-200 placeholder-slate-600 focus:outline-none focus:border-gold-500/50"
            />
          </div>

          {/* Min Confidence Filter */}
          <div className="space-y-2">
            <label className="text-sm text-slate-400">Confiança Mínima</label>
            <input
              type="number"
              step="1"
              min="1"
              max="10"
              placeholder="Ex: 7"
              value={minConfidenceFilter || ''}
              onChange={(e) => {
                const val = parseInt(e.target.value)
                setMinConfidenceFilter(isNaN(val) ? null : val)
              }}
              className="w-full px-3 py-2 rounded-lg bg-navy-800 border border-navy-700 text-slate-200 placeholder-slate-600 focus:outline-none focus:border-gold-500/50"
            />
          </div>
        </div>

        {/* Active Filters Badge */}
        {hasActiveFilters && (
          <motion.div
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            className="flex flex-wrap gap-2 mt-4 pt-4 border-t border-navy-700/50"
          >
            <span className="text-sm text-slate-400">Filtros ativos:</span>
            {selectedSport !== 'all' && (
              <span className="px-2 py-1 rounded-full bg-gold-500/10 text-gold-400 text-xs border border-gold-500/20">
                {selectedSport === 'football' ? 'Futebol' : 'Ténis'}
              </span>
            )}
            {minOddFilter !== null && (
              <span className="px-2 py-1 rounded-full bg-blue-500/10 text-blue-400 text-xs border border-blue-500/20">
                Odd ≥ {minOddFilter.toFixed(2)}
              </span>
            )}
            {maxOddFilter !== null && (
              <span className="px-2 py-1 rounded-full bg-blue-500/10 text-blue-400 text-xs border border-blue-500/20">
                Odd ≤ {maxOddFilter.toFixed(2)}
              </span>
            )}
            {minValueFilter !== null && (
              <span className="px-2 py-1 rounded-full bg-emerald-500/10 text-emerald-400 text-xs border border-emerald-500/20">
                Value ≥ {(minValueFilter * 100).toFixed(0)}%
              </span>
            )}
            {minConfidenceFilter !== null && (
              <span className="px-2 py-1 rounded-full bg-purple-500/10 text-purple-400 text-xs border border-purple-500/20">
                Confiança ≥ {minConfidenceFilter}/10
              </span>
            )}
          </motion.div>
        )}
      </CardContent>
    </Card>
  )
}
