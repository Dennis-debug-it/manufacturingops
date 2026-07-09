import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App'
import { brand } from './config/brand'

document.title = brand.appName

const faviconLink = document.querySelector("link[rel='icon']") as HTMLLinkElement | null
if (faviconLink) {
  faviconLink.href = brand.favicon
}

createRoot(document.getElementById('root')!).render(
  <App />,
)
