import { motion } from 'framer-motion'
import { 
  Wallet, 
  Brain, 
  Shield,
  Save
} from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { useAppStore } from '@/stores/appStore'
import { formatCurrency } from '@/lib/utils'

export default function SettingsPage() {
  const { settings, updateSettings } = useAppStore()

  return (
    <div className="min-h-screen bg-gradient-to-br from-background via-background to-navy-950">
      {/* Header */}
      <header className="border-b border-navy-700/50 bg-background/80 backdrop-blur-xl">
        <div className="container mx-auto px-4 py-6">
          <h1 className="text-3xl font-display font-bold gradient-text">
            Configurações
          </h1>
          <p className="text-muted-foreground mt-1">
            Personalize as preferências do sistema
          </p>
        </div>
      </header>

      <main className="container mx-auto px-4 py-8 max-w-3xl">
        <div className="space-y-6">
          {/* Bankroll Settings */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
          >
            <Card className="border-navy-700/50">
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Wallet className="w-5 h-5 text-gold-400" />
                  Gestão de Banca
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                  <div className="space-y-2">
                    <label className="text-sm font-medium text-muted-foreground">
                      Banca Total (€)
                    </label>
                    <input
                      type="number"
                      value={settings.bankroll}
                      onChange={(e) => updateSettings({ bankroll: parseFloat(e.target.value) || 0 })}
                      className="w-full px-4 py-3 rounded-lg bg-navy-800 border border-navy-700 
                               focus:border-gold-500 focus:ring-1 focus:ring-gold-500 
                               transition-all outline-none"
                    />
                  </div>

                  <div className="space-y-2">
                    <label className="text-sm font-medium text-muted-foreground">
                      Stake Máxima (%)
                    </label>
                    <input
                      type="number"
                      value={settings.maxStakePct * 100}
                      onChange={(e) => updateSettings({ maxStakePct: parseFloat(e.target.value) / 100 || 0 })}
                      className="w-full px-4 py-3 rounded-lg bg-navy-800 border border-navy-700 
                               focus:border-gold-500 focus:ring-1 focus:ring-gold-500 
                               transition-all outline-none"
                      min="1"
                      max="10"
                      step="0.5"
                    />
                    <p className="text-xs text-muted-foreground">
                      Máximo: {formatCurrency(settings.bankroll * settings.maxStakePct)}
                    </p>
                  </div>

                  <div className="space-y-2">
                    <label className="text-sm font-medium text-muted-foreground">
                      Fração Kelly (%)
                    </label>
                    <input
                      type="number"
                      value={settings.kellyFraction * 100}
                      onChange={(e) => updateSettings({ kellyFraction: parseFloat(e.target.value) / 100 || 0 })}
                      className="w-full px-4 py-3 rounded-lg bg-navy-800 border border-navy-700 
                               focus:border-gold-500 focus:ring-1 focus:ring-gold-500 
                               transition-all outline-none"
                      min="10"
                      max="50"
                      step="5"
                    />
                    <p className="text-xs text-muted-foreground">
                      Recomendado: 25% para reduzir variância
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          {/* Analysis Thresholds */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
          >
            <Card className="border-navy-700/50">
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Shield className="w-5 h-5 text-gold-400" />
                  Thresholds de Análise
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-6">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                  <div className="space-y-2">
                    <label className="text-sm font-medium text-muted-foreground">
                      Value Mínimo (%)
                    </label>
                    <input
                      type="number"
                      value={settings.minValue * 100}
                      onChange={(e) => updateSettings({ minValue: parseFloat(e.target.value) / 100 || 0 })}
                      className="w-full px-4 py-3 rounded-lg bg-navy-800 border border-navy-700 
                               focus:border-gold-500 focus:ring-1 focus:ring-gold-500 
                               transition-all outline-none"
                      min="3"
                      max="15"
                      step="0.5"
                    />
                    <p className="text-xs text-muted-foreground">
                      Mínimo recomendado: 5%
                    </p>
                  </div>

                  <div className="space-y-2">
                    <label className="text-sm font-medium text-muted-foreground">
                      Confiança Mínima (1-10)
                    </label>
                    <input
                      type="number"
                      value={settings.minConfidence}
                      onChange={(e) => updateSettings({ minConfidence: parseInt(e.target.value) || 6 })}
                      className="w-full px-4 py-3 rounded-lg bg-navy-800 border border-navy-700 
                               focus:border-gold-500 focus:ring-1 focus:ring-gold-500 
                               transition-all outline-none"
                      min="1"
                      max="10"
                      step="1"
                    />
                    <p className="text-xs text-muted-foreground">
                      Mínimo recomendado: 6/10
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          {/* LLM Provider */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
          >
            <Card className="border-navy-700/50">
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-lg">
                  <Brain className="w-5 h-5 text-gold-400" />
                  Modelo de IA
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="p-4 rounded-lg bg-navy-800/50 border border-navy-700">
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="font-medium">Provider Actual</p>
                      <p className="text-sm text-muted-foreground capitalize">
                        {settings.llmProvider}
                      </p>
                    </div>
                    <span className="px-3 py-1 rounded-full bg-gold-500/10 text-gold-400 text-sm font-medium border border-gold-500/20">
                      Activo
                    </span>
                  </div>
                  <p className="text-xs text-muted-foreground mt-3">
                    Para alterar o provider, modifique a variável de ambiente LLM_PROVIDER 
                    e reinicie o serviço.
                  </p>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          {/* Save Button */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.3 }}
          >
            <Button 
              className="w-full h-12 bg-gradient-to-r from-gold-500 to-gold-400 
                       text-navy-950 font-semibold text-lg
                       hover:from-gold-400 hover:to-gold-300"
            >
              <Save className="w-5 h-5 mr-2" />
              Guardar Configurações
            </Button>
          </motion.div>
        </div>
      </main>
    </div>
  )
}
