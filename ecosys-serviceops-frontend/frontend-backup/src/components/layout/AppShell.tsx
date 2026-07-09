import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from 'react'
import { Link, Outlet, useLocation, useNavigate, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { usePersistentState } from '../../hooks/usePersistentState'
import { licenseService } from '../../services/licenseService'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import type { AppSession, Branch, NotificationItem, SearchItem } from '../../types/app'
import type { TenantLicenseSnapshot } from '../../types/api'
import type { ShellNavItem, ShellNavSection } from './Sidebar'
import { KEYBOARD_SHORTCUTS } from '../../utils/constants'
import { cleanupBodyInteractivity, dispatchUiReset } from '../../utils/appCleanup'
import { isPlatformRole } from '../../utils/roles'

export type ShellOutletContext = {
  selectedBranchId: string
  setSelectedBranchId: (value: string) => void
  branches: Branch[]
}

export function AppShell({
  mode,
  navItems,
  sidebarSections,
  session,
  tenantName,
  branches,
  notifications,
  searchItems,
}: {
  mode: 'tenant' | 'platform'
  navItems: ShellNavItem[]
  sidebarSections: ShellNavSection[]
  session: AppSession
  tenantName: string
  branches: Branch[]
  notifications: NotificationItem[]
  searchItems: SearchItem[]
}) {
  const navigate = useNavigate()
  const location = useLocation()
  const { logout } = useAuth()
  const [mobileOpen, setMobileOpen] = useState(false)
  const { value: sidebarCollapsed, setValue: setSidebarCollapsed } = usePersistentState('ecosys.workspace.sidebarCollapsed', false)
  const [selectedBranchId, setSelectedBranchId] = useState(session.branchId ?? branches[0]?.id ?? 'all')
  const [searchQuery, setSearchQuery] = useState('')
  const [license, setLicense] = useState<TenantLicenseSnapshot | null>(null)
  const deferredSearchQuery = useDeferredValue(searchQuery)
  const searchInputRef = useRef<HTMLInputElement | null>(null)
  const showGlobalTopbar = location.pathname === '/dashboard'
  const closeMobileMenu = useCallback(() => setMobileOpen(false), [])
  const openMobileMenu = useCallback(() => setMobileOpen(true), [])
  const normalizedSelectedBranchId = useMemo(() => {
    if (branches.some((branch) => branch.id === selectedBranchId)) {
      return selectedBranchId
    }

    return branches[0]?.id ?? 'all'
  }, [branches, selectedBranchId])

  useEffect(() => {
    closeMobileMenu()
    setSearchQuery('')
    cleanupBodyInteractivity()
    dispatchUiReset()
  }, [closeMobileMenu, location.key])

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (!showGlobalTopbar) {
        return
      }

      if (event.key === KEYBOARD_SHORTCUTS.search) {
        const target = event.target as HTMLElement | null
        if (target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA') {
          return
        }
        event.preventDefault()
        searchInputRef.current?.focus()
      }

      if (mode === 'tenant' && event.key.toLowerCase() === KEYBOARD_SHORTCUTS.newWorkOrder && !event.metaKey && !event.ctrlKey) {
        const target = event.target as HTMLElement | null
        if (target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA') {
          return
        }
        event.preventDefault()
        navigate('/work-orders')
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [mode, navigate, showGlobalTopbar])

  useEffect(() => {
    if (mode !== 'tenant' || isPlatformRole(session.role)) {
      return
    }

    const controller = new AbortController()
    void licenseService
      .getTenantLicense(controller.signal)
      .then(setLicense)
      .catch(() => setLicense(null))

    return () => controller.abort()
  }, [mode, session.role, session.tenantId])

  // Inject tenant brand colors as CSS custom properties on <html>
  useEffect(() => {
    if (mode !== 'tenant') return
    const root = document.documentElement
    if (session.primaryColor) {
      root.style.setProperty('--brand-primary', session.primaryColor)
    }
    if (session.secondaryColor) {
      root.style.setProperty('--brand-secondary', session.secondaryColor)
    }
    return () => {
      root.style.removeProperty('--brand-primary')
      root.style.removeProperty('--brand-secondary')
    }
  }, [mode, session.primaryColor, session.secondaryColor])

  // Inject tenant favicon dynamically
  useEffect(() => {
    if (mode !== 'tenant' || !session.faviconUrl) return
    let link = document.querySelector<HTMLLinkElement>("link[rel~='icon']")
    if (!link) {
      link = document.createElement('link')
      link.rel = 'icon'
      document.head.appendChild(link)
    }
    const previousHref = link.href
    link.href = session.faviconUrl
    return () => {
      if (link) link.href = previousHref
    }
  }, [mode, session.faviconUrl])

  const filteredSearchItems = useMemo(() => {
    const query = deferredSearchQuery.trim().toLowerCase()
    if (!query) {
      return []
    }

    return searchItems
      .filter((item) => item.roles.includes(session.role))
      .filter((item) => `${item.title} ${item.subtitle}`.toLowerCase().includes(query))
      .slice(0, 5)
  }, [deferredSearchQuery, searchItems, session.role])

  const breadcrumbs = useMemo(() => {
    const firstSegment = `/${location.pathname.split('/').filter(Boolean)[0] ?? ''}`
    const labels: Record<string, string> = {
      '/dashboard': 'Dashboard',
      '/work-orders': 'Work Orders',
      '/dispatch': 'Dispatch',
      '/clients': 'Clients',
      '/contracts': 'Contracts',
      '/sites': 'Sites & Assets',
      '/assets': 'Assets',
      '/materials': 'Materials',
      '/preventive-maintenance': 'Preventive Maintenance',
      '/templates': 'Templates',
      '/knowledge': 'Knowledge',
      '/finance': 'Finance',
      '/reports': 'Reports',
      '/team-management': 'Team Management',
      '/settings': 'Settings',
      '/platform': 'Platform',
    }
    const rootLabel = labels[firstSegment] ?? 'Workspace'

    const trail: Array<{ label: string; to?: string }> = [
      {
        label: 'Home',
        to: '/dashboard',
      },
      {
        label: rootLabel,
        to: firstSegment === '/dashboard' ? '/dashboard' : firstSegment || '/dashboard',
      },
    ]

    if (location.pathname.startsWith('/work-orders/') && location.pathname !== '/work-orders') {
      trail.push({ label: 'Detail' })
    } else if (location.pathname.startsWith('/contracts/') && location.pathname !== '/contracts') {
      trail.push({ label: 'Detail' })
    } else if (location.pathname.startsWith('/clients/') && location.pathname !== '/clients') {
      trail.push({ label: 'Detail' })
    }

    return trail
  }, [location.pathname])

  const handleSelectSearchResult = useCallback((path: string) => {
    setSearchQuery('')
    navigate(path)
  }, [navigate])

  const handleLogout = useCallback(() => {
    closeMobileMenu()
    setSearchQuery('')
    cleanupBodyInteractivity()
    dispatchUiReset()
    void logout().finally(() => {
      navigate('/login', { replace: true })
    })
  }, [closeMobileMenu, logout, navigate])

  const shellContext = useMemo<ShellOutletContext>(() => ({
    selectedBranchId: normalizedSelectedBranchId,
    setSelectedBranchId,
    branches,
  }), [branches, normalizedSelectedBranchId])

  return (
    <div className="min-h-dvh w-full overflow-x-hidden bg-app text-app">
      <div className="flex min-h-dvh w-full">
        <Sidebar
          sections={sidebarSections}
          mode={mode}
          tenantName={tenantName}
          tenantLogoUrl={mode === 'tenant' ? session.logoUrl ?? null : null}
          collapsed={sidebarCollapsed}
          mobileOpen={mobileOpen}
          onCloseMobile={closeMobileMenu}
          onToggleCollapsed={() => setSidebarCollapsed((current) => !current)}
        />

        <main className="flex-1 min-w-0 overflow-x-hidden">
          {showGlobalTopbar ? (
            <Topbar
              mode={mode}
              session={session}
              tenantName={tenantName}
              tenantLogoUrl={mode === 'tenant' ? session.logoUrl ?? null : null}
              navItems={navItems}
              branches={branches}
              selectedBranchId={normalizedSelectedBranchId}
              setSelectedBranchId={setSelectedBranchId}
              searchQuery={searchQuery}
              setSearchQuery={setSearchQuery}
              searchInputRef={searchInputRef}
              searchResults={filteredSearchItems}
              notifications={notifications}
              onSelectSearchResult={handleSelectSearchResult}
              onMenuClick={openMobileMenu}
              onToggleSidebar={() => setSidebarCollapsed((current) => !current)}
              onLogout={handleLogout}
            />
          ) : null}

          <div className="min-w-0 px-4 py-3 sm:px-6 lg:px-8">
            <div className="mx-auto flex w-full max-w-[1680px] min-w-0 flex-col gap-2.5">
              <div className="workspace-shell-strip flex flex-wrap items-center justify-between gap-2.5 rounded-[16px] border border-app px-3 py-2.5">
                <nav aria-label="Breadcrumbs" className="flex min-w-0 flex-wrap items-center gap-1.5 text-[11px] font-medium text-muted">
                  {breadcrumbs.map((crumb, index) => (
                    <div key={`${crumb.label}-${index}`} className="flex min-w-0 items-center gap-1.5">
                      {index > 0 ? <span className="text-[10px] opacity-60">/</span> : null}
                      {crumb.to ? (
                        <Link to={crumb.to} className="truncate transition hover:text-app">
                          {crumb.label}
                        </Link>
                      ) : (
                        <span className="truncate text-app">{crumb.label}</span>
                      )}
                    </div>
                  ))}
                </nav>
              </div>
              {license?.warningMessage ? (
                <div className={`rounded-2xl border px-3 py-2.5 text-sm ${license.isSuspended ? 'border-rose-400/30 bg-rose-500/10 text-rose-100' : 'border-amber-400/30 bg-amber-500/10 text-amber-100'}`}>
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span>{license.warningMessage}</span>
                    <span className="font-semibold text-app">{license.planName}</span>
                  </div>
                </div>
              ) : null}
              <Outlet context={shellContext} />
            </div>
          </div>
        </main>
      </div>
    </div>
  )
}

export function useShellContext() {
  const context = useOutletContext<ShellOutletContext | undefined>()

  if (!context) {
    throw new Error('useShellContext must be used within the AppShell outlet context.')
  }

  return context
}
