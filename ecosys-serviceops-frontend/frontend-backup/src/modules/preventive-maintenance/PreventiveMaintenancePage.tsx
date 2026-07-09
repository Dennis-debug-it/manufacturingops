import { Plus, Wrench } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useShellContext } from '../../components/layout/AppShell'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { assetService } from '../../services/assetService'
import { pmTemplateService } from '../../services/pmTemplateService'
import { pmService } from '../../services/pmService'
import type { AssetRecord, PmTemplateRecord, PreventiveMaintenancePlan, UpsertPreventiveMaintenanceInput } from '../../types/api'
import { formatDateOnly } from '../../utils/date'

type PmPayload = {
  plans: PreventiveMaintenancePlan[]
  assets: AssetRecord[]
  templates: PmTemplateRecord[]
}

const emptyPayload: PmPayload = { plans: [], assets: [], templates: [] }

const pmFrequencyOptions = [
  { value: 1, label: 'Monthly' },
  { value: 3, label: 'Quarterly' },
  { value: 6, label: 'Semi-Annual' },
  { value: 12, label: 'Annual' },
] as const

export function PreventiveMaintenancePage() {
  const { selectedBranchId } = useShellContext()
  const { pushToast } = useToast()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editing, setEditing] = useState<PreventiveMaintenancePlan | null>(null)
  const [form, setForm] = useState<UpsertPreventiveMaintenanceInput>({
    assetId: '',
    serviceIntervalMonths: null,
    autoSchedule: true,
    pmTemplateId: '',
    lastPmDate: '',
    nextPmDate: '',
    status: 'Active',
  })

  const { data, loading, error, reload } = useAsyncData<PmPayload>(
    async (signal) => {
      const [plans, assets, templates] = await Promise.all([
        pmService.list(selectedBranchId, signal),
        assetService.list(selectedBranchId, signal),
        pmTemplateService.list(signal),
      ])
      return { plans, assets, templates }
    },
    emptyPayload,
    [selectedBranchId],
  )

  const selectedAsset = data.assets.find((asset) => asset.id === form.assetId)
  const activeTemplates = data.templates.filter(
    (template) => template.isActive && (template.workType || 'PreventiveMaintenance') === 'PreventiveMaintenance',
  )
  const suggestedTemplates = activeTemplates.filter((template) => {
    if (!selectedAsset) return false
    const categoryMatches = template.assetCategoryId && template.assetCategoryId === selectedAsset.assetCategoryId
    const text = `${template.category} ${template.name}`.toLowerCase()
    const assetHints = [selectedAsset.assetCategoryName, selectedAsset.assetType, selectedAsset.systemType]
      .filter((value): value is string => Boolean(value))
      .map((value) => value.toLowerCase())
    return Boolean(categoryMatches || assetHints.some((hint) => hint && text.includes(hint)))
  })
  const sortedTemplates = selectedAsset && suggestedTemplates.length > 0
    ? [...suggestedTemplates, ...activeTemplates.filter((template) => !suggestedTemplates.some((suggested) => suggested.id === template.id))]
    : activeTemplates

  function openEditor(plan?: PreventiveMaintenancePlan) {
    setEditing(plan ?? null)
    setForm({
      assetId: plan?.assetId ?? '',
      pmTemplateId: plan?.pmTemplateId ?? '',
      frequency: plan?.frequency ?? null,
      serviceIntervalMonths: plan?.serviceIntervalMonths ?? getPmFrequencyValue(plan?.frequency),
      autoSchedule: plan?.autoSchedule ?? true,
      lastPmDate: plan?.lastPmDate?.slice(0, 10) ?? '',
      nextPmDate: plan?.nextPmDate?.slice(0, 10) ?? '',
      status: plan?.status ?? 'Active',
    })
    setDrawerOpen(true)
  }

  async function savePlan() {
    if (!form.assetId) {
      pushToast({ title: 'Asset required', description: 'Select an asset before saving the PM plan.', tone: 'warning' })
      return
    }

    if (!isValidPmFrequency(form.serviceIntervalMonths)) {
      pushToast({ title: 'Service interval required', description: 'Select Monthly, Quarterly, Semi-Annual, or Annual.', tone: 'warning' })
      return
    }

    if (!form.pmTemplateId) {
      pushToast({ title: 'PM template required', description: 'Select a PM template before saving the PM plan.', tone: 'warning' })
      return
    }

    try {
      const payload: UpsertPreventiveMaintenanceInput = {
        ...form,
        frequency: getPmFrequencyLabel(form.serviceIntervalMonths),
        serviceIntervalMonths: Number(form.serviceIntervalMonths),
        nextPmDate: form.nextPmDate || calculateNextPmDate(form.lastPmDate, form.serviceIntervalMonths),
      }

      if (editing) {
        await pmService.update(editing.id, payload)
        pushToast({ title: 'Plan updated', description: 'Preventive maintenance plan saved successfully.', tone: 'success' })
      } else {
        await pmService.create(payload)
        pushToast({ title: 'Plan created', description: 'The plan is now active.', tone: 'success' })
      }
      setDrawerOpen(false)
      await reload()
    } catch (error) {
      pushToast({ title: 'Save failed', description: error instanceof Error ? error.message : 'Unable to save maintenance plan.', tone: 'danger' })
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Preventive Maintenance"
        title="Preventive maintenance plans"
        description="Schedule recurring preventive maintenance with standard service intervals."
        actions={
          <button type="button" className="button-primary" onClick={() => openEditor()}>
            <Plus className="h-4 w-4" />
            Add plan
          </button>
        }
      />

      <section className="surface-card">
        {loading ? <LoadingState label="Loading PM plans" /> : null}
        {!loading && error ? <ErrorState title="Unable to load PM plans" description={error} /> : null}
        {!loading && !error && data.plans.length === 0 ? (
          <EmptyState title="No PM plans yet" description="Create a preventive maintenance plan for an asset to begin scheduling." actionLabel="Refresh" onAction={() => void reload()} />
        ) : null}
        {!loading && !error && data.plans.length > 0 ? (
          <DataTable
            rows={data.plans}
            rowKey={(row) => row.id}
            pageSize={10}
            columns={[
              { key: 'asset', header: 'Asset', cell: (row) => <span>{row.assetName || 'Unknown asset'}</span> },
              { key: 'client', header: 'Client', cell: (row) => <span>{row.clientName || 'Unknown client'}</span> },
              { key: 'branch', header: 'Branch', cell: (row) => <span>{row.branchName || 'Global'}</span> },
              { key: 'frequency', header: 'Service Interval', cell: (row) => <span>{getPmFrequencyLabel(row.serviceIntervalMonths ?? row.frequency)}</span> },
              { key: 'template', header: 'PM Template', cell: (row) => <span>{row.pmTemplateName || 'Not assigned'}</span> },
              { key: 'next', header: 'Next PM', cell: (row) => <span>{formatDateOnly(row.nextPmDate || undefined)}</span> },
              { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Active' ? 'success' : 'warning'}>{row.status}</Badge> },
              {
                key: 'actions',
                header: 'Actions',
                cell: (row) => (
                  <div className="flex flex-wrap gap-2">
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => openEditor(row)}>Edit</button>
                    <button
                      type="button"
                      className="button-secondary px-3 py-2"
                      onClick={async () => {
                        try {
                          const workOrder = await pmService.generateWorkOrder(row.id)
                          pushToast({ title: 'Work order generated', description: `${workOrder.workOrderNumber} was created from the PM plan.`, tone: 'success' })
                        } catch (error) {
                          pushToast({ title: 'Generation failed', description: error instanceof Error ? error.message : 'Unable to generate work order.', tone: 'danger' })
                        }
                      }}
                    >
                      <Wrench className="h-4 w-4" />
                      Generate work order
                    </button>
                  </div>
                ),
              },
            ]}
          />
        ) : null}
      </section>

      <Drawer open={drawerOpen} title={editing ? 'Edit PM plan' : 'Add PM plan'} description="Set the service schedule and checklist for this asset." onClose={() => setDrawerOpen(false)}>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Asset">
            <select value={form.assetId} onChange={(event) => setForm((current) => ({ ...current, assetId: event.target.value }))} className="field-input">
              <option value="">Select asset</option>
              {data.assets.map((asset) => (
                <option key={asset.id} value={asset.id}>{asset.assetName}</option>
              ))}
            </select>
          </Field>
          <Field label="PM Template">
            <select value={form.pmTemplateId || ''} onChange={(event) => setForm((current) => ({ ...current, pmTemplateId: event.target.value }))} className="field-input">
              <option value="">{data.templates.length === 0 ? 'No PM templates found' : 'Select PM template'}</option>
              {sortedTemplates.map((template) => (
                <option key={template.id} value={template.id}>
                  {suggestedTemplates.some((suggested) => suggested.id === template.id) ? 'Suggested - ' : ''}{template.name}
                </option>
              ))}
            </select>
            {activeTemplates.length === 0 ? (
              <p className="text-xs text-amber-100">No PM templates found. Create a PM template before scheduling preventive maintenance.</p>
            ) : selectedAsset && suggestedTemplates.length > 0 ? (
              <p className="text-xs text-emerald-100">Suggested templates are matched from the asset category or type.</p>
            ) : null}
          </Field>
          <Field label="Service Interval">
            <select
              value={form.serviceIntervalMonths ?? ''}
              onChange={(event) => setForm((current) => ({ ...current, serviceIntervalMonths: event.target.value ? Number(event.target.value) : null }))}
              className="field-input"
            >
              <option value="">Select service frequency</option>
              {pmFrequencyOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </Field>
          <Field label="Last PM date"><input type="date" value={form.lastPmDate || ''} onChange={(event) => setForm((current) => ({ ...current, lastPmDate: event.target.value }))} className="field-input" max={new Date().toISOString().slice(0, 10)} /></Field>
          <Field label="Next PM date"><input type="date" value={form.nextPmDate || ''} onChange={(event) => setForm((current) => ({ ...current, nextPmDate: event.target.value }))} className="field-input" min={new Date().toISOString().slice(0, 10)} /></Field>
          <Field label="Status"><input value={form.status || ''} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value }))} className="field-input" /></Field>
        </div>
        <label className="panel-subtle mt-4 flex items-center justify-between rounded-2xl px-4 py-3">
          <span className="text-sm text-app">Auto schedule</span>
          <input type="checkbox" checked={form.autoSchedule} onChange={(event) => setForm((current) => ({ ...current, autoSchedule: event.target.checked }))} />
        </label>
        <div className="mt-6 flex justify-end gap-3">
          <button type="button" className="button-secondary" onClick={() => setDrawerOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void savePlan()}>Save plan</button>
        </div>
      </Drawer>
    </div>
  )
}

