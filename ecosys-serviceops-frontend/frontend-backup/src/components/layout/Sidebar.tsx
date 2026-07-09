import { memo, useEffect } from 'react'
import { ChevronDown, X } from 'lucide-react'
import { NavLink } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'
import { EcosysIcon } from '../brand'
import { cleanupBodyInteractivity } from '../../utils/appCleanup'

export type ShellNavItem = {
  label: string
  path: string
  icon: LucideIcon
}

export type ShellNavSection = {
  label: string
  items: ShellNavItem[]
}

export const Sidebar = memo(function Sidebar({
  sections,
  mode,
  tenantName,
  tenantLogoUrl,
  collapsed,
  mobileOpen,
  onCloseMobile,
  onToggleCollapsed,
}: {
  sections: ShellNavSection[]
  mode: 'tenant' | 'platform'
  tenantName: string
  tenantLogoUrl?: string | null
  collapsed: boolean
  mobileOpen: boolean
  onCloseMobile: () => void
  onToggleCollapsed: () => void
}) {
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
    <div className="flex h-full flex-col gap-5">
      <div className={`space-y-4 ${collapsed ? 'text-center' : ''}`}>
        <div className={`inline-flex max-w-full ${collapsed ? 'justify-center' : ''}`}>
          {tenantLogoUrl ? (
            <div className={`panel-subtle inline-flex max-w-full rounded-[24px] px-4 py-3 ${collapsed ? 'justify-center' : ''}`}>
              <img src={tenantLogoUrl} alt={`${tenantName} logo`} className={`max-h-14 w-auto object-contain ${collapsed ? 'max-w-[44px]' : 'max-w-[180px]'}`} />
            </div>
          ) : (
            <div className="panel-subtle inline-flex h-14 w-14 items-center justify-center rounded-[24px]">
              <EcosysIcon size={34} />
            </div>
          )}
        </div>

        {!collapsed ? (
          <div className="space-y-1.5">
            <p className="text-[0.82rem] font-semibold uppercase tracking-[0.26em] text-app">ECOSYS</p>
            <p className="text-sm font-semibold leading-5 text-app">ServiceOps Suite</p>
            <p className="text-[0.72rem] font-medium leading-5 text-muted">Connect • Automate • Scale • Rely</p>
          </div>
        ) : null}

        {!collapsed && tenantLogoUrl ? (
          <div className="space-y-1">
            <p className="text-sm font-semibold text-app">{tenantName}</p>
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted">Tenant Workspace</p>
          </div>
        ) : null}
      </div>

      <nav aria-label="Sidebar navigation" className="space-y-5">
        {sections.map((section) => (
          <section key={section.label} className="space-y-2">
            {!collapsed ? (
              <p className="px-2 text-[0.68rem] font-semibold uppercase tracking-[0.24em] text-muted">{section.label}</p>
            ) : (
              <span className="sr-only">{section.label}</span>
            )}
            <div className="space-y-1.5">
              {section.items.map((item) => {
                const Icon = item.icon

                return (
                  <NavLink
                    key={item.path}
                    to={item.path}
                    onClick={onCloseMobile}
                    className={({ isActive }) =>
                      `sidebar-link ${collapsed ? 'sidebar-link-collapsed justify-center' : 'justify-start gap-3'} ${isActive ? 'sidebar-link-active' : ''}`
                    }
                    title={collapsed ? item.label : undefined}
                  >
                    <span className="flex min-w-0 items-center gap-3">
                      <Icon className="h-4 w-4 shrink-0" />
                      {!collapsed ? <span className="truncate">{item.label}</span> : null}
                    </span>
                  </NavLink>
                )
              })}
            </div>
          </section>
        ))}
      </nav>
    </div>
  )

  return (
    <>
      <aside
        data-shell-mode={mode}
        data-testid="command-centre-sidebar"
        className={`bg-sidebar-shell border-app hidden lg:flex lg:min-h-screen lg:shrink-0 lg:border-r ${collapsed ? 'lg:w-20' : 'lg:w-64'}`}
      >
        <div className={`sticky top-0 h-screen w-full overflow-y-auto py-5 ${collapsed ? 'px-3' : 'px-4'}`}>
          <div className={`mb-3 flex ${collapsed ? 'justify-center' : 'justify-end'}`}>
            <button type="button" className="icon-button h-9 w-9" onClick={onToggleCollapsed} aria-label={collapsed ? 'Expand navigation' : 'Collapse navigation'}>
              <ChevronDown className={`h-4 w-4 transition ${collapsed ? 'rotate-90' : '-rotate-90'}`} />
            </button>
          </div>
          {content}
        </div>
      </aside>

      {mobileOpen ? (
        <div data-ui-overlay="true" className="bg-overlay fixed inset-0 z-40 p-3 lg:hidden" onClick={onCloseMobile}>
          <div className="glass-panel flex h-full max-h-full w-full max-w-[320px] flex-col overflow-hidden rounded-[30px] p-4" onClick={(event) => event.stopPropagation()}>
            <div className="mb-3 flex justify-end">
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
