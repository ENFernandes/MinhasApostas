import { useState } from 'react'
import { useRecordOutcome } from '@/hooks/useApi'

type Outcome = 'WON' | 'LOST' | 'VOID'

interface Props {
  recommendationId: string
  currentOutcome: Outcome | null
}

const BADGE_CONFIG: Record<Outcome, { label: string; className: string }> = {
  WON:  { label: '✓ Ganhou', className: 'bg-emerald-900/60 text-emerald-300 border border-emerald-700/50' },
  LOST: { label: '✗ Perdeu', className: 'bg-red-900/60 text-red-300 border border-red-700/50' },
  VOID: { label: '— Anulada', className: 'bg-slate-800 text-slate-400 border border-slate-600/50' },
}

const BUTTON_CONFIG: { outcome: Outcome; label: string; idle: string; active: string }[] = [
  {
    outcome: 'WON',
    label: '✓ Ganhou',
    idle:   'border border-emerald-800/60 text-emerald-600 hover:bg-emerald-900/40 hover:text-emerald-300 hover:border-emerald-600',
    active: 'bg-emerald-900/60 text-emerald-300 border border-emerald-600',
  },
  {
    outcome: 'LOST',
    label: '✗ Perdeu',
    idle:   'border border-red-900/60 text-red-700 hover:bg-red-900/40 hover:text-red-300 hover:border-red-700',
    active: 'bg-red-900/60 text-red-300 border border-red-700',
  },
  {
    outcome: 'VOID',
    label: '— Anulada',
    idle:   'border border-slate-700/60 text-slate-600 hover:bg-slate-800 hover:text-slate-400 hover:border-slate-600',
    active: 'bg-slate-800 text-slate-400 border border-slate-600',
  },
]

export default function RecommendationOutcomeButtons({ recommendationId, currentOutcome }: Props) {
  const { mutate, isPending } = useRecordOutcome()
  const [pending, setPending] = useState<Outcome | null>(null)

  if (currentOutcome) {
    const cfg = BADGE_CONFIG[currentOutcome]
    return (
      <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-mono ${cfg.className}`}>
        {cfg.label}
      </span>
    )
  }

  return (
    <div className="inline-flex rounded overflow-hidden" style={{ gap: 1 }}>
      {BUTTON_CONFIG.map(({ outcome, label, idle, active }) => {
        const isThis = pending === outcome
        const isLoading = isPending && isThis

        return (
          <button
            key={outcome}
            disabled={isPending}
            onClick={() => {
              setPending(outcome)
              mutate(
                { id: recommendationId, outcome },
                { onSettled: () => setPending(null) }
              )
            }}
            className={[
              'h-7 px-2.5 text-xs font-mono transition-colors duration-150 focus:outline-none',
              'disabled:opacity-50 disabled:cursor-not-allowed',
              isThis ? active : idle,
            ].join(' ')}
          >
            {isLoading ? (
              <span className="inline-block h-3 w-3 rounded-full border border-current border-t-transparent animate-spin" />
            ) : (
              label
            )}
          </button>
        )
      })}
    </div>
  )
}
