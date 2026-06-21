import { memo, useEffect } from 'react'
import { ChevronRight, X } from 'lucide-react'
import { NavLink } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'
import { EcosysLogo, PoweredByEcosys } from '../brand'
import { useThemeMode } from '../../context/ThemeContext'
import { cleanupBodyInteractivity } from '../../utils/appCleanup'

export type ShellNavItem = {
  label: string
  path: string
  icon: LucideIcon
}

export const Sidebar = memo(function Sidebar({
  navItems,
  mode,
  tenantName,
  tenantLogoUrl,
  mobileOpen,
  onCloseMobile,
}: {
  navItems: ShellNavItem[]
  mode: 'tenant' | 'platform'
  tenantName: string
  tenantLogoUrl?: string | null
  mobileOpen: boolean
  onCloseMobile: () => void
}) {
  const { theme } = useThemeMode()
  const logoVariant = theme === 'light' ? 'lightPanel' : 'darkPanel'
  const poweredByTone = theme === 'light' ? 'light' : 'dark'

  useEffect(() => {
    if (!mobileOpen) {
      return
    }

    const { body } = document
    const previousOverflow = body.style.overflow
    body.style.overflow = 'hidden'

    return () => {
      body.style.overflow = previousOverflow
      cleanupBodyInteractivity()
    }
  }, [mobileOpen])

  const content = (
    <div className="flex h-full flex-col gap-6">
      {tenantLogoUrl ? (
        <div className="space-y-3">
          <div className="panel-subtle inline-flex max-w-full rounded-[24px] px-4 py-3">
            <img src={tenantLogoUrl} alt={`${tenantName} logo`} className="max-h-14 w-auto max-w-[180px] object-contain" />
          </div>
          <div>
            <p className="text-lg font-semibold text-app">{tenantName}</p>
            <p className="mt-1 text-xs font-semibold uppercase tracking-[0.2em] text-muted">Tenant Workspace</p>
          </div>
          <PoweredByEcosys minimal tone={poweredByTone} />
        </div>
      ) : (
        <EcosysLogo variant={logoVariant} size="lg" subtitle="ServiceOps Suite" />
      )}

      <nav className="space-y-2">
        {navItems.map((item) => {
          const Icon = item.icon

          return (
            <NavLink
              key={item.path}
              to={item.path}
              onClick={onCloseMobile}
              className={({ isActive }) =>
                `sidebar-link ${isActive ? 'sidebar-link-active' : ''}`
              }
            >
              <span className="flex items-center gap-3">
                <Icon className="h-4 w-4" />
                <span>{item.label}</span>
              </span>
              <ChevronRight className="h-4 w-4 opacity-60" />
            </NavLink>
          )
        })}
      </nav>
    </div>
  )

  return (
    <>
      <aside
        data-shell-mode={mode}
        data-testid="command-centre-sidebar"
        className="bg-sidebar-shell border-app hidden lg:flex lg:min-h-screen lg:w-64 lg:shrink-0 lg:border-r"
      >
        <div className="sticky top-0 h-screen w-full overflow-y-auto px-5 py-6">
          {content}
        </div>
      </aside>

      {mobileOpen ? (
        <div data-ui-overlay="true" className="bg-overlay fixed inset-0 z-40 p-3 lg:hidden" onClick={onCloseMobile}>
          <div className="glass-panel flex h-full max-h-full w-full max-w-[320px] flex-col overflow-hidden rounded-[30px] p-5" onClick={(event) => event.stopPropagation()}>
            <div className="mb-4 flex justify-end">
              <button type="button" className="icon-button" onClick={onCloseMobile}>
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto pr-1">
              {content}
            </div>
          </div>
        </div>
      ) : null}
    </>
  )
})
