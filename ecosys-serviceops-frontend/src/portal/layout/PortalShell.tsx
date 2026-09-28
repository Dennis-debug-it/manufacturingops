import type { ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { cn } from '../../shared/utils/cn'
import { brand } from '../../shared/config/brand'
import { LayoutDashboard, ClipboardList, FileText, UserCircle2, LogOut, BookOpen, FileSignature, Boxes, CalendarClock, ScrollText, BarChart3, type LucideIcon } from 'lucide-react'
import { usePortalAuth } from '../auth/PortalAuthContext'
import { Crystal } from '../../shared/components/Crystal'
import { usePortalSettings } from '../api'
import { portalFeatures, type PortalFeature } from '../components/PortalFeatureGate'

interface PortalNavItem {
  label: string
  to: string
  icon: LucideIcon
  /** Hidden when the tenant has switched this section off in Settings → Client Portal. */
  feature?: PortalFeature
}

const portalNav: PortalNavItem[] = [
  { label: 'Dashboard', to: '/portal/dashboard', icon: LayoutDashboard },
  { label: 'Jobs', to: '/portal/requests', icon: ClipboardList },
  { label: 'Quotes', to: '/portal/quotes', icon: FileSignature },
  { label: 'Invoices', to: '/portal/invoices', icon: FileText, feature: portalFeatures.invoices },
  { label: 'Assets', to: '/portal/assets', icon: Boxes, feature: portalFeatures.assetRegister },
  { label: 'Maintenance', to: '/portal/maintenance', icon: CalendarClock },
  { label: 'Contracts', to: '/portal/contracts', icon: ScrollText, feature: portalFeatures.contracts },
  { label: 'Reports', to: '/portal/reports', icon: BarChart3 },
  { label: 'Help Center', to: '/portal/knowledge', icon: BookOpen },
  { label: 'Profile', to: '/portal/profile', icon: UserCircle2 },
]

export interface PortalShellProps {
  children: ReactNode
}

export function PortalShell({ children }: PortalShellProps) {
  const location = useLocation()
  const navigate = useNavigate()
  const { client, tenant, user, logout } = usePortalAuth()
  const { data: settings } = usePortalSettings()
  const navItems = portalNav.filter((item) => !item.feature || (settings ? item.feature(settings) : false))

  const handleLogout = async () => {
    await logout()
    navigate('/portal/login', { replace: true })
  }

  return (
    <div className="relative min-h-screen bg-[var(--app-bg)] text-[var(--app-text)]">
      <div aria-hidden className="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
        <Crystal variant="analytics" className="absolute inset-0" />
      </div>
      <header className="border-b border-[var(--glass-border)] bg-[var(--glass-white-strong)] px-4 py-4 shadow-[var(--shadow-app)] backdrop-blur-[var(--glass-blur-md)] sm:px-6">
        <div className="mx-auto flex max-w-7xl items-center justify-between">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{tenant?.companyName ?? brand.appName}</div>
            <div className="text-sm font-medium text-slate-900">{client?.clientName ?? 'Client Portal'}</div>
          </div>
          <div className="flex items-center gap-3">
            <div className="hidden text-right sm:block">
              <div className="text-xs font-medium text-slate-900">{user?.fullName}</div>
              <div className="text-xs text-slate-500">{user?.email}</div>
            </div>
            <button
              type="button"
              onClick={() => void handleLogout()}
              className="inline-flex h-9 items-center gap-2 rounded-lg border border-[var(--app-border)] px-3 text-xs font-medium text-slate-700 hover:bg-[var(--app-primary-soft)]"
            >
              <LogOut className="h-3.5 w-3.5" />
              Sign out
            </button>
          </div>
        </div>
      </header>
      <div className="mx-auto flex max-w-7xl gap-4 px-4 py-4 sm:px-6">
        <aside className="hidden w-64 shrink-0 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-3 backdrop-blur-[var(--glass-blur-md)] lg:block">
          <nav className="space-y-1">
            {navItems.map((item) => {
              const active = location.pathname === item.to || location.pathname.startsWith(`${item.to}/`)
              const Icon = item.icon
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  className={cn(
                    'relative flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition',
                    active
                      ? 'bg-[var(--app-primary)] text-white before:absolute before:left-0 before:top-1/2 before:h-5 before:w-[3px] before:-translate-y-1/2 before:rounded-full before:bg-[var(--app-lime)] before:content-[""]'
                      : 'text-slate-700 hover:bg-[var(--app-primary-soft)]',
                  )}
                >
                  <Icon className="h-4 w-4" />
                  {item.label}
                </Link>
              )
            })}
          </nav>
        </aside>
        <main className="min-w-0 flex-1 pb-24 lg:pb-6">{children}</main>
      </div>
      <nav className="fixed inset-x-0 bottom-0 z-30 border-t border-[var(--glass-border)] bg-[var(--glass-white-strong)] px-2 py-2 backdrop-blur-[var(--glass-blur-md)] lg:hidden">
        <div className="flex gap-1 overflow-x-auto">
          {navItems.map((item) => {
            const active = location.pathname === item.to || location.pathname.startsWith(`${item.to}/`)
            const Icon = item.icon
            return (
              <Link
                key={item.to}
                to={item.to}
                className={cn(
                  'flex min-w-[4.5rem] shrink-0 flex-col items-center gap-1 rounded-xl px-2 py-2 text-[11px] font-medium',
                  active ? 'bg-[var(--app-primary)] text-white' : 'text-slate-500',
                )}
              >
                <Icon className="h-4 w-4" />
                {item.label}
              </Link>
            )
          })}
        </div>
      </nav>
    </div>
  )
}
