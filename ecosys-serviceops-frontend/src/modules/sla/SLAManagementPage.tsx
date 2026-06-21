import { Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { slaService } from '../../services/slaService'
import { workOrderService } from '../../services/workOrderService'
import type { SlaDefinitionRecord, UpsertSlaDefinitionInput, WorkOrder } from '../../types/api'

const priorities: Array<'Critical' | 'High' | 'Medium' | 'Low'> = ['Critical', 'High', 'Medium', 'Low']

const emptyForm = (): UpsertSlaDefinitionInput => ({
  planName: '',
  description: '',
  isActive: true,
  rules: priorities.map((priority) => ({
    priority,
    responseTargetHours: priority === 'Critical' ? 1 : priority === 'High' ? 2 : priority === 'Medium' ? 4 : 8,
    resolutionTargetHours: priority === 'Critical' ? 4 : priority === 'High' ? 8 : priority === 'Medium' ? 24 : 48,
    businessHoursOnly: false,
  })),
})

export function SLAManagementPage() {
  const { pushToast } = useToast()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editing, setEditing] = useState<SlaDefinitionRecord | null>(null)
  const [form, setForm] = useState<UpsertSlaDefinitionInput>(emptyForm)

  const { data, loading, error, reload } = useAsyncData(
    async (signal) => {
      const [definitions, workOrders] = await Promise.all([
        slaService.list(signal),
        workOrderService.list(undefined, signal),
      ])
      return { definitions, workOrders }
    },
    { definitions: [] as SlaDefinitionRecord[], workOrders: [] as WorkOrder[] },
    [],
  )

  const breachedWorkOrders = useMemo(
    () => data.workOrders.filter((item) => item.slaResponseBreached || item.slaResolutionBreached),
    [data.workOrders],
  )

  function openEditor(definition?: SlaDefinitionRecord) {
    setEditing(definition ?? null)
    setForm(definition ? {
      planName: definition.planName,
      description: definition.description ?? '',
      isActive: definition.isActive,
      rules: priorities.map((priority) => {
        const current = definition.rules.find((item) => item.priority === priority)
        return {
          priority,
          responseTargetHours: current?.responseTargetHours ?? 0,
          resolutionTargetHours: current?.resolutionTargetHours ?? 0,
          businessHoursOnly: current?.businessHoursOnly ?? false,
        }
      }),
    } : emptyForm())
    setDrawerOpen(true)
  }

  async function save() {
    try {
      if (editing) {
        await slaService.update(editing.id, form)
        pushToast({ title: 'Response plan updated', description: 'The response plan was saved successfully.', tone: 'success' })
      } else {
        await slaService.create(form)
        pushToast({ title: 'Response plan created', description: 'The response plan is ready for client assignment.', tone: 'success' })
      }
      setDrawerOpen(false)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the response plan.', tone: 'danger' })
    }
  }

  async function remove(definition: SlaDefinitionRecord) {
    const confirmed = window.confirm(`Delete response plan "${definition.planName}"?`)
    if (!confirmed) return

    try {
      await slaService.remove(definition.id)
      pushToast({ title: 'Response plan deleted', description: 'The response plan was removed.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Delete failed', description: nextError instanceof Error ? nextError.message : 'Unable to delete the response plan.', tone: 'danger' })
    }
  }

  if (loading) return <LoadingState label="Loading response plans" />
  if (error) return <ErrorState message={error} onRetry={reload} />

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Response Plans"
        description="Manage SLA targets by priority, assign plans to clients, and monitor live breaches."
        actions={(
          <button type="button" className="button-primary" onClick={() => openEditor()}>
            <Plus className="h-4 w-4" />
            New response plan
          </button>
        )}
      />

      <section className="surface-card">
        <DataTable
          rows={data.definitions}
          rowKey={(row) => row.id}
          pageSize={8}
          columns={[
            {
              key: 'plan',
              header: 'Plan',
              cell: (row) => (
                <div>
                  <p className="font-semibold text-app">{row.planName}</p>
                  <p className="mt-1 text-xs text-muted">{row.description || 'No description set.'}</p>
                </div>
              ),
            },
            {
              key: 'coverage',
              header: 'Coverage',
              cell: (row) => <span>{row.rules.length} priorities</span>,
            },
            {
              key: 'critical',
              header: 'Critical',
              cell: (row) => {
                const rule = row.rules.find((item) => item.priority === 'Critical')
                return <span>{rule ? `${rule.responseTargetHours}h / ${rule.resolutionTargetHours}h` : 'Not set'}</span>
              },
            },
            {
              key: 'status',
              header: 'Status',
              cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge>,
            },
            {
              key: 'actions',
              header: 'Actions',
              cell: (row) => (
                <div className="flex gap-2">
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => openEditor(row)}>Edit</button>
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => void remove(row)}>
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              ),
            },
          ]}
        />
      </section>

      <section className="surface-card">
        <div className="mb-4 flex items-center justify-between gap-3">
          <div>
            <p className="text-lg font-semibold text-app">Live breach watchlist</p>
            <p className="text-sm text-muted">Open work orders with response or resolution breaches.</p>
          </div>
          <Badge tone={breachedWorkOrders.length > 0 ? 'danger' : 'success'}>{breachedWorkOrders.length} flagged</Badge>
        </div>
        <div className="space-y-3">
          {breachedWorkOrders.length === 0 ? <p className="text-sm text-muted">No breached work orders right now.</p> : null}
          {breachedWorkOrders.slice(0, 8).map((workOrder) => (
            <div key={workOrder.id} className="panel-subtle rounded-[24px] p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold text-app">{workOrder.workOrderNumber}</p>
                  <p className="mt-1 text-xs text-muted">{workOrder.clientName || 'No client'} | {workOrder.title}</p>
                </div>
                <Badge tone="danger">{workOrder.slaStatus}</Badge>
              </div>
            </div>
          ))}
        </div>
      </section>

      <Drawer open={drawerOpen} title={editing ? 'Edit response plan' : 'Create response plan'} description="Each priority row sets response and resolution targets for the selected response plan." onClose={() => setDrawerOpen(false)}>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Plan name">
            <input value={form.planName} onChange={(event) => setForm((current) => ({ ...current, planName: event.target.value }))} className="field-input" />
          </Field>
          <Field label="Status">
            <select value={form.isActive ? 'active' : 'inactive'} onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.value === 'active' }))} className="field-input">
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </Field>
        </div>

        <Field label="Description">
          <textarea value={form.description || ''} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[90px]" />
        </Field>

        <div className="mt-4 space-y-3">
          {form.rules.map((rule, index) => (
            <div key={rule.priority} className="panel-subtle rounded-[22px] p-4">
              <div className="mb-3 flex items-center justify-between gap-3">
                <p className="font-semibold text-app">{rule.priority}</p>
                <label className="inline-flex items-center gap-2 text-sm text-muted">
                  <input
                    type="checkbox"
                    checked={rule.businessHoursOnly}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        rules: current.rules.map((item, itemIndex) => itemIndex === index ? { ...item, businessHoursOnly: event.target.checked } : item),
                      }))
                    }
                  />
                  Business hours only
                </label>
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                <Field label="Response target (hours)">
                  <input
                    type="number"
                    min={0}
                    step={0.25}
                    value={rule.responseTargetHours}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        rules: current.rules.map((item, itemIndex) => itemIndex === index ? { ...item, responseTargetHours: Number(event.target.value) || 0 } : item),
                      }))
                    }
                    className="field-input"
                  />
                </Field>
                <Field label="Resolution target (hours)">
                  <input
                    type="number"
                    min={0}
                    step={0.25}
                    value={rule.resolutionTargetHours}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        rules: current.rules.map((item, itemIndex) => itemIndex === index ? { ...item, resolutionTargetHours: Number(event.target.value) || 0 } : item),
                      }))
                    }
                    className="field-input"
                  />
                </Field>
              </div>
            </div>
          ))}
        </div>

        <div className="mt-6 flex justify-end gap-3">
          <button type="button" className="button-secondary" onClick={() => setDrawerOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void save()}>Save plan</button>
        </div>
      </Drawer>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}
