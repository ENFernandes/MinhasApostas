import { cn, getValueColor } from '@/lib/utils'

interface OddBadgeProps {
  odd: number
  value?: number
  size?: 'sm' | 'md' | 'lg'
  className?: string
}

export function OddBadge({ odd, value, size = 'md', className }: OddBadgeProps) {
  const sizeClasses = {
    sm: 'px-2 py-0.5 text-xs',
    md: 'px-3 py-1 text-sm',
    lg: 'px-4 py-2 text-base',
  }

  const hasValue = value !== undefined && value >= 0.05
  const valueColor = value !== undefined ? getValueColor(value) : ''

  return (
    <span
      className={cn(
        'inline-flex items-center justify-center rounded-lg font-mono font-semibold transition-all',
        sizeClasses[size],
        hasValue
          ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30'
          : 'bg-navy-800 text-navy-300 border border-navy-700',
        hasValue && 'animate-pulse-gold',
        className
      )}
    >
      {odd.toFixed(2)}
      {hasValue && value !== undefined && (
        <span className={cn('ml-1.5 text-xs opacity-80', valueColor)}>
          +{(value * 100).toFixed(1)}%
        </span>
      )}
    </span>
  )
}
