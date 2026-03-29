# Frontend - Sports Betting Dashboard

Dashboard React para o sistema de análise de apostas desportivas.

## Stack Tecnológico

- **Framework**: React 18 + Vite
- **Linguagem**: TypeScript
- **Estilização**: Tailwind CSS
- **UI Components**: Radix UI + shadcn/ui
- **Estado**: Zustand
- **Data Fetching**: TanStack Query (React Query)
- **Animações**: Framer Motion
- **Gráficos**: Recharts
- **Ícones**: Lucide React

## Design System

### Tema Visual
- **Estilo**: Luxury Dark Theme
- **Cores principais**: Navy escuro (#0a1929) + Dourado (#f59e0b)
- **Tipografia**: 
  - Display: Playfair Display (serif)
  - Body: DM Sans (sans-serif)
  - Mono: JetBrains Mono

### Cores
- Background: hsl(222, 47%, 6%)
- Primary (Gold): hsl(45, 93%, 47%)
- Card: hsl(222, 47%, 8%)
- Border: hsl(217, 33%, 17%)
- Success: emerald-400
- Warning: amber-400
- Danger: red-400

## Estrutura de Pastas

```
src/
├── components/          # Componentes reutilizáveis
│   ├── ui/             # Componentes base (shadcn)
│   ├── MatchCard.tsx
│   ├── OddBadge.tsx
│   ├── ConfidenceMeter.tsx
│   ├── BankrollChart.tsx
│   └── StakeModal.tsx
├── pages/              # Páginas da aplicação
│   ├── dashboard/
│   ├── history/
│   └── settings/
├── hooks/              # Custom hooks
│   └── useApi.ts       # TanStack Query hooks
├── stores/             # Zustand stores
│   └── appStore.ts
├── lib/                # Utilitários
│   └── utils.ts
├── types/              # TypeScript types
│   └── index.ts
├── App.tsx
└── main.tsx
```

## Páginas

### Dashboard (`/`)
- Lista de jogos do dia
- Filtros por desporto (Futebol/Ténis)
- Cards de jogos com recomendações
- Estatísticas rápidas (banca, P&L, win rate)

### Histórico (`/history`)
- Estatísticas de performance (P&L, ROI, Win Rate)
- Gráfico de evolução da banca
- Tabela de apostas recentes

### Configurações (`/settings`)
- Gestão de banca
- Thresholds de análise
- Informações do provider LLM

## Componentes Principais

### MatchCard
Card de jogo com:
- Informações das equipas/jogadores
- Badge de recomendação
- Odds com indicador de value
- Barra de confiança
- Botões de ação

### OddBadge
Display de odds com:
- Valor da odd
- Indicador de value (%) quando positivo
- Cores dinâmicas baseadas no value

### ConfidenceMeter
Barra visual de confiança:
- Escala 1-10
- Cores: verde (8+), amarelo (6-7), vermelho (<6)
- Animação suave

### BankrollChart
Gráfico de área com:
- Evolução temporal da banca
- Gradientes dourados
- Tooltip customizado

### StakeModal
Modal para registo de apostas:
- Input de stake
- Quick buttons (€5, €10, €25, €50)
- Validação de limites (max 5% banca)

## Scripts

```bash
# Instalar dependências
npm install

# Servidor de desenvolvimento
npm run dev

# Build de produção
npm run build

# Lint
npm run lint

# Testes
npm run test
```

## Variáveis de Ambiente

Criar ficheiro `.env`:

```env
VITE_API_URL=http://localhost:5000
VITE_ANALYSIS_URL=http://localhost:8000
```

## Proxy (Vite)

O `vite.config.ts` está configurado com proxy para:
- `/api` → C# Data Collector (porta 5000)
- `/analysis` → Python Analysis Engine (porta 8000)

## Design Decisions

1. **Luxury Dark Theme**: Escolhido para transmitir profissionalismo e sofisticação adequadas a um sistema de betting
2. **Gold Accents**: Dourado para destacar elementos importantes (odds, lucros, CTAs)
3. **Card-based Layout**: Cards com glassmorphism para modernidade e hierarquia visual
4. **Animations**: Framer Motion para transições suaves e feedback visual
5. **Mobile-first**: Navegação inferior em mobile, superior em desktop

## Próximos Passos

- Integração real com APIs
- Página de detalhe de jogo completa
- WebSocket para odds em tempo real
- Notificações push
