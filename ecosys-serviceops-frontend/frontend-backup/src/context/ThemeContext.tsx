import { createContext, useContext, useLayoutEffect, useState } from 'react'
import type { ReactNode } from 'react'
import type { ThemeMode } from '../types/app'

const THEME_KEY = 'ecosys-serviceops-theme'

type ThemeContextValue = {
  theme: ThemeMode
  setTheme: (theme: ThemeMode) => void
  toggleTheme: () => void
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

function getInitialTheme(): ThemeMode {
  if (typeof window === 'undefined') return 'light'

  const stored = window.localStorage.getItem(THEME_KEY)
  if (stored === 'light' || stored === 'dark') {
    return stored
  }

  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<ThemeMode>(() => getInitialTheme())

  useLayoutEffect(() => {
    if (typeof document === 'undefined') return
    document.documentElement.dataset.theme = theme
    document.documentElement.classList.toggle('dark', theme === 'dark')
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(THEME_KEY, theme)
    }
  }, [theme])

  return (
    <ThemeContext.Provider
      value={{
        theme,
        setTheme,
        toggleTheme: () => {
          setTheme((current) => (current === 'dark' ? 'light' : 'dark'))
        },
      }}
    >
      {children}
    </ThemeContext.Provider>
  )
}

export function useThemeMode() {
  const context = useContext(ThemeContext)

  if (!context) {
    throw new Error('useThemeMode must be used inside ThemeProvider')
  }

  return context
}
