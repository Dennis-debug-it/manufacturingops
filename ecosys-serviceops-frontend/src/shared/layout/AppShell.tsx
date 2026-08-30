import type { ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { BottomNav } from './BottomNav'
import { FirstLoginModal } from '../components/FirstLoginModal'
import { ImpersonationBanner } from '../components/ImpersonationBanner'
import { Crystal } from '../components/Crystal'
import { crystalForScreen } from './crystalForScreen'

export interface AppShellProps {
  children?: ReactNode
}

export function AppShell({ children }: AppShellProps) {
  const { user } = useAuth()
  const location = useLocation()

  if (user?.role === 'super_admin' || user?.role === 'platform_sales') {
    return <>{children}</>
  }

  return (
    <div className="relative flex min-h-screen flex-col bg-[var(--app-bg)] text-[var(--app-text)]">
      <div aria-hidden className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
        <Crystal variant={crystalForScreen(location.pathname)} className="absolute inset-0" />
      </div>
      <ImpersonationBanner />
      <div className="flex min-h-0 flex-1">
        <Sidebar />
        <div className="flex min-h-screen min-w-0 flex-1 flex-col">
          <Topbar />
          <main className="min-w-0 flex-1 px-4 py-4 pb-24 sm:px-6 sm:py-5 lg:px-6">
            {children}
          </main>
        </div>
        <BottomNav />
      </div>
      <FirstLoginModal />
    </div>
  )
}
