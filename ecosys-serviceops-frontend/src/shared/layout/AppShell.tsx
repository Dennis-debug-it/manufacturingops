import type { ReactNode } from 'react'
import { useAuth } from '../hooks/useAuth'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { BottomNav } from './BottomNav'
import { FirstLoginModal } from '../components/FirstLoginModal'
import { ImpersonationBanner } from '../components/ImpersonationBanner'

export interface AppShellProps {
  children?: ReactNode
}

export function AppShell({ children }: AppShellProps) {
  const { user } = useAuth()
  const online = useOnlineStatus()

  if (user?.role === 'super_admin' || user?.role === 'platform_sales') {
    return <>{children}</>
  }

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900">
      <ImpersonationBanner />
      <div className="flex min-h-0 flex-1">
        <Sidebar />
        <div className="flex min-h-screen min-w-0 flex-1 flex-col">
          <Topbar syncingLabel={online ? null : 'Working locally'} />
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
