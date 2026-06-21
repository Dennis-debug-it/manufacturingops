import { Menu, PanelLeftClose, PanelLeftOpen, Settings2, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { settingsSections } from './routes'
import { UI_RESET_EVENT, cleanupBodyInteractivity } from '../../utils/appCleanup'

export function SettingsLayout() {
  const location = useLocation()
  const [collapsed, setCollapsed] = useState(false)
  const [mobilePanelOpen, setMobilePanelOpen] = useState(false)

  useEffect(() => {
    setMobilePanelOpen(false)
    cleanupBodyInteractivity()
  }, [location.key])

  useEffect(() => {
    const handleUiReset = () => setMobilePanelOpen(false)
    window.addEventListener(UI_RESET_EVENT, handleUiReset)
    return () => window.removeEventListener(UI_RESET_EVENT, handleUiReset)
  }, [])

  useEffect(() => {
    if (!mobilePanelOpen) {
      return
    }

    const { body } = document
    const previousOverflow = body.style.overflow
    body.style.overflow = 'hidden'

    return () => {
      body.style.overflow = previousOverflow
      cleanupBodyInteractivity()
    }
  }, [mobilePanelOpen])

  const activeSection = useMemo(
    () => settingsSections.find((section) => location.pathname === section.path) ?? settingsSections[0],
    [location.pathname],
  )
  const navCollapsed = collapsed && !mobilePanelOpen

  const navContent = (
    <div className="flex h-full min-h-0 flex-col">
      <div className={`flex items-start gap-3 border-b border-app px-4 py-4 ${navCollapsed ? 'justify-center' : ''}`}>
        <div className="icon-accent rounded-2xl p-3">
          <Settings2 className="h-5 w-5" />
        </div>
        {!navCollapsed ? (
          <div className="min-w-0">
            <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Settings</p>
            <h1 className="mt-2 text-xl font-semibold tracking-tight text-app">Tenant Controls</h1>
            <p className="mt-2 text-sm leading-6 text-muted">Select one settings area at a time for a more stable admin workflow.</p>
          </div>
        ) : null}
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3">
        <nav className="space-y-2">
          {settingsSections.map((section) => {
            const Icon = section.icon

            return (
              <NavLink
                key={section.path}
                to={section.path}
                end
                title={navCollapsed ? section.label : undefined}
                className={({ isActive }) =>
                  `settings-nav-link ${navCollapsed ? 'settings-nav-link-collapsed' : ''} ${isActive ? 'settings-nav-link-active' : ''}`
                }
              >
                <div className={`flex min-w-0 items-start gap-3 ${navCollapsed ? 'justify-center' : ''}`}>
                  <div className="mt-0.5 shrink-0">
                    <Icon className="h-4 w-4" />
                  </div>
                  {!navCollapsed ? (
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold">{section.label}</p>
                      <p className="mt-1 text-xs leading-5 text-muted">{section.description}</p>
                    </div>
                  ) : null}
                </div>
              </NavLink>
            )
          })}
        </nav>
      </div>
    </div>
  )

  return (
    <div className="min-w-0 space-y-3">
      <div className="surface-card flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Settings Workspace</p>
          <h2 className="mt-1.5 text-2xl font-semibold tracking-tight text-app">{activeSection.label}</h2>
          <p className="mt-1.5 text-sm text-muted">{activeSection.description}</p>
        </div>
        <div className="flex items-center gap-2">
          <button type="button" className="button-secondary md:hidden" onClick={() => setMobilePanelOpen(true)}>
            <Menu className="h-4 w-4" />
            Sections
          </button>
          <button type="button" className="button-secondary hidden md:inline-flex" onClick={() => setCollapsed((current) => !current)}>
            {collapsed ? <PanelLeftOpen className="h-4 w-4" /> : <PanelLeftClose className="h-4 w-4" />}
            {collapsed ? 'Expand panel' : 'Collapse panel'}
          </button>
        </div>
      </div>

      <div className="surface-card md:hidden">
        <div className="flex gap-2 overflow-x-auto pb-1">
          {settingsSections.map((section) => (
            <NavLink
              key={section.path}
              to={section.path}
              end
              className={({ isActive }) => `shrink-0 rounded-2xl px-3 py-2 text-sm font-semibold transition ${isActive ? 'bg-subtle-strong text-app border border-app' : 'panel-subtle text-muted'}`}
            >
              {section.shortLabel}
            </NavLink>
          ))}
        </div>
      </div>

      <div className="flex w-full min-w-0 gap-0 overflow-hidden rounded-[32px] border border-app bg-[var(--app-card)]">
        <div className={`hidden shrink-0 border-r border-app md:block ${collapsed ? 'w-20' : 'w-72'}`}>
          {navContent}
        </div>

        <div className="flex-1 min-w-0 p-4 sm:p-5">
          <Outlet />
        </div>
      </div>

      {mobilePanelOpen ? (
        <div data-ui-overlay="true" className="fixed inset-0 z-40 bg-black/50 p-4 md:hidden" onClick={() => setMobilePanelOpen(false)}>
          <div className="glass-panel flex h-full max-h-full w-full max-w-xs flex-col overflow-hidden rounded-[28px]" onClick={(event) => event.stopPropagation()}>
            <div className="flex shrink-0 items-center justify-between border-b border-app px-4 py-4">
              <div>
                <p className="text-sm font-semibold text-app">Settings Sections</p>
                <p className="mt-1 text-xs text-muted">Choose one section to edit.</p>
              </div>
              <button type="button" className="icon-button" onClick={() => setMobilePanelOpen(false)} aria-label="Close settings sections">
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="min-h-0 flex-1">
              {navContent}
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
