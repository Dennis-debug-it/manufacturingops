import { memo, useEffect, useState } from 'react'
import { Bell, ChevronDown, LogOut, Menu, Search, ShieldCheck } from 'lucide-react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { EcosysIcon, EcosysLogo } from '../brand'
import { Badge } from '../ui/Badge'
import { ThemeToggle } from '../ui/ThemeToggle'
import { useThemeMode } from '../../context/ThemeContext'
import type { AppSession, Branch, NotificationItem, SearchItem } from '../../types/app'
import { UI_RESET_EVENT } from '../../utils/appCleanup'
import type { ShellNavItem } from './Sidebar'

export const Topbar = memo(function Topbar({
  mode,
  session,
  tenantName,
  tenantLogoUrl,
  navItems,
  branches,
  selectedBranchId,
  setSelectedBranchId,
  searchQuery,
  setSearchQuery,
  searchInputRef,
  searchResults,
  notifications,
  onSelectSearchResult,
  onMenuClick,
  onLogout,
}: {
  mode: 'tenant' | 'platform'
  session: AppSession
  tenantName: string
  tenantLogoUrl?: string | null
  navItems: ShellNavItem[]
  branches: Branch[]
  selectedBranchId: string
  setSelectedBranchId: (value: string) => void
  searchQuery: string
  setSearchQuery: (value: string) => void
  searchInputRef: React.RefObject<HTMLInputElement | null>
  searchResults: SearchItem[]
  notifications: NotificationItem[]
  onSelectSearchResult: (path: string) => void
  onMenuClick: () => void
  onLogout: () => void
}) {
  const location = useLocation()
  const { theme } = useThemeMode()
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [notificationsOpen, setNotificationsOpen] = useState(false)
  const logoVariant = theme === 'light' ? 'lightPanel' : 'darkPanel'

  useEffect(() => {
    setUserMenuOpen(false)
    setNotificationsOpen(false)
  }, [location.key])

  useEffect(() => {
    const handleUiReset = () => {
      setUserMenuOpen(false)
      setNotificationsOpen(false)
    }

    window.addEventListener(UI_RESET_EVENT, handleUiReset)
    return () => window.removeEventListener(UI_RESET_EVENT, handleUiReset)
  }, [])

  return (
    <header className="bg-topbar-shell border-app sticky top-0 z-20 w-full border-b backdrop-blur">
      <div className="mx-auto flex w-full max-w-[1600px] flex-wrap items-center gap-3 px-4 py-4 sm:px-6 lg:px-8">
        <div className="flex min-w-0 items-center gap-3">
          <button type="button" className="icon-button lg:hidden" onClick={onMenuClick} aria-label="Open navigation menu">
            <Menu className="h-5 w-5" />
          </button>
          {mode === 'platform' ? (
            <>
              <EcosysIcon className="sm:hidden" size={28} />
              <EcosysLogo className="hidden sm:flex" variant={logoVariant} size="sm" subtitle="Platform" />
            </>
          ) : tenantLogoUrl ? (
            <img src={tenantLogoUrl} alt={`${tenantName} logo`} className="hidden h-9 w-auto max-w-[120px] object-contain sm:block" />
          ) : (
            <>
              <EcosysIcon className="sm:hidden" size={28} />
              <EcosysLogo className="hidden sm:flex" variant={logoVariant} size="sm" subtitle="ServiceOps Suite" />
            </>
          )}
        </div>

        {mode === 'tenant' ? (
          <nav className="hidden min-w-0 items-center gap-1 overflow-x-auto rounded-full border border-app bg-[var(--app-subtle)] px-2 py-1 lg:flex">
            {navItems.slice(0, 5).map((item) => (
              <NavLink
                key={item.path}
                to={item.path}
                className={({ isActive }) =>
                  `rounded-full px-3 py-2 text-xs font-semibold transition ${
                    isActive
                      ? 'bg-[var(--app-primary)] text-white shadow-[0_8px_18px_rgba(0,52,43,0.18)]'
                      : 'text-muted hover:bg-white hover:text-app'
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
        ) : null}

        <div className="min-w-0 flex-1 basis-full lg:basis-auto">
          <div className="flex flex-wrap items-center gap-3">
            <div className="min-w-0 lg:hidden xl:block">
              <p className="truncate text-sm font-semibold text-app">{tenantName}</p>
              <p className="text-xs text-muted">{session.title}</p>
            </div>

            <div className="divider-app hidden h-7 w-px xl:block" />

            <label className="panel-subtle hidden min-w-0 flex-1 items-center gap-3 rounded-2xl px-4 py-3 xl:flex xl:max-w-md">
              <Search className="h-4 w-4 text-muted" />
              <input
                ref={searchInputRef}
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="Search records, clients, or assets"
                className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
              />
            </label>
          </div>
        </div>

        {branches.length > 0 ? (
          <select
            value={selectedBranchId}
            onChange={(event) => setSelectedBranchId(event.target.value)}
            className="field-input min-w-0 flex-1 sm:flex-none sm:w-auto sm:min-w-[220px] sm:max-w-[220px]"
          >
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </select>
        ) : (
          <Badge tone="info">Global Scope</Badge>
        )}

        <ThemeToggle />

        <div className="relative">
          <button
            type="button"
            className="icon-button"
            onClick={() => {
              setNotificationsOpen((current) => !current)
              setUserMenuOpen(false)
            }}
          >
            <Bell className="h-4 w-4" />
          </button>

          {notificationsOpen ? (
            <div className="glass-panel absolute right-0 top-[calc(100%+0.75rem)] z-30 w-80 max-w-[calc(100vw-2rem)] rounded-[26px] p-3 sm:w-[340px]">
              <div className="mb-3 flex items-center justify-between">
                <p className="text-sm font-semibold text-app">Notifications</p>
                <Badge tone="neutral">{notifications.length}</Badge>
              </div>
              <div className="space-y-2">
                {notifications.map((notification) => (
                  <div key={notification.id} className="panel-subtle rounded-2xl p-3">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-medium text-app">{notification.title}</p>
                      <Badge
                        tone={
                          notification.level === 'critical'
                            ? 'danger'
                            : notification.level === 'warning'
                              ? 'warning'
                              : 'info'
                        }
                      >
                        {notification.level}
                      </Badge>
                    </div>
                    <p className="mt-2 text-sm text-muted">{notification.detail}</p>
                    <p className="mt-2 text-xs text-muted">{notification.time}</p>
                  </div>
                ))}
              </div>
            </div>
          ) : null}
        </div>

        <div className="relative">
          <button
            type="button"
            className="panel-subtle hover-surface flex items-center gap-3 rounded-2xl px-3 py-2 text-left transition"
            data-testid="user-menu-trigger"
            onClick={() => {
              setUserMenuOpen((current) => !current)
              setNotificationsOpen(false)
            }}
          >
            <div className="avatar-accent flex h-10 w-10 items-center justify-center rounded-2xl text-sm font-semibold">
              {session.avatar}
            </div>
            <div className="hidden sm:block">
              <p className="text-sm font-medium text-app">{session.name}</p>
              <p className="text-xs text-muted">{session.role}</p>
            </div>
            <ChevronDown className="h-4 w-4 text-muted" />
          </button>

          {userMenuOpen ? (
            <div className="glass-panel absolute right-0 top-[calc(100%+0.75rem)] z-30 w-72 max-w-[calc(100vw-2rem)] rounded-[26px] p-3">
              <div className="panel-subtle rounded-2xl p-3">
                <div className="flex items-center gap-3">
                  <div className="avatar-accent flex h-11 w-11 items-center justify-center rounded-2xl text-sm font-semibold">
                    {session.avatar}
                  </div>
                  <div>
                    <p className="text-sm font-medium text-app">{session.name}</p>
                    <p className="text-xs text-muted">{session.email}</p>
                  </div>
                </div>
                <div className="mt-3 flex items-center gap-2">
                  <ShieldCheck className="text-accent h-4 w-4" />
                  <p className="text-xs uppercase tracking-[0.18em] text-muted">{session.role}</p>
                </div>
              </div>
              <Link
                to="/my-notifications"
                className="button-secondary mt-3 flex w-full items-center gap-2 justify-start"
                onClick={() => setUserMenuOpen(false)}
              >
                <Bell className="h-4 w-4" />
                Notification Preferences
              </Link>
              <button type="button" className="button-secondary mt-2 w-full justify-start" onClick={onLogout} data-testid="logout-button">
                <LogOut className="h-4 w-4" />
                Logout
              </button>
            </div>
          ) : null}
        </div>
      </div>

      <div className="mx-auto w-full max-w-[1600px] px-4 pb-4 sm:px-6 xl:hidden">
        <label className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-3">
          <Search className="h-4 w-4 text-muted" />
          <input
            ref={searchInputRef}
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
            placeholder="Search records, clients, or assets"
            className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
          />
        </label>
      </div>

      {searchQuery.trim() && searchResults.length > 0 ? (
        <div className="mx-auto w-full max-w-[1600px] px-4 pb-4 sm:px-6 lg:px-8">
          <div className="glass-panel max-h-[320px] overflow-y-auto rounded-[26px] p-2">
            {searchResults.map((result) => (
              <button
                key={result.id}
                type="button"
                className="hover-surface flex w-full items-center justify-between rounded-2xl px-3 py-3 text-left transition"
                onClick={() => onSelectSearchResult(result.path)}
              >
                <div>
                  <p className="text-sm font-medium text-app">{result.title}</p>
                  <p className="text-xs text-muted">{result.subtitle}</p>
                </div>
                <Badge tone="neutral">Open</Badge>
              </button>
            ))}
          </div>
        </div>
      ) : null}
    </header>
  )
})
