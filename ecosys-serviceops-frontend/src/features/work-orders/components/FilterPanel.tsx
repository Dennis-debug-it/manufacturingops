import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ChevronDown, Funnel, X } from 'lucide-react'
import { api } from '../../../shared/api/apiClient'
import { endpoints } from '../../../shared/api/endpoints'
import { cn } from '../../../shared/utils/cn'
import { useClientOptions, useTechnicianOptions, type SiteOption } from '../pickerApi'
import { KANBAN_STATUSES, slaStatusLabels, type SlaStatusKey } from '../listModel'
import type { WorkOrderListState } from '../hooks/useWorkOrderListState'

const PRIORITIES = ['Low', 'Medium', 'High', 'Critical']
const SLA_STATUSES: SlaStatusKey[] = ['on-track', 'at-risk', 'breached', 'no-sla']

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none transition focus:border-slate-900'
const labelClass = 'mb-1 block text-xs font-medium text-slate-500'

function useSiteFilterOptions(clientId: string) {
  return useQuery({
    queryKey: ['picker', 'sites-search', clientId],
    queryFn: () => api.get<SiteOption[]>(endpoints.sites.search, { clientId: clientId || undefined }),
    staleTime: 60_000,
  })
}

export interface FilterPanelProps {
  state: WorkOrderListState
  onChange: (patch: Partial<WorkOrderListState>) => void
  onClear: () => void
  activeFilterCount: number
}

export function FilterPanel({ state, onChange, onClear, activeFilterCount }: FilterPanelProps) {
  const [open, setOpen] = useState(false)
  const { data: clients } = useClientOptions()
  const { data: technicians } = useTechnicianOptions()
  const { data: sites } = useSiteFilterOptions(state.clientId)

  const chips: Array<{ key: keyof WorkOrderListState; label: string }> = []
  if (state.status) chips.push({ key: 'status', label: `Status: ${state.status}` })
  if (state.priority) chips.push({ key: 'priority', label: `Priority: ${state.priority}` })
  if (state.technicianId) {
    const name = technicians?.find((t) => t.id === state.technicianId)?.fullName
    chips.push({ key: 'technicianId', label: `Technician: ${name ?? '—'}` })
  }
  if (state.clientId) {
    const name = clients?.find((c) => c.id === state.clientId)?.clientName
    chips.push({ key: 'clientId', label: `Client: ${name ?? '—'}` })
  }
  if (state.siteId) {
    const name = sites?.find((s) => s.id === state.siteId)?.siteName
    chips.push({ key: 'siteId', label: `Site: ${name ?? '—'}` })
  }
  if (state.slaStatus) chips.push({ key: 'slaStatus', label: `SLA: ${slaStatusLabels[state.slaStatus as SlaStatusKey]}` })
  if (state.unassignedOnly) chips.push({ key: 'unassignedOnly', label: 'Unassigned only' })
  if (state.fromDate) chips.push({ key: 'fromDate', label: `From: ${state.fromDate}` })
  if (state.toDate) chips.push({ key: 'toDate', label: `To: ${state.toDate}` })

  const removeChip = (key: keyof WorkOrderListState) => {
    if (key === 'unassignedOnly') {
      onChange({ unassignedOnly: false })
    } else {
      onChange({ [key]: '' } as Partial<WorkOrderListState>)
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => setOpen((value) => !value)}
          className={cn(
            'inline-flex h-10 items-center gap-2 rounded-lg border px-3 text-sm font-medium transition',
            open ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
          )}
        >
          <Funnel className="h-4 w-4" />
          Filters
          {activeFilterCount > 0 ? (
            <span className={cn('inline-flex h-5 min-w-[1.25rem] items-center justify-center rounded-full px-1 text-[11px] font-semibold', open ? 'bg-white text-slate-900' : 'bg-slate-900 text-white')}>
              {activeFilterCount}
            </span>
          ) : null}
          <ChevronDown className={cn('h-3.5 w-3.5 transition-transform', open && 'rotate-180')} />
        </button>

        {chips.map((chip) => (
          <span key={chip.key} className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-50 py-1 pl-3 pr-1.5 text-xs font-medium text-slate-700">
            {chip.label}
            <button type="button" onClick={() => removeChip(chip.key)} className="rounded-full p-0.5 hover:bg-slate-200" aria-label={`Remove ${chip.label} filter`}>
              <X className="h-3 w-3" />
            </button>
          </span>
        ))}

        {activeFilterCount > 0 ? (
          <button type="button" onClick={onClear} className="text-xs font-medium text-slate-500 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
            Clear all
          </button>
        ) : null}
      </div>

      {open ? (
        <div className="grid grid-cols-1 gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label className={labelClass}>Status</label>
            <select className={fieldClass} value={state.status} onChange={(event) => onChange({ status: event.target.value })}>
              <option value="">All statuses</option>
              {KANBAN_STATUSES.map((status) => (
                <option key={status} value={status}>{status}</option>
              ))}
            </select>
          </div>

          <div>
            <label className={labelClass}>Priority</label>
            <select className={fieldClass} value={state.priority} onChange={(event) => onChange({ priority: event.target.value })}>
              <option value="">All priorities</option>
              {PRIORITIES.map((priority) => (
                <option key={priority} value={priority}>{priority}</option>
              ))}
            </select>
          </div>

          <div>
            <label className={labelClass}>Technician</label>
            <select className={fieldClass} value={state.technicianId} onChange={(event) => onChange({ technicianId: event.target.value })}>
              <option value="">All technicians</option>
              {(technicians ?? []).map((technician) => (
                <option key={technician.id} value={technician.id}>{technician.fullName}</option>
              ))}
            </select>
          </div>

          <div>
            <label className={labelClass}>Client</label>
            <select className={fieldClass} value={state.clientId} onChange={(event) => onChange({ clientId: event.target.value, siteId: '' })}>
              <option value="">All clients</option>
              {(clients ?? []).map((client) => (
                <option key={client.id} value={client.id}>{client.clientName}</option>
              ))}
            </select>
          </div>

          <div>
            <label className={labelClass}>Site</label>
            <select className={fieldClass} value={state.siteId} onChange={(event) => onChange({ siteId: event.target.value })}>
              <option value="">All sites</option>
              {(sites ?? []).map((site) => (
                <option key={site.id} value={site.id}>{site.siteName}</option>
              ))}
            </select>
          </div>

          <div>
            <label className={labelClass}>SLA status</label>
            <select className={fieldClass} value={state.slaStatus} onChange={(event) => onChange({ slaStatus: event.target.value })}>
              <option value="">Any SLA status</option>
              {SLA_STATUSES.map((status) => (
                <option key={status} value={status}>{slaStatusLabels[status]}</option>
              ))}
            </select>
          </div>

          <div>
            <label className={labelClass}>From date</label>
            <input type="date" className={fieldClass} value={state.fromDate} onChange={(event) => onChange({ fromDate: event.target.value })} />
          </div>

          <div>
            <label className={labelClass}>To date</label>
            <input type="date" className={fieldClass} value={state.toDate} onChange={(event) => onChange({ toDate: event.target.value })} />
          </div>

          <label className="flex items-center gap-2 pt-6 text-sm text-slate-700">
            <input
              type="checkbox"
              className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
              checked={state.unassignedOnly}
              onChange={(event) => onChange({ unassignedOnly: event.target.checked })}
            />
            Unassigned only
          </label>
        </div>
      ) : null}
    </div>
  )
}
