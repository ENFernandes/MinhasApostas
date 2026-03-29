import { useState } from 'react'
import { Calculator, Wallet, Percent, Info } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Slider } from '@/components/ui/slider'
import { useAppStore } from '@/stores/appStore'
import { formatCurrency } from '@/lib/utils'
import { getOddClassification } from './ValueBadge'

interface StakeCalculatorProps {
  odd: number
  modelProbability: number
  confidence: number
}

export function StakeCalculator({ odd, modelProbability, confidence }: StakeCalculatorProps) {
  const { settings } = useAppStore()
  const [kellyFraction, setKellyFraction] = useState(settings.kellyFraction * 100)

  // Calculate Kelly Criterion
  const impliedProbability = 1 / odd
  const value = (modelProbability - impliedProbability) / impliedProbability
  const kelly = (modelProbability * odd - 1) / (odd - 1)
  
  // RN-06: Confidence-based adjustment
  let confidenceMultiplier = 0.10
  if (confidence >= 8) confidenceMultiplier = 0.25
  else if (confidence >= 6) confidenceMultiplier = 0.15

  const adjustedKelly = kelly * confidenceMultiplier
  const maxStake = settings.maxStakePct / 100
  const stakePercentage = Math.min(Math.max(adjustedKelly, 0), maxStake)
  const stakeAmount = stakePercentage * settings.bankroll

  const classification = getOddClassification(value, confidence, kelly)

  return (
    <Card className="border-navy-700/50 bg-navy-900/50">
      <CardHeader className="pb-3">
        <CardTitle className="text-lg font-semibold text-slate-200 flex items-center gap-2">
          <Calculator className="w-5 h-5 text-gold-400" />
          Calculadora Kelly
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Bankroll Display */}
        <div className="flex items-center justify-between p-3 rounded-lg bg-navy-800/50">
          <div className="flex items-center gap-2">
            <Wallet className="w-4 h-4 text-gold-400" />
            <span className="text-sm text-slate-300">Banca Atual</span>
          </div>
          <span className="text-lg font-bold text-gold-400">
            {formatCurrency(settings.bankroll)}
          </span>
        </div>

        {/* Kelly Fraction Slider */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Percent className="w-4 h-4 text-slate-400" />
              <span className="text-sm text-slate-300">Fração Kelly</span>
            </div>
            <span className="text-sm font-medium text-slate-200">
              {kellyFraction.toFixed(0)}%
            </span>
          </div>
          <Slider
            value={kellyFraction.toString()}
            onChange={(e) => setKellyFraction(Number(e.target.value))}
            min={5}
            max={50}
            step={5}
            className="w-full"
          />
          <p className="text-xs text-slate-500">
            Fração Kelly recomendada: {settings.kellyFraction * 100}%
          </p>
        </div>

        {/* Calculations */}
        <div className="grid grid-cols-2 gap-3">
          <div className="p-3 rounded-lg bg-navy-800/30">
            <p className="text-xs text-slate-400 mb-1">Kelly Bruto</p>
            <p className="text-lg font-bold text-slate-200">
              {(kelly * 100).toFixed(1)}%
            </p>
          </div>
          <div className="p-3 rounded-lg bg-navy-800/30">
            <p className="text-xs text-slate-400 mb-1">Ajustado</p>
            <p className={`text-lg font-bold ${adjustedKelly > 0 ? 'text-emerald-400' : 'text-red-400'}`}>
              {(adjustedKelly * 100).toFixed(1)}%
            </p>
          </div>
          <div className="p-3 rounded-lg bg-navy-800/30">
            <p className="text-xs text-slate-400 mb-1">% da Banca</p>
            <p className={`text-lg font-bold ${classification.isValid ? 'text-emerald-400' : 'text-red-400'}`}>
              {(stakePercentage * 100).toFixed(1)}%
            </p>
          </div>
          <div className="p-3 rounded-lg bg-navy-800/30">
            <p className="text-xs text-slate-400 mb-1">Stake</p>
            <p className={`text-lg font-bold ${classification.isValid ? 'text-gold-400' : 'text-slate-500'}`}>
              {formatCurrency(stakeAmount)}
            </p>
          </div>
        </div>

        {/* Classification */}
        <div className={`p-3 rounded-lg border ${classification.isValid ? 'border-emerald-500/30 bg-emerald-500/10' : 'border-red-500/30 bg-red-500/10'}`}>
          <div className="flex items-start gap-2">
            <Info className={`w-4 h-4 mt-0.5 ${classification.isValid ? 'text-emerald-400' : 'text-red-400'}`} />
            <div>
              <p className={`text-sm font-medium ${classification.isValid ? 'text-emerald-400' : 'text-red-400'}`}>
                {classification.isValid ? 'Aposta Recomendada' : 'Não Apostar'}
              </p>
              <p className="text-xs text-slate-400 mt-0.5">
                {classification.reason}
              </p>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

export function calculateStake(
  bankroll: number,
  odd: number,
  probability: number,
  confidence: number,
  kellyFraction: number = 0.25
): {
  stake: number
  stakePercentage: number
  isValid: boolean
} {
  const kelly = (probability * odd - 1) / (odd - 1)
  
  if (kelly <= 0 || confidence < 6) {
    return { stake: 0, stakePercentage: 0, isValid: false }
  }

  let confidenceMultiplier = 0.10
  if (confidence >= 8) confidenceMultiplier = 0.25
  else if (confidence >= 6) confidenceMultiplier = 0.15

  const adjustedKelly = kelly * confidenceMultiplier * kellyFraction
  const maxStake = 0.05 // RN-03: Max 5%
  
  const stakePercentage = Math.min(adjustedKelly, maxStake)
  const stake = stakePercentage * bankroll

  return {
    stake,
    stakePercentage,
    isValid: true,
  }
}
