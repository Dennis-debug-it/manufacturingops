import { LogOut, WifiOff, Wrench } from 'lucide-react'
import { useEffect, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'

function useOnlineStatus() {
  const [online, setOnline] = useState(() => typeof navigator !== 'undefined' ? navigator.onLine : true)
  useEffect(() => {
    const handleOnline = () => setOnline(true)
    const handleOffline = () => setOnline(false)
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [])
  return online
}

export function TechLayout() {
  const { session, logout } = useAuth()
  const online = useOnlineStatus()

  return (
    <div className="min-h-dvh bg-app">
      {!online ? (
        <div className="flex items-center justify-center gap-2 bg-amber-500/90 px-4 py-2 text-sm font-semibold text-white">
          <WifiOff className="size-4 shrink-0" />
          You're offline — some actions may not be available until connectivity is restored.
        </div>
      ) : null}
      <header className="sticky top-0 z-40 border-b border-app bg-app/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-4 sm:px-6">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Technician mode</p>
            <h1 className="text-lg font-semibold text-app">{session?.tenantName || 'ServiceOps'}</h1>
          </div>
          <div className="flex items-center gap-2">
            <NavLink to="/tech" className="button-secondary">
              <Wrench className="size-4" />
              My jobs
            </NavLink>
            <button type="button" className="button-secondary" onClick={() => void logout()}>
              <LogOut className="size-4" />
              Logout
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-5 sm:px-6">
        <Outlet />
      </main>
    </div>
  )
}
