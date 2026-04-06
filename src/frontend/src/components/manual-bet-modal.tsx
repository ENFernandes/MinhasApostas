import { useMemo, useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { X, Wallet, Calculator, PenLine, ListFilter } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useAppStore } from '@/stores/appStore'
import { useMatches, useRegisterBet } from '@/hooks/useApi'
import { formatCurrency, formatDate } from '@/lib/utils'
import type { Match } from '@/types'

type RegistrationSport = 'football' | 'tennis' | 'manual'

function sportLabel(s: Match['sport']): string {
  if (s === 'football') return 'Futebol'
  if (s === 'tennis') return 'Ténis'
  return 'Outro'
}

function matchDisplayLine(m: Match): string {
  const base = `${m.homeTeam} vs ${m.awayTeam}`
  return m.competition ? `${base} (${m.competition})` : base
}

export function ManualBetModal() {
  const { isManualBetModalOpen, closeManualBetModal, settings } = useAppStore()
  const registerBet = useRegisterBet()

  const [registrationSport, setRegistrationSport] = useState<RegistrationSport>('football')
  const [eventLabel, setEventLabel] = useState('')
  const [linkedMatchId, setLinkedMatchId] = useState<string | null>(null)
  const [showAllSuggestions, setShowAllSuggestions] = useState(false)
  const [stake, setStake] = useState(0)
  const [odd, setOdd] = useState(0)
  const [manualMarket, setManualMarket] = useState('')
  const [manualSelection, setManualSelection] = useState('')
  const [manualBookmaker, setManualBookmaker] = useState('')

  const { from, to } = useMemo(() => {
    const a = new Date()
    a.setHours(0, 0, 0, 0)
    const b = new Date()
    b.setDate(b.getDate() + 14)
    b.setHours(23, 59, 59, 999)
    return { from: a, to: b }
  }, [isManualBetModalOpen])

  const { data: matches, isLoading } = useMatches(from, to, undefined, true, isManualBetModalOpen)

  const sortedMatches = useMemo(() => {
    if (!matches?.length) return []
    return [...matches].sort(
      (a, b) => new Date(a.commenceTime).getTime() - new Date(b.commenceTime).getTime()
    )
  }, [matches])

  const suggestions = useMemo(() => {
    let list = sortedMatches
    if (!showAllSuggestions && registrationSport !== 'manual') {
      list = list.filter((m) => m.sport === registrationSport)
    }
    const q = eventLabel.trim().toLowerCase()
    if (q.length >= 2) {
      list = list.filter(
        (m) =>
          m.homeTeam.toLowerCase().includes(q) ||
          m.awayTeam.toLowerCase().includes(q) ||
          (m.competition?.toLowerCase().includes(q) ?? false)
      )
    }
    return list.slice(0, 14)
  }, [sortedMatches, showAllSuggestions, registrationSport, eventLabel])

  const linkedMatch = useMemo(
    () => (linkedMatchId ? sortedMatches.find((m) => m.id === linkedMatchId) ?? null : null),
    [sortedMatches, linkedMatchId]
  )

  useEffect(() => {
    if (!isManualBetModalOpen) {
      setRegistrationSport('football')
      setEventLabel('')
      setLinkedMatchId(null)
      setShowAllSuggestions(false)
      setStake(0)
      setOdd(0)
      setManualMarket('')
      setManualSelection('')
      setManualBookmaker('')
    }
  }, [isManualBetModalOpen])

  useEffect(() => {
    setLinkedMatchId(null)
  }, [registrationSport])

  const portalTarget = typeof document !== 'undefined' ? document.body : null

  const hasTarget = linkedMatchId !== null || eventLabel.trim().length >= 3

  const handleSubmit = async () => {
    if (stake <= 0 || odd <= 0) return
    if (!manualMarket.trim() || !manualSelection.trim()) return
    if (!hasTarget) return

    const base = {
      recommendationId: '00000000-0000-0000-0000-000000000000',
      market: manualMarket.trim(),
      betSelection: manualSelection.trim(),
      bookmaker: manualBookmaker.trim() || 'Manual',
      stakeActual: stake,
      oddActual: odd,
      outcome: 'PENDING',
      profitLoss: 0,
    }

    if (linkedMatchId) {
      await registerBet.mutateAsync({
        ...base,
        matchId: linkedMatchId,
      })
    } else {
      await registerBet.mutateAsync({
        ...base,
        manualEventLabel: eventLabel.trim(),
        manualSport: registrationSport,
      })
    }

    closeManualBetModal()
  }

  function pickSuggestion(m: Match) {
    setLinkedMatchId(m.id)
    setEventLabel(`${matchDisplayLine(m)} · ${formatDate(m.commenceTime)}`)
    if (m.sport === 'football' || m.sport === 'tennis') {
      setRegistrationSport(m.sport)
    }
  }

  const canSubmit =
    hasTarget &&
    stake > 0 &&
    odd > 0 &&
    !!manualMarket.trim() &&
    !!manualSelection.trim() &&
    !registerBet.isPending

  return portalTarget
    ? createPortal(
        <AnimatePresence>
          {isManualBetModalOpen && (
            <>
              <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50"
                onClick={closeManualBetModal}
              />
              <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-8 overflow-y-auto pointer-events-none">
                <motion.div
                  initial={{ opacity: 0, scale: 0.98, y: 16 }}
                  animate={{ opacity: 1, scale: 1, y: 0 }}
                  exit={{ opacity: 0, scale: 0.98, y: 16 }}
                  className="w-full max-w-lg pointer-events-auto"
                  role="dialog"
                  aria-modal="true"
                  aria-label="Aposta manual — escolher jogo"
                  onClick={(e) => e.stopPropagation()}
                >
                  <Card className="border-amber-500/25 shadow-2xl shadow-amber-500/10 max-h-[90vh] overflow-y-auto">
                    <CardHeader className="flex flex-row items-center justify-between">
                      <CardTitle className="flex items-center gap-2 text-xl">
                        <ListFilter className="w-5 h-5 text-amber-400" />
                        Aposta sem sugestão
                      </CardTitle>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        onClick={closeManualBetModal}
                      >
                        <X className="w-4 h-4" />
                      </Button>
                    </CardHeader>
                    <CardContent className="space-y-5">
                      <p className="text-sm text-muted-foreground">
                        Indique o <strong className="text-slate-300">desporto</strong> e escreva o{' '}
                        <strong className="text-slate-300">jogo</strong> no campo livre (mesmo que não
                        exista na app). Opcionalmente use uma sugestão abaixo para ligar à BD.
                      </p>

                      <div className="space-y-2">
                        <label className="text-sm font-medium">Desporto do evento</label>
                        <select
                          value={registrationSport}
                          onChange={(e) =>
                            setRegistrationSport(e.target.value as RegistrationSport)
                          }
                          className="w-full px-3 py-2.5 rounded-lg bg-navy-800 border border-navy-700 text-slate-200 focus:outline-none focus:border-amber-500/60"
                        >
                          <option value="football">Futebol</option>
                          <option value="tennis">Ténis</option>
                          <option value="manual">Outro / não listado</option>
                        </select>
                      </div>

                      <div className="space-y-2">
                        <label htmlFor="manual-event-label" className="text-sm font-medium">
                          Jogo (texto livre)
                        </label>
                        <input
                          id="manual-event-label"
                          type="text"
                          value={eventLabel}
                          onChange={(e) => {
                            setEventLabel(e.target.value)
                            setLinkedMatchId(null)
                          }}
                          autoComplete="off"
                          className="w-full px-3 py-2.5 rounded-lg bg-navy-800 border border-navy-700 text-slate-200 placeholder:text-slate-500 focus:outline-none focus:border-amber-500/60 text-sm"
                          placeholder="ex.: Benfica vs Sporting · Taça · ou qualquer jogo que a app não mostre"
                        />
                        <p className="text-xs text-slate-500">
                          Mínimo 3 caracteres se não usar sugestão. Se escolher uma sugestão, a aposta
                          fica ligada a esse jogo na BD.
                        </p>
                      </div>

                      {linkedMatch && (
                        <div className="p-3 rounded-lg bg-emerald-500/10 border border-emerald-500/25 text-sm">
                          <p className="text-xs text-emerald-400/90 mb-1">Ligado a jogo na app</p>
                          <p className="font-medium text-slate-200">{matchDisplayLine(linkedMatch)}</p>
                        </div>
                      )}

                      <div className="space-y-2">
                        <div className="flex items-center justify-between gap-2 flex-wrap">
                          <span className="text-sm font-medium text-slate-300">
                            Sugestões (opcional)
                          </span>
                          <label className="flex items-center gap-2 text-xs text-slate-400 cursor-pointer">
                            <input
                              type="checkbox"
                              checked={showAllSuggestions}
                              onChange={(e) => setShowAllSuggestions(e.target.checked)}
                              className="rounded border-navy-600"
                            />
                            Mostrar todos os desportos
                          </label>
                        </div>
                        {isLoading ? (
                          <p className="text-sm text-muted-foreground py-2">A carregar…</p>
                        ) : suggestions.length === 0 ? (
                          <p className="text-xs text-slate-500 py-1">
                            Sem sugestões (escreva o jogo à mão ou mude o filtro).
                          </p>
                        ) : (
                          <ul className="flex flex-col gap-1.5 max-h-40 overflow-y-auto pr-1">
                            {suggestions.map((m) => (
                              <li key={m.id}>
                                <button
                                  type="button"
                                  onClick={() => pickSuggestion(m)}
                                  className="w-full text-left px-3 py-2 rounded-lg bg-navy-900/80 border border-navy-700 hover:border-amber-500/40 text-xs text-slate-300 transition-colors"
                                >
                                  <span className="text-amber-500/90">{formatDate(m.commenceTime)}</span>
                                  {' · '}
                                  <span className="text-slate-400">{sportLabel(m.sport)}</span>
                                  {' · '}
                                  {m.homeTeam} vs {m.awayTeam}
                                  {m.competition ? (
                                    <span className="text-slate-500"> ({m.competition})</span>
                                  ) : null}
                                </button>
                              </li>
                            ))}
                          </ul>
                        )}
                      </div>

                      <div className="p-3 rounded-lg bg-navy-800/50 border border-navy-600 space-y-3">
                        <div className="flex items-center gap-2 text-xs font-medium text-slate-300">
                          <PenLine className="w-4 h-4 text-slate-400" />
                          Detalhes da aposta
                        </div>
                        <div className="space-y-2">
                          <label className="text-sm font-medium">Mercado</label>
                          <input
                            type="text"
                            value={manualMarket}
                            onChange={(e) => setManualMarket(e.target.value)}
                            className="w-full px-3 py-2 rounded-lg bg-navy-900 border border-navy-700 focus:border-amber-500/60 focus:ring-1 focus:ring-amber-500/30 outline-none text-sm"
                            placeholder="ex.: 1X2, Over/Under 2.5"
                          />
                        </div>
                        <div className="space-y-2">
                          <label className="text-sm font-medium">Seleção</label>
                          <input
                            type="text"
                            value={manualSelection}
                            onChange={(e) => setManualSelection(e.target.value)}
                            className="w-full px-3 py-2 rounded-lg bg-navy-900 border border-navy-700 focus:border-amber-500/60 focus:ring-1 focus:ring-amber-500/30 outline-none text-sm"
                            placeholder="ex.: Casa, Over 2.5"
                          />
                        </div>
                        <div className="space-y-2">
                          <label className="text-sm font-medium">Casa de apostas (opcional)</label>
                          <input
                            type="text"
                            value={manualBookmaker}
                            onChange={(e) => setManualBookmaker(e.target.value)}
                            className="w-full px-3 py-2 rounded-lg bg-navy-900 border border-navy-700 focus:border-amber-500/60 focus:ring-1 focus:ring-amber-500/30 outline-none text-sm"
                            placeholder="ex.: Betclic"
                          />
                        </div>
                      </div>

                      <div className="p-4 rounded-lg bg-navy-800/50 border border-navy-700">
                        <div className="flex items-center justify-between mb-2">
                          <span className="text-sm text-muted-foreground">Banca</span>
                          <span className="text-lg font-semibold text-gold-400">
                            {formatCurrency(settings.bankroll)}
                          </span>
                        </div>
                        <div className="flex items-center justify-between text-sm">
                          <span className="text-muted-foreground">Stake máx. (5%)</span>
                          <span>{formatCurrency(settings.bankroll * 0.05)}</span>
                        </div>
                      </div>

                      <div className="space-y-2">
                        <label className="text-sm font-medium">Odd</label>
                        <input
                          type="number"
                          value={odd || ''}
                          onChange={(e) => setOdd(parseFloat(e.target.value) || 0)}
                          className="w-full px-4 py-3 rounded-lg bg-navy-800 border border-navy-700 focus:border-amber-500/60 focus:ring-1 focus:ring-amber-500/30 outline-none text-lg"
                          placeholder="1.50"
                          min="1"
                          step="0.01"
                        />
                      </div>

                      <div className="space-y-2">
                        <label className="text-sm font-medium flex items-center gap-2">
                          <Calculator className="w-4 h-4 text-amber-400" />
                          Valor da aposta (€)
                        </label>
                        <input
                          type="number"
                          value={stake || ''}
                          onChange={(e) => setStake(parseFloat(e.target.value) || 0)}
                          className="w-full px-4 py-3 rounded-lg bg-navy-800 border border-navy-700 focus:border-amber-500/60 focus:ring-1 focus:ring-amber-500/30 outline-none text-lg"
                          placeholder="0.00"
                          min="0"
                          step="0.5"
                        />
                        <div className="flex gap-2">
                          {[5, 10, 25, 50].map((amount) => (
                            <button
                              key={amount}
                              type="button"
                              onClick={() => setStake(amount)}
                              className="flex-1 py-2 px-3 rounded-md bg-navy-800 hover:bg-navy-700 border border-navy-700 text-sm font-medium"
                            >
                              €{amount}
                            </button>
                          ))}
                        </div>
                      </div>

                      {stake > 0 && odd > 0 && (
                        <div className="p-3 rounded-lg bg-emerald-500/5 border border-emerald-500/20 text-sm">
                          <div className="flex justify-between">
                            <span className="text-slate-400">Lucro potencial</span>
                            <span className="font-semibold text-emerald-400">
                              +{formatCurrency(stake * odd - stake)}
                            </span>
                          </div>
                        </div>
                      )}

                      <Button
                        type="button"
                        className="w-full h-12 bg-gradient-to-r from-amber-500 to-amber-400 text-navy-950 font-semibold text-lg hover:from-amber-400 hover:to-amber-300 disabled:opacity-50 inline-flex items-center justify-center gap-2"
                        disabled={!canSubmit}
                        onClick={handleSubmit}
                      >
                        {registerBet.isPending ? (
                          'A registar…'
                        ) : (
                          <>
                            <Wallet className="w-5 h-5" />
                            Confirmar aposta
                          </>
                        )}
                      </Button>
                    </CardContent>
                  </Card>
                </motion.div>
              </div>
            </>
          )}
        </AnimatePresence>,
        portalTarget
      )
    : null
}
