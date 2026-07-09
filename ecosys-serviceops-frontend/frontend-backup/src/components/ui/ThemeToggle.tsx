import { MoonStar, SunMedium } from 'lucide-react'
import { useThemeMode } from '../../context/ThemeContext'

export function ThemeToggle() {
  const { theme, toggleTheme } = useThemeMode()

  return (
    <button type="button" className="icon-button" onClick={toggleTheme} aria-label="Toggle theme">
      {theme === 'dark' ? <SunMedium className="h-4 w-4" /> : <MoonStar className="h-4 w-4" />}
    </button>
  )
}
