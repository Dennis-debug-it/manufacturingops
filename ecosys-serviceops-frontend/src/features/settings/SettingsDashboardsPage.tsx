import { useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { ErrorState } from '../../shared/components/ErrorState'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { useDashboardAdminSettings, useSetRoleHiddenWidgets } from '../dashboard/roleDashboardApi'
import type { DashboardRoleSettings, DashboardWidgetInfo } from '../dashboard/roleDashboardTypes'

const roleLabels: Record<string, string> = {
  Admin: 'Admins',
  Manager: 'Managers',
  Dispatcher: 'Dispatchers',
  finance: 'Finance',
  MaterialsController: 'Materials controllers',
  Viewer: 'Viewers',
  Technician: 'Technicians',
}

/**
 * Admin: hide dashboard widgets per role. A hidden widget leaves that role's dashboards, can't be
 * added back by the user, and its figures are refused by the server. "Project managers" applies to
 * anyone running an open project, on top of their own role.
 */
export function SettingsDashboardsPage() {
  const { data, isLoading, isError, refetch } = useDashboardAdminSettings()
  const [role, setRole] = useState('Manager')

  if (isLoading) return <Spinner label="Loading dashboard settings" />
  if (isError || !data) {
    return <ErrorState title="Dashboard settings unavailable" description="The dashboard settings could not be loaded." onRetry={() => void refetch()} />
  }

  const selected = data.roles.find((x) => x.role === role) ?? data.roles[0]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Dashboards"
        subtitle="Choose which widgets each role can have. People arrange the rest themselves."
        breadcrumbs={[{ label: 'Settings', to: '/settings' }, { label: 'Dashboards' }]}
      />
      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Role">
        {data.roles.map((x) => (
          <button
            key={x.role}
            type="button"
            role="tab"
            aria-selected={x.role === selected?.role}
            onClick={() => setRole(x.role)}
            className={cn(
              'inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition',
              x.role === selected?.role ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {roleLabels[x.role] ?? x.role}
            {x.hiddenKeys.length > 0 ? <span className="ml-1.5 text-xs opacity-80">({x.hiddenKeys.length} hidden)</span> : null}
          </button>
        ))}
      </div>
      {selected ? <RoleWidgets key={selected.role} settings={selected} widgets={data.widgets} /> : null}
    </div>
  )
}

function RoleWidgets({ settings, widgets }: { settings: DashboardRoleSettings; widgets: DashboardWidgetInfo[] }) {
  const save = useSetRoleHiddenWidgets()
  const hidden = new Set(settings.hiddenKeys)
  const defaults = new Set(settings.defaultKeys)

  const toggle = async (key: string) => {
    const next = hidden.has(key) ? settings.hiddenKeys.filter((x) => x !== key) : [...settings.hiddenKeys, key]
    try {
      await save.mutateAsync({ role: settings.role, hiddenKeys: next })
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4">
      <p className="text-sm text-slate-600">
        Widgets still need the module and the person's permission. <span className="font-medium">Default</span> marks the widgets this role starts with.
      </p>
      <ul className="mt-2 divide-y divide-slate-100">
        {widgets.map((widget) => {
          const shown = !hidden.has(widget.key)
          return (
            <li key={widget.key} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <span className="min-w-0 flex-1">
                <span className="flex flex-wrap items-center gap-2 text-sm font-medium text-slate-800">
                  {widget.title}
                  {defaults.has(widget.key) ? <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600">Default</span> : null}
                </span>
                <span className="block text-xs text-slate-500">{widget.description}</span>
              </span>
              <button
                type="button"
                role="switch"
                aria-checked={shown}
                aria-label={`${widget.title}: ${shown ? 'shown' : 'hidden'}`}
                disabled={save.isPending}
                onClick={() => void toggle(widget.key)}
                className={cn('h-8 shrink-0 rounded-md px-3 text-xs font-medium ring-1 ring-inset disabled:opacity-60', shown ? 'bg-[var(--app-primary)] text-white ring-transparent' : 'bg-white text-slate-500 ring-slate-300')}
              >
                {shown ? 'Shown' : 'Hidden'}
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
