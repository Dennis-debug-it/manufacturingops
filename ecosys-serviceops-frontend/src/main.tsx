import { createRoot } from 'react-dom/client'
import './styles/globals.css'
import { App } from './app/App'
import { brand } from './shared/config/brand'

document.title = brand.appName

const root = createRoot(document.getElementById('root') as HTMLElement)
root.render(<App />)
