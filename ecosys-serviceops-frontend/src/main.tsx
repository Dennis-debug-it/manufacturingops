import { createRoot } from 'react-dom/client'
import './styles/globals.css'
import { App } from './app/App'
import { installBootGuard } from './app/bootGuard'
import { brand } from './shared/config/brand'

document.title = brand.appName

const rootElement = document.getElementById('root') as HTMLElement
// False while a requested reset (?reset-app=1) clears the cached app and reloads.
if (installBootGuard(rootElement)) {
  createRoot(rootElement).render(<App />)
}