function isValidPmFrequency(value: number | string | null | undefined) {
  const numericValue = Number(value)
  return [1, 3, 6, 12].includes(numericValue)
}

function getPmFrequencyValue(value: number | string | null | undefined) {
  const numericValue = Number(value)
  if ([1, 3, 6, 12].includes(numericValue)) {
    return numericValue
  }

  const normalized = String(value ?? '').trim().toLowerCase()
  if (normalized === 'monthly') return 1
  if (normalized === 'quarterly') return 3
  if (normalized === 'semi-annual' || normalized === 'semi annual' || normalized === 'semiannual') return 6
  if (normalized === 'annual') return 12
  return null
}

function getPmFrequencyLabel(value: number | string | null | undefined) {
  const numericValue = Number(value)

  switch (numericValue) {
    case 1:
      return 'Monthly'
    case 3:
      return 'Quarterly'
    case 6:
      return 'Semi-Annual'
    case 12:
      return 'Annual'
    default:
      return 'Not set'
  }
}

function calculateNextPmDate(lastPmDate: string | null | undefined, serviceIntervalMonths: number | string | null | undefined) {
  if (!lastPmDate || !isValidPmFrequency(serviceIntervalMonths)) {
    return ''
  }

  const next = new Date(`${lastPmDate}T00:00:00`)
  next.setMonth(next.getMonth() + Number(serviceIntervalMonths))
  return next.toISOString().slice(0, 10)
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}
