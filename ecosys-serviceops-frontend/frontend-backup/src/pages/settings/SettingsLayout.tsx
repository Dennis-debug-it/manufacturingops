import { Menu, Search, Settings2, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { settingsGroups, type SettingsGroup } from './enterpriseSettings'

function matchGroup(pathname: string) {
  return settingsGroups.find((group) => pathname === group.path || pathname.startsWith(`${group.path}/`)) ?? settingsGroups[0]
}

export function SettingsLayout() {
  const location = useLocation()
  const navigate = useNavigate()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [search, setSearch] = useState('')

  const activeGroup = useMemo(() => matchGroup(location.pathname), [location.pathname])
  const filteredGroups = useMemo(() => {
    const term = search.trim().toLowerCase()
    if (!term) return settingsGroups
    return settingsGroups.filter((group) => `${group.label} ${group.description}`.toLowerCase().includes(term))
  }, [search])

  function goTo(group: SettingsGroup) {
    navigate(group.path)
    setMobileOpen(false)
  }

  return (
    <div className="min-w-0 space-y-4">
      <header className="space-y-3 border-b border-[var(--app-divider)] pb-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="eyebrow-accent text-[11px] font-semibold uppercase tracking-[0.22em]">Settings Workspace</p>
            <h1 className="mt-1.5 text-2xl font-semibold tracking-tight text-app">{activeGroup.label}</h1>
            <p className="mt-1.5 max-w-3xl text-sm leading-6 text-muted">{activeGroup.description}</p>
          </div>
        </div>

        <div className="grid gap-3 lg:grid-cols-[1fr_auto]">
          <label className="flex items-center gap-3 rounded-[14px] border border-[var(--app-divider)] bg-[var(--app-surface)] px-3 py-2">
            <Search className="h-4 w-4 text-muted" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              className="w-full bg-transparent text-sm outline-none placeholder:text-muted"
              placeholder="Search settings"
            />
          </label>
          <button type="button" className="button-secondary lg:hidden" onClick={() => setMobileOpen(true)}>
            <Menu className="h-4 w-4" />
            Sections
          </button>
        </div>
      </header>

      <div className="grid min-h-[calc(100dvh-15rem)] gap-0 border border-[var(--app-divider)] bg-[var(--app-card)] lg:grid-cols-[18rem_minmax(0,1fr)]">
        <aside className="hidden border-r border-[var(--app-divider)] lg:block">
          <SettingsNav groups={filteredGroups} activePath={activeGroup.path} onSelect={goTo} />
        </aside>

        <main className="min-w-0 overflow-y-auto p-4 sm:p-5">
          <Outlet />
        </main>
      </div>

      {mobileOpen ? (
        <div className="fixed inset-0 z-50 bg-black/50 p-4 lg:hidden" onClick={() => setMobileOpen(false)}>
          <div className="glass-panel flex h-[calc(100dvh-2rem)] max-h-[calc(100dvh-2rem)] w-full max-w-sm flex-col overflow-hidden border border-[var(--app-divider)]" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-center justify-between border-b border-[var(--app-divider)] px-4 py-4">
              <div>
                <p className="text-sm font-semibold text-app">Settings sections</p>
                <p className="mt-1 text-xs text-muted">Choose an operational area.</p>
              </div>
              <button type="button" className="icon-button h-10 w-10" onClick={() => setMobileOpen(false)} aria-label="Close settings sections">
                <X className="h-4 w-4" />
              </button>
            </div>
            <SettingsNav groups={filteredGroups} activePath={activeGroup.path} onSelect={goTo} />
          </div>
        </div>
      ) : null}
    </div>
  )
}

function SettingsNav({
  groups,
  activePath,
  onSelect,
}: {
  groups: SettingsGroup[]
  activePath: string
  onSelect: (group: SettingsGroup) => void
}) {
  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="flex items-start gap-3 border-b border-[var(--app-divider)] px-4 py-4">
        <div className="icon-accent rounded-[14px] p-2.5">
          <Settings2 className="h-4 w-4" />
        </div>
        <div className="min-w-0">
          <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Control panel</p>
          <h2 className="mt-1.5 text-base font-semibold tracking-tight text-app">Settings shell</h2>
          <p className="mt-1 text-sm leading-6 text-muted">Operational groups, not card dashboards.</p>
        </div>
      </div>

      <nav className="min-h-0 flex-1 overflow-y-auto p-3">
        <div className="space-y-1">
          {groups.map((group) => {
            const active = activePath === group.path
            return (
              <button
                key={group.id}
                type="button"
                onClick={() => onSelect(group)}
                className={`settings-nav-link w-full text-left ${active ? 'settings-nav-link-active' : ''}`}
              >
                <div className="flex items-start gap-3">
                  <div className="icon-accent mt-0.5 rounded-[12px] p-2">
                    <group.icon className="h-4 w-4" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-app">{group.label}</p>
                    <p className="mt-1 text-xs leading-5 text-muted">{group.description}</p>
                  </div>
                </div>
              </button>
            )
          })}
        </div>
      </nav>
    </div>
  )
}
