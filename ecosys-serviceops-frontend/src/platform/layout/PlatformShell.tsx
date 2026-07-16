import type { ReactNode } from 'react'
import { useEffect, useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { LogOut, Menu, X } from 'lucide-react'
import { Logo } from '../../shared/components/Logo'
import { cn } from '../../shared/utils/cn'
import { useAuth } from '../../shared/hooks/useAuth'
import { platformNavItems } from '../../shared/layout/navigation'

export interface PlatformShellProps {
  children: ReactNode
}

function PlatformNavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const location = useLocation()

  return (
    <>
      {platformNavItems.map((item) => {
        const Icon = item.icon
        const active = location.pathname === item.to || location.pathname.startsWith(`${item.to}/`)
        return (
          <NavLink
            key={item.to}
            to={item.to}
            onClick={onNavigate}
            className={cn(
              'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition',
              active ? 'bg-slate-900 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
            )}
          >
            <Icon className="h-4 w-4" />
            {item.label}
          </NavLink>
        )
      })}
    </>
  )
}

export function PlatformShell({ children }: PlatformShellProps) {
  const { user, logout } = useAuth()
  const location = useLocation()
  const [mobileNavOpen, setMobileNavOpen] = useState(false)

  useEffect(() => {
    setMobileNavOpen(false)
  }, [location.pathname])

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <div className="mx-auto flex min-h-screen max-w-[1900px]">
        <aside className="hidden w-72 shrink-0 border-r border-slate-200 bg-white lg:flex lg:flex-col">
          <div className="border-b border-slate-200 px-6 py-5">
            <div>
              <Logo className="h-8 w-auto" />
              <div className="mt-1 text-sm font-semibold text-slate-900">Platform Owner</div>
            </div>
          </div>
          <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
            <PlatformNavLinks />
          </nav>
          <div className="border-t border-slate-200 px-6 py-4">
            <div className="text-xs uppercase tracking-[0.2em] text-slate-500">Signed in</div>
            <div className="mt-1 text-sm font-medium text-slate-900">{user?.fullName ?? 'Platform user'}</div>
            <button
              type="button"
              className="mt-4 inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
              onClick={logout}
            >
              <LogOut className="h-4 w-4" />
              Logout
            </button>
          </div>
        </aside>

        {mobileNavOpen ? (
          <div className="fixed inset-0 z-40 flex lg:hidden">
            <button
              type="button"
              aria-label="Close navigation"
              className="absolute inset-0 bg-slate-900/40"
              onClick={() => setMobileNavOpen(false)}
            />
            <aside className="relative flex h-full w-72 max-w-[85vw] flex-col border-r border-slate-200 bg-white shadow-2xl">
              <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
                <div>
                  <Logo className="h-8 w-auto" />
                  <div className="mt-1 text-sm font-semibold text-slate-900">Platform Owner</div>
                </div>
                <button type="button" onClick={() => setMobileNavOpen(false)} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
                  <X className="h-4 w-4" />
                </button>
              </div>
              <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
                <PlatformNavLinks onNavigate={() => setMobileNavOpen(false)} />
              </nav>
              <div className="border-t border-slate-200 px-5 py-4">
                <div className="text-sm font-medium text-slate-900">{user?.fullName ?? 'Platform user'}</div>
                <button
                  type="button"
                  className="mt-3 inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
                  onClick={logout}
                >
                  <LogOut className="h-4 w-4" />
                  Logout
                </button>
              </div>
            </aside>
          </div>
        ) : null}

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/90 backdrop-blur">
            <div className="flex h-16 items-center justify-between px-4 sm:px-6">
              <div className="flex items-center gap-3">
                <button
                  type="button"
                  aria-label="Open navigation"
                  onClick={() => setMobileNavOpen(true)}
                  className="inline-flex h-10 w-10 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-700 transition hover:bg-slate-50 lg:hidden"
                >
                  <Menu className="h-4 w-4" />
                </button>
                <div className="flex items-center gap-3">
                  <Logo variant="mark" className="h-8 w-8 shrink-0 lg:hidden" />
                  <div className="text-sm font-semibold text-slate-900">Command Centre</div>
                </div>
              </div>
              <button
                type="button"
                className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50 lg:hidden"
                onClick={logout}
              >
                <LogOut className="h-4 w-4" />
                Logout
              </button>
            </div>
          </header>

          <main className="min-w-0 flex-1 px-4 py-5 sm:px-6 lg:px-8">
            <div className="space-y-5">{children}</div>
          </main>
        </div>
      </div>
    </div>
  )
}
