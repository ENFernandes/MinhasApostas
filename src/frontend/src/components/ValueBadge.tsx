import { Check, X, AlertTriangle, TrendingUp } from 'lucide-react'
import { Badge } from '@/components/ui/badge'

interface ValueBadgeProps {
  isValid: boolean
  value: number
  confidence: number
  kellyFraction: number
  size?: 'sm' | 'md' | 'lg'
  showDetails?: boolean
}

export function ValueBadge({ 
  isValid, 
  value, 
  confidence, 
  kellyFraction: _kellyFraction,
  size = 'md',
  showDetails = false 
}: ValueBadgeProps) {
  const sizeClasses = {
    sm: 'text-xs px-2 py-0.5',
    md: 'text-sm px-2.5 py-1',
    lg: 'text-base px-3 py-1.5',
  }

  if (!isValid) {
    return (
      <Badge 
        variant="outline" 
        className={`${sizeClasses[size]} border-red-500/30 bg-red-500/10 text-red-400`}
      >
        <X className={`${size === 'sm' ? 'w-3 h-3' : 'w-4 h-4'} mr-1`} />
        Não Apostar
      </Badge>
    )
  }

  // Valid bet - determine quality
  let quality: 'excellent' | 'good' | 'fair' = 'fair'
  if (value >= 0.15 && confidence >= 8) quality = 'excellent'
  else if (value >= 0.08 && confidence >= 6) quality = 'good'

  const qualityConfig = {
    excellent: {
      color: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-400',
      icon: TrendingUp,
      label: 'Value Bet',
    },
    good: {
      color: 'border-blue-500/30 bg-blue-500/10 text-blue-400',
      icon: Check,
      label: 'Boa Odd',
    },
    fair: {
      color: 'border-amber-500/30 bg-amber-500/10 text-amber-400',
      icon: AlertTriangle,
      label: 'Razoável',
    },
  }

  const config = qualityConfig[quality]
  const Icon = config.icon

  return (
    <div className="flex flex-col gap-1">
      <Badge 
        variant="outline" 
        className={`${sizeClasses[size]} ${config.color}`}
      >
        <Icon className={`${size === 'sm' ? 'w-3 h-3' : 'w-4 h-4'} mr-1`} />
        {config.label}
      </Badge>
      {showDetails && (
        <div className="flex items-center gap-2 text-xs text-slate-400">
          <span>Value: {(value * 100).toFixed(1)}%</span>
          <span>•</span>
          <span>Conf: {confidence}/10</span>
        </div>
      )}
    </div>
  )
}

export function getOddClassification(value: number, confidence: number, kellyFraction: number): {
  isValid: boolean
  quality: 'excellent' | 'good' | 'fair' | 'poor'
  stakePercentage: number
  reason: string
} {
  // RN-06: Confidence-based Kelly
  let adjustedKelly = kellyFraction
  if (confidence >= 8) adjustedKelly = kellyFraction * 0.25
  else if (confidence >= 6) adjustedKelly = kellyFraction * 0.15
  else adjustedKelly = kellyFraction * 0.10

  // RN-03: Max 5% per bet
  const maxStake = 0.05
  const stakePercentage = Math.min(adjustedKelly, maxStake)

  // Validation logic
  if (value <= 0 || confidence < 6 || kellyFraction <= 0) {
    return {
      isValid: false,
      quality: 'poor',
      stakePercentage: 0,
      reason: confidence < 6 ? 'Confiança insuficiente' : value <= 0 ? 'Sem valor positivo' : 'Kelly negativo',
    }
  }

  if (value >= 0.15 && confidence >= 8) {
    return {
      isValid: true,
      quality: 'excellent',
      stakePercentage,
      reason: 'Excelente valor com alta confiança',
    }
  }

  if (value >= 0.08 && confidence >= 6) {
    return {
      isValid: true,
      quality: 'good',
      stakePercentage,
      reason: 'Bom valor com confiança adequada',
    }
  }

  return {
    isValid: true,
    quality: 'fair',
    stakePercentage,
    reason: 'Valor marginal',
  }
}
