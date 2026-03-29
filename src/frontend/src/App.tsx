import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Routes, Route, Link, useLocation } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { 
  LayoutDashboard, 
  History, 
  Settings, 
  Trophy,
  Calendar,
  LineChart,
  CalendarDays
} from 'lucide-react'
import DashboardPage from './pages/dashboard'
import HistoryPage from './pages/history'
import SettingsPage from './pages/settings'
import TodayMatchesPage from './pages/today'
import UpcomingMatchesPage from './pages/upcoming'
import OddsAnalyzerPage from './pages/odds'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
})

function Navigation() {
  const location = useLocation()
  
  const navItems = [
    { path: '/', icon: LayoutDashboard, label: 'Dashboard' },
    { path: '/today', icon: Calendar, label: 'Hoje' },
    { path: '/upcoming', icon: CalendarDays, label: 'Futuros' },
    { path: '/odds', icon: LineChart, label: 'Odds' },
    { path: '/history', icon: History, label: 'Histórico' },
    { path: '/settings', icon: Settings, label: 'Config' },
  ]

  return (
    <nav className="fixed bottom-0 left-0 right-0 z-50 bg-background/90 backdrop-blur-xl border-t border-navy-700/50 lg:top-0 lg:bottom-auto lg:border-t-0 lg:border-b">
      <div className="container mx-auto px-4">
        <div className="flex items-center justify-around lg:justify-start lg:gap-8 py-3 lg:py-4">
          {/* Logo - only on desktop */}
          <Link to="/" className="hidden lg:flex items-center gap-2 mr-8">
            <div className="p-1.5 rounded-lg bg-gradient-to-br from-gold-500 to-gold-600">
              <Trophy className="w-5 h-5 text-navy-950" />
            </div>
            <span className="font-display font-bold text-lg gradient-text">Minhas Apostas</span>
          </Link>

          {/* Nav Items */}
          {navItems.map((item) => {
            const Icon = item.icon
            const isActive = location.pathname === item.path
            
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`relative flex flex-col lg:flex-row items-center gap-1 lg:gap-2 px-3 py-2 rounded-lg transition-all ${
                  isActive 
                    ? 'text-gold-400' 
                    : 'text-muted-foreground hover:text-foreground'
                }`}
              >
                <Icon className="w-5 h-5" />
                <span className="text-xs lg:text-sm font-medium">{item.label}</span>
                
                {/* Active indicator */}
                {isActive && (
                  <motion.div
                    layoutId="activeNav"
                    className="absolute -bottom-3 lg:bottom-0 left-0 right-0 h-0.5 bg-gradient-to-r from-gold-500 to-gold-400 lg:hidden"
                    transition={{ type: 'spring', stiffness: 380, damping: 30 }}
                  />
                )}
              </Link>
            )
          })}
        </div>
      </div>
    </nav>
  )
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <div className="min-h-screen bg-background pb-20 lg:pb-0 lg:pt-20">
          <Navigation />
          
          <AnimatePresence mode="wait">
              <Routes>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/today" element={<TodayMatchesPage />} />
              <Route path="/upcoming" element={<UpcomingMatchesPage />} />
              <Route path="/odds" element={<OddsAnalyzerPage />} />
              <Route path="/history" element={<HistoryPage />} />
              <Route path="/settings" element={<SettingsPage />} />
            </Routes>
          </AnimatePresence>
        </div>
      </BrowserRouter>
    </QueryClientProvider>
  )
}

export default App
