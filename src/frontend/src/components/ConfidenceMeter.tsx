import { cn, getConfidenceColor } from '@/lib/utils'

interface ConfidenceMeterProps {
  confidence: number
  size?: 'sm' | 'md' | 'lg'
  showLabel?: boolean
  className?: string
}

export function ConfidenceMeter({ 
  confidence, 
  size = 'md', 
  showLabel = true,
  className 
}: ConfidenceMeterProps) {
  const sizeClasses = {
    sm: 'h-1.5 w-20',
    md: 'h-2 w-28',
    lg: 'h-2.5 w-36',
  }

  const colorClasses = getConfidenceColor(confidence)
  const percentage = (confidence / 10) * 100

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <div className="flex items-center gap-2">
        <div className={cn('relative rounded-full bg-navy-800 overflow-hidden', sizeClasses[size])}>
          <div
            className={cn(
              'absolute left-0 top-0 h-full rounded-full transition-all duration-700 ease-out',
              confidence >= 8 && 'bg-gradient-to-r from-emerald-500 to-emerald-400',
              confidence >= 6 && confidence < 8 && 'bg-gradient-to-r from-amber-500 to-amber-400',
              confidence < 6 && 'bg-gradient-to-r from-red-500 to-red-400'
            )}
            style={{ width: `${percentage}%` }}
          />
        </div>
        {showLabel && (
          <span className={cn('text-xs font-medium tabular-nums', colorClasses)}>
            {confidence}/10
          </span>
        )}
      </div>
    </div>
  )
}
