import type { ReactNode } from 'react'
import { useEffect, useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { LogOut, Menu, X } from 'lucide-react'
import { Logo } from '../../shared/components/Logo'
import { Crystal } from '../../shared/components/Crystal'
import { cn } from '../../shared/utils/cn'
import { useAuth } from '../../shared/hooks/useAuth'
import { platformNavItems } from '../../shared/layout/navigation'

export interface PlatformShellProps {
  children: ReactNode
}

function PlatformNavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const location = useLocation()
  const { user } = useAuth()

  // Render only the sections this role may open. `platformNavItems` has always declared the
  // roles per section; ignoring them showed platform_sales links into Finance, Users, Licensing
  // and Settings. The matching refusal lives on the routes themselves (see PlatformApp) — a
  // hidden link is not access control.
  const visibleItems = platformNavItems.filter((item) => user !== null && item.roles.includes(user.role))

  return (
    <>
      {visibleItems.map((item) => {
        const Icon = item.icon
        const active = location.pathname === item.to || location.pathname.startsWith(`${item.to}/`)
        return (
          <NavLink
            key={item.to}
            to={item.to}
            onClick={onNavigate}
            className={cn(
              'relative flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition',
              active
                ? 'bg-white/10 text-white before:absolute before:left-0 before:top-1/2 before:h-5 before:w-[3px] before:-translate-y-1/2 before:rounded-full before:bg-[var(--app-lime)] before:content-[""]'
                : 'text-white/70 hover:bg-white/5 hover:text-white',
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
    <div className="relative min-h-screen bg-[var(--app-bg)] text-[var(--app-text)]">
      <div aria-hidden className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
        <Crystal variant="command-centre" className="absolute inset-0" />
      </div>
      <div className="mx-auto flex min-h-screen max-w-[1900px]">
        <aside className="hidden w-72 shrink-0 bg-gradient-to-b from-[#073F43] to-[#052D30] lg:flex lg:flex-col">
          <div className="border-b border-white/10 px-6 py-5">
            <div>
              <Logo tone="dark" className="h-8 w-auto" />
              <div className="mt-1 text-sm font-semibold text-white">Platform Owner</div>
            </div>
          </div>
          <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
            <PlatformNavLinks />
          </nav>
          <div className="border-t border-white/10 px-6 py-4">
            <div className="text-xs uppercase tracking-[0.2em] text-white/60">Signed in</div>
            <div className="mt-1 text-sm font-medium text-white">{user?.fullName ?? 'Platform user'}</div>
            <button
              type="button"
              className="mt-4 inline-flex h-10 items-center gap-2 rounded-lg border border-white/15 bg-white/5 px-3 text-sm font-medium text-white transition hover:bg-white/10"
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
              className="absolute inset-0 bg-[var(--app-primary)]/30 backdrop-blur-[2px]"
              onClick={() => setMobileNavOpen(false)}
            />
            <aside className="relative flex h-full w-72 max-w-[85vw] flex-col bg-gradient-to-b from-[#073F43] to-[#052D30] shadow-2xl">
              <div className="flex items-center justify-between border-b border-white/10 px-5 py-4">
                <div>
                  <Logo tone="dark" className="h-8 w-auto" />
                  <div className="mt-1 text-sm font-semibold text-white">Platform Owner</div>
                </div>
                <button type="button" onClick={() => setMobileNavOpen(false)} className="rounded-lg p-2 text-white/70 transition hover:bg-white/10 hover:text-white">
                  <X className="h-4 w-4" />
                </button>
              </div>
              <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
                <PlatformNavLinks onNavigate={() => setMobileNavOpen(false)} />
              </nav>
              <div className="border-t border-white/10 px-5 py-4">
                <div className="text-sm font-medium text-white">{user?.fullName ?? 'Platform user'}</div>
                <button
                  type="button"
                  className="mt-3 inline-flex h-10 items-center gap-2 rounded-lg border border-white/15 bg-white/5 px-3 text-sm font-medium text-white transition hover:bg-white/10"
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
          <header className="sticky top-0 z-20 border-b border-[var(--glass-border)] bg-[var(--glass-white-strong)] shadow-[var(--shadow-app)] backdrop-blur-[var(--glass-blur-md)]">
            <div className="flex h-16 items-center justify-between px-4 sm:px-6">
              <div className="flex items-center gap-3">
                <button
                  type="button"
                  aria-label="Open navigation"
                  onClick={() => setMobileNavOpen(true)}
                  className="inline-flex h-10 w-10 items-center justify-center rounded-xl border border-[var(--app-border)] bg-white text-slate-700 transition hover:bg-[var(--app-primary-soft)] lg:hidden"
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
                className="inline-flex h-10 items-center gap-2 rounded-lg border border-[var(--app-border)] bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-[var(--app-primary-soft)] lg:hidden"
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
