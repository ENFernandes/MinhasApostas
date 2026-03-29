import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { UserSettings } from '@/types'

interface AppState {
  // UI State
  selectedSport: 'all' | 'football' | 'tennis'
  selectedCompetition: string | null
  isStakeModalOpen: boolean
  selectedMatchId: string | null
  stakeModalRecommendation: any | null
  stakeModalMatch: any | null
  isAnalysisModalOpen: boolean
  selectedAnalysisMatchId: string | null
  // Advanced filters
  minOddFilter: number | null
  maxOddFilter: number | null
  minValueFilter: number | null
  minConfidenceFilter: number | null

  // User Settings
  settings: UserSettings

  // Actions
  setSelectedSport: (sport: 'all' | 'football' | 'tennis') => void
  setSelectedCompetition: (competition: string | null) => void
  openStakeModal: (matchId: string, match?: any, recommendation?: any) => void
  closeStakeModal: () => void
  openAnalysisModal: (matchId: string) => void
  closeAnalysisModal: () => void
  setMinOddFilter: (value: number | null) => void
  setMaxOddFilter: (value: number | null) => void
  setMinValueFilter: (value: number | null) => void
  setMinConfidenceFilter: (value: number | null) => void
  clearFilters: () => void
  updateSettings: (settings: Partial<UserSettings>) => void
}

export const useAppStore = create<AppState>()(
  persist(
    (set) => ({
      // Initial UI State
      selectedSport: 'all',
      selectedCompetition: null,
      isStakeModalOpen: false,
      selectedMatchId: null,
      stakeModalRecommendation: null,
      stakeModalMatch: null,
      isAnalysisModalOpen: false,
      selectedAnalysisMatchId: null,
      // Advanced filters
      minOddFilter: null,
      maxOddFilter: null,
      minValueFilter: null,
      minConfidenceFilter: null,

      // Initial Settings
      settings: {
        bankroll: 100,
        minValue: 0.05,
        minConfidence: 6,
        maxStakePct: 0.05,
        kellyFraction: 0.25,
        llmProvider: 'ollama',
      },
      
      // Actions
      setSelectedSport: (sport) => set({ selectedSport: sport }),
      setSelectedCompetition: (competition) => set({ selectedCompetition: competition }),
      openStakeModal: (matchId, match, recommendation) => set({
        isStakeModalOpen: true,
        selectedMatchId: matchId,
        stakeModalMatch: match ?? null,
        stakeModalRecommendation: recommendation ?? null,
      }),
      closeStakeModal: () => set({
        isStakeModalOpen: false,
        selectedMatchId: null,
        stakeModalMatch: null,
        stakeModalRecommendation: null,
      }),
      openAnalysisModal: (matchId) => set({ isAnalysisModalOpen: true, selectedAnalysisMatchId: matchId }),
      closeAnalysisModal: () => set({ isAnalysisModalOpen: false, selectedAnalysisMatchId: null }),
      setMinOddFilter: (value) => set({ minOddFilter: value }),
      setMaxOddFilter: (value) => set({ maxOddFilter: value }),
      setMinValueFilter: (value) => set({ minValueFilter: value }),
      setMinConfidenceFilter: (value) => set({ minConfidenceFilter: value }),
      clearFilters: () => set({
        selectedSport: 'all',
        selectedCompetition: null,
        minOddFilter: null,
        maxOddFilter: null,
        minValueFilter: null,
        minConfidenceFilter: null,
      }),
      updateSettings: (newSettings) =>
        set((state) => ({
          settings: { ...state.settings, ...newSettings }
        })),
    }),
    {
      name: 'sports-betting-storage',
      partialize: (state) => ({ settings: state.settings }),
    }
  )
)
