import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import './index.css'

// Add Google Fonts
const link = document.createElement('link')
link.href = 'https://fonts.googleapis.com/css2?family=DM+Sans:ital,opsz,wght@0,9..40,100..1000;1,9..40,100..1000&family=Playfair+Display:ital,wght@0,400..900;1,400..900&family=JetBrains+Mono:wght@100..800&display=swap'
link.rel = 'stylesheet'
document.head.appendChild(link)

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
