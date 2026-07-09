import { AlertTriangle, ClipboardList, FolderClock, LayoutPanelLeft, Plus, Search, SlidersHorizontal, Wrench } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useShellContext } from '../../components/layout/AppShell'
import { DataTableWrapper } from '../../components/ui/DataTableWrapper'
import { Pagination } from '../../components/ui/Pagination'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { usePagination } from '../../hooks/usePagination'
import { assetService } from '../../services/assetService'
import { clientService } from '../../services/clientService'
import { pmTemplateService } from '../../services/pmTemplateService'
import { settingsService } from '../../services/settingsService'
import { siteService } from '../../services/siteService'
import { workOrderService } from '../../services/workOrderService'
import type { AssetRecord, AssignmentGroupRecord, ClientRecord, PmTemplateRecord, SiteRecord, WorkOrder } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import {
  PriorityBadge,
  WorkTypeBadge,
  WorkflowStatusBadge,
  formatWorkTypeLabel,
  isOverdue,
  resolveWorkflowStatus,
} from './workOrderWorkflowUi'

type WorkOrdersPayload = {
  workOrders: WorkOrder[]
  clients: ClientRecord[]
  assignmentGroups: AssignmentGroupRecord[]
  templates: PmTemplateRecord[]
}

type WorkOrderDraft = {
  clientId: string
  siteId: string
  assetId: string
  assignmentGroupId: string
  title: string
  description: string
  priority: string
  dueDate: string
  workType: '' | 'NewInstallation' | 'PreventiveMaintenance' | 'CorrectiveMaintenance' | 'InspectionAssessment'
  pmTemplateId: string
  notifyClient: boolean
  clientContactEmail: string
}

type QueueFilters = {
  statusTab: 'All' | 'New' | 'Open' | 'Assigned' | 'Scheduled' | 'In Progress' | 'On Hold' | 'Completed' | 'Client Accepted' | 'Closed' | 'Cancelled' | 'Rejected'
  priority: string
  workType: string
  clientId: string
  site: string
  quickFilter: 'all' | 'overdue' | 'my-group' | 'assigned-to-me'
}

const emptyPayload: WorkOrdersPayload = {
  workOrders: [],
  clients: [],
  assignmentGroups: [],
  templates: [],
}

const emptyDraft: WorkOrderDraft = {
  clientId: '',
  siteId: '',
  assetId: '',
  assignmentGroupId: '',
  title: '',
  description: '',
  priority: 'Medium',
  dueDate: '',
  workType: '',
  pmTemplateId: '',
  notifyClient: true,
  clientContactEmail: '',
}

const defaultQueueFilters: QueueFilters = {
  statusTab: 'All',
  priority: 'All',
  workType: 'All',
  clientId: 'All',
  site: 'All',
  quickFilter: 'all',
}

export function WorkOrdersPage() {
  const navigate = useNavigate()
  const { selectedBranchId } = useShellContext()
  const { session } = useAuth()
  const { pushToast } = useToast()
  const [search, setSearch] = useState('')
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [draftFilters, setDraftFilters] = useState<QueueFilters>(defaultQueueFilters)
  const [appliedFilters, setAppliedFilters] = useState<QueueFilters>(defaultQueueFilters)
  const [editorOpen, setEditorOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [draft, setDraft] = useState<WorkOrderDraft>(emptyDraft)
  const [draftSites, setDraftSites] = useState<SiteRecord[]>([])
  const [draftAssets, setDraftAssets] = useState<AssetRecord[]>([])
  const [loadingSites, setLoadingSites] = useState(false)
  const [loadingAssets, setLoadingAssets] = useState(false)

  const { data, loading, error, reload } = useAsyncData<WorkOrdersPayload>(
    async (signal) => {
      const [workOrders, clients, assignmentGroups, templates] = await Promise.all([
        workOrderService.list(selectedBranchId, signal),
        clientService.list({ signal, status: 'active' }),
        settingsService.listAssignmentGroups(signal).catch(() => [] as AssignmentGroupRecord[]),
        pmTemplateService.list(signal).catch(() => [] as PmTemplateRecord[]),
      ])
      return { workOrders, clients, assignmentGroups, templates }
    },
    emptyPayload,
    [selectedBranchId],
  )

  const filteredRows = useMemo(() => {
    return data.workOrders
      .filter((item) => appliedFilters.statusTab === 'All' || resolveWorkflowStatus(item) === appliedFilters.statusTab)
      .filter((item) => appliedFilters.priority === 'All' || item.priority === appliedFilters.priority)
      .filter((item) => appliedFilters.workType === 'All' || (item.workType || '') === appliedFilters.workType)
      .filter((item) => appliedFilters.clientId === 'All' || item.clientId === appliedFilters.clientId)
      .filter((item) => appliedFilters.site === 'All' || (item.siteName || 'Unassigned') === appliedFilters.site)
      .filter((item) => {
        if (appliedFilters.quickFilter === 'overdue') return isOverdue(item)
        if (appliedFilters.quickFilter === 'my-group') {
          const groupHint = `${session?.department || session?.title || ''}`.toLowerCase()
          return groupHint.length > 0 && `${item.assignmentGroupName || ''}`.toLowerCase().includes(groupHint)
        }
        if (appliedFilters.quickFilter === 'assigned-to-me') {
          const name = `${session?.name || ''}`.toLowerCase()
          return `${item.assignedTechnicianName || ''} ${item.leadTechnicianName || ''} ${item.technicianAssignments?.map((assignment) => assignment.technicianName || '').join(' ') || ''}`.toLowerCase().includes(name)
        }
        return true
      })
      .filter((item) =>
        `${item.workOrderNumber} ${item.title} ${item.clientName || ''} ${item.siteName || ''} ${item.assetName || ''} ${item.assignedTechnicianName || ''} ${item.leadTechnicianName || ''}`
          .toLowerCase()
          .includes(search.toLowerCase()),
      )
  }, [appliedFilters, data.workOrders, search, session?.department, session?.name, session?.title])

  const summary = useMemo(() => {
    const all = data.workOrders
    return {
      total: all.length,
      newCount: all.filter((item) => {
        const status = resolveWorkflowStatus(item)
        return status === 'New' || status === 'Open'
      }).length,
      inProgress: all.filter((item) => resolveWorkflowStatus(item) === 'In Progress').length,
      overdue: all.filter((item) => isOverdue(item)).length,
    }
  }, [data.workOrders])

  const availableTemplates = useMemo(() => data.templates.filter((template) => template.isActive).sort((left, right) => left.name.localeCompare(right.name)), [data.templates])
  const rowsPerPage = 10

  useEffect(() => {
    if (!editorOpen) return
    setDraftSites([])
    setDraftAssets([])
    setDraft((current) => ({ ...current, siteId: '', assetId: '' }))
    if (!draft.clientId) return
    setLoadingSites(true)
    siteService.list(draft.clientId)
      .then(setDraftSites)
      .catch(() => {})
      .finally(() => setLoadingSites(false))
  }, [draft.clientId, editorOpen])

  useEffect(() => {
    if (!editorOpen) return
    setDraftAssets([])
    setDraft((current) => ({ ...current, assetId: '' }))
    if (!draft.siteId) return
    setLoadingAssets(true)
    assetService.list(null, undefined, { clientId: draft.clientId, siteId: draft.siteId })
      .then(setDraftAssets)
      .catch(() => {})
      .finally(() => setLoadingAssets(false))
  }, [draft.clientId, draft.siteId, editorOpen])

  function resetFilters() {
    setSearch('')
    setDraftFilters(defaultQueueFilters)
    setAppliedFilters(defaultQueueFilters)
    setFiltersOpen(false)
  }

  function applyFilters() {
    setAppliedFilters(draftFilters)
    setFiltersOpen(false)
  }

  async function refreshWorkspace() {
    await reload()
  }

  async function createWorkOrder() {
    if (!draft.clientId || !draft.title.trim() || !draft.workType) {
      pushToast({ title: 'Missing details', description: 'Client, title, and work type are required.', tone: 'warning' })
      return
    }
    if (draft.workType === 'PreventiveMaintenance' && !draft.pmTemplateId) {
      pushToast({ title: 'Checklist required', description: 'Select a checklist template for preventive maintenance work.', tone: 'warning' })
      return
    }

    setSaving(true)
    try {
      await workOrderService.create({
        clientId: draft.clientId,
        branchId: selectedBranchId === 'all' ? null : selectedBranchId,
        siteId: draft.siteId || null,
        assetId: draft.assetId || null,
        assignmentGroupId: draft.assignmentGroupId || null,
        assignmentType: draft.assignmentGroupId ? 'AssignmentGroup' : 'Unassigned',
        title: draft.title.trim(),
        description: draft.description.trim() || null,
        priority: draft.priority,
        dueDate: draft.dueDate || null,
        workType: draft.workType,
        isPreventiveMaintenance: draft.workType === 'PreventiveMaintenance',
        pmTemplateId: draft.pmTemplateId || null,
        notifyClient: draft.notifyClient,
        clientContactEmail: draft.clientContactEmail.trim() || null,
      })
      pushToast({ title: 'Work order created', description: 'The new job was added to the queue.', tone: 'success' })
      setEditorOpen(false)
      setDraft(emptyDraft)
      await refreshWorkspace()
    } catch (nextError) {
      pushToast({ title: 'Create failed', description: nextError instanceof Error ? nextError.message : 'Unable to create the work order.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  const pageActions = (
    <>
      <button type="button" className="button-secondary h-8 px-2.5 text-[11px] sm:h-9 sm:px-3 sm:text-xs" onClick={() => setFiltersOpen((current) => !current)}>
        <SlidersHorizontal className="h-3.5 w-3.5" />
        Filters
      </button>
      <button
        type="button"
        className="button-secondary h-8 px-2.5 text-[11px] sm:h-9 sm:px-3 sm:text-xs"
        onClick={() => {
          resetFilters()
        }}
      >
        Reset workspace
      </button>
      <button type="button" className="button-primary h-8 px-2.5 text-[11px] sm:h-9 sm:px-3 sm:text-xs" onClick={() => setEditorOpen(true)}>
        <Plus className="h-3.5 w-3.5" />
        New Work Order
      </button>
    </>
  )

  if (loading) {
    return <LoadingState label="Loading work orders" />
  }

  if (error) {
    return <ErrorState title="Unable to load work orders" description={error} />
  }

  return (
    <div className="w-full space-y-1.5 pb-2">
      <div className="space-y-1.5">
        <div className="flex flex-col gap-1.5 border-b border-[var(--app-divider)] pb-1.5 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="font-heading text-[1.25rem] font-semibold tracking-[-0.04em] text-app sm:text-[1.45rem]">Work Orders</h1>
              <span className="rounded-full border border-[var(--app-divider)] bg-[var(--app-surface-strong)] px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">
                {summary.total} total
              </span>
            </div>
            <p className="mt-0.5 text-[11px] text-muted sm:text-xs">
              {filteredRows.length} visible right now
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {pageActions}
          </div>
        </div>

        <div className="flex flex-wrap gap-1">
          <CompactStatChip label="Total" value={summary.total} icon={<ClipboardList className="size-3" />} />
          <CompactStatChip label="New / Open" value={summary.newCount} icon={<FolderClock className="size-3" />} />
          <CompactStatChip label="In Progress" value={summary.inProgress} icon={<Wrench className="size-3" />} />
          <CompactStatChip label="Overdue" value={summary.overdue} icon={<AlertTriangle className="size-3" />} />
          <CompactStatChip label="Filtered" value={filteredRows.length} icon={<LayoutPanelLeft className="size-3" />} />
        </div>
      </div>

      <div className="work-orders-toolbar-shell space-y-1.5 rounded-[12px] bg-[var(--app-card)] px-2 py-2">
        <div className="flex flex-col gap-1.5 xl:flex-row xl:items-center">
          <label className="flex h-8 min-w-0 flex-1 items-center gap-2 rounded-full border border-[var(--app-border-strong)] bg-[var(--app-surface-strong)] px-3">
            <Search className="size-3.5 shrink-0 text-muted" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              className="min-w-0 flex-1 bg-transparent text-sm text-app outline-none placeholder:text-muted"
              placeholder="Search work orders, clients, assets, or technicians"
            />
          </label>

          <div className="grid gap-1.5 sm:grid-cols-2 xl:flex xl:flex-wrap xl:items-center">
            <ToolbarSelect
              label="Status"
              value={draftFilters.statusTab}
              onChange={(value) => setDraftFilters((current) => ({ ...current, statusTab: value as QueueFilters['statusTab'] }))}
            >
              {['All', 'New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected'].map((tab) => (
                <option key={tab} value={tab}>{tab === 'All' ? 'All statuses' : tab}</option>
              ))}
            </ToolbarSelect>

            <ToolbarSelect
              label="Priority"
              value={draftFilters.priority}
              onChange={(value) => setDraftFilters((current) => ({ ...current, priority: value }))}
            >
              {['All', 'Low', 'Medium', 'High', 'Critical'].map((priority) => (
                <option key={priority} value={priority}>{priority}</option>
              ))}
            </ToolbarSelect>

            <ToolbarSelect
              label="Assigned"
              value={draftFilters.quickFilter}
              onChange={(value) => setDraftFilters((current) => ({ ...current, quickFilter: value as QueueFilters['quickFilter'] }))}
            >
              <option value="all">All work</option>
              <option value="assigned-to-me">Assigned to me</option>
              <option value="my-group">My group</option>
              <option value="overdue">Overdue</option>
            </ToolbarSelect>

            <div className="flex items-center gap-2 sm:col-span-2 xl:col-span-1">
              <button
                type="button"
                className="button-secondary h-8 px-3 py-1.5 text-[11px]"
                onClick={resetFilters}
                disabled={!search && draftFilters.statusTab === defaultQueueFilters.statusTab && draftFilters.priority === defaultQueueFilters.priority && draftFilters.workType === defaultQueueFilters.workType && draftFilters.clientId === defaultQueueFilters.clientId && draftFilters.site === defaultQueueFilters.site && draftFilters.quickFilter === defaultQueueFilters.quickFilter}
              >
                Clear
              </button>
              <button
                type="button"
                className="button-secondary h-8 px-3 py-1.5 text-[11px]"
                onClick={() => setFiltersOpen((current) => !current)}
              >
                {filtersOpen ? 'Hide filters' : 'More filters'}
              </button>
            </div>
          </div>
        </div>

        {filtersOpen ? (
          <div className="grid gap-1.5 sm:grid-cols-2 xl:grid-cols-4">
            <FilterField label="Work type">
              <select value={draftFilters.workType} onChange={(event) => setDraftFilters((current) => ({ ...current, workType: event.target.value }))} className="field-input h-8 px-3 py-1.5 text-sm">
                {['All', 'NewInstallation', 'PreventiveMaintenance', 'CorrectiveMaintenance', 'InspectionAssessment'].map((type) => (
                  <option key={type} value={type}>{type === 'All' ? 'All work types' : formatWorkTypeLabel(type)}</option>
                ))}
              </select>
            </FilterField>
            <FilterField label="Client">
              <select value={draftFilters.clientId} onChange={(event) => setDraftFilters((current) => ({ ...current, clientId: event.target.value }))} className="field-input h-8 px-3 py-1.5 text-sm">
                <option value="All">All clients</option>
                {data.clients.map((client) => <option key={client.id} value={client.id}>{client.clientName}</option>)}
              </select>
            </FilterField>
            <FilterField label="Site">
              <select value={draftFilters.site} onChange={(event) => setDraftFilters((current) => ({ ...current, site: event.target.value }))} className="field-input h-8 px-3 py-1.5 text-sm">
                <option value="All">All sites</option>
                {[...new Set(data.workOrders.map((item) => item.siteName).filter(Boolean))].map((site) => <option key={site} value={site as string}>{site}</option>)}
              </select>
            </FilterField>
            <div className="flex items-end gap-2 sm:col-span-2 xl:col-span-1">
              <button type="button" className="button-secondary h-8 px-3 py-1.5 text-[11px]" onClick={() => setDraftFilters(appliedFilters)}>Use current</button>
              <button type="button" className="button-primary h-8 px-3 py-1.5 text-[11px]" onClick={applyFilters}>Apply</button>
            </div>
          </div>
        ) : null}
      </div>

      {filteredRows.length === 0 ? (
        <EmptyState title="No matching records" description="Adjust the filters or create a new work order to start the workflow." />
      ) : (
      <QueuePanel
          rows={filteredRows}
          onOpenDetail={(workOrder) => navigate(`/work-orders/${workOrder.id}`)}
          rowsPerPage={rowsPerPage}
        />
      )}

      <Modal
        open={editorOpen}
        onClose={() => setEditorOpen(false)}
        title="New Work Order"
        description="Create a compact work order shell for dispatch and field execution."
        maxWidth="max-w-3xl"
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <FilterField label="Client" className="sm:col-span-2">
            <select value={draft.clientId} onChange={(event) => setDraft((current) => ({ ...current, clientId: event.target.value }))} className="field-input">
              <option value="">Select client</option>
              {data.clients.map((client) => <option key={client.id} value={client.id}>{client.clientName}</option>)}
            </select>
          </FilterField>
          {draft.clientId ? (
            <FilterField label="Site" className="sm:col-span-2">
              <select value={draft.siteId} onChange={(event) => setDraft((current) => ({ ...current, siteId: event.target.value, assetId: '' }))} className="field-input" disabled={loadingSites}>
                <option value="">{loadingSites ? 'Loading sites...' : 'Select site'}</option>
                {draftSites.map((site) => <option key={site.id} value={site.id}>{site.siteName}</option>)}
              </select>
            </FilterField>
          ) : null}
          {draft.clientId ? (
            <FilterField label="Asset" className="sm:col-span-2">
              <select value={draft.assetId} onChange={(event) => setDraft((current) => ({ ...current, assetId: event.target.value }))} className="field-input" disabled={!draft.siteId || loadingAssets}>
                <option value="">{!draft.siteId ? 'Select a site first' : loadingAssets ? 'Loading assets...' : 'Select asset'}</option>
                {draftAssets.map((asset) => <option key={asset.id} value={asset.id}>{asset.assetName}</option>)}
              </select>
            </FilterField>
          ) : null}
          <FilterField label="Title" className="sm:col-span-2">
            <input value={draft.title} onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))} className="field-input" placeholder="Describe the service request" />
          </FilterField>
          <FilterField label="Description" className="sm:col-span-2">
            <textarea value={draft.description} onChange={(event) => setDraft((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[120px]" placeholder="Add concise notes for dispatch and execution" />
          </FilterField>
          <FilterField label="Work type">
            <select value={draft.workType} onChange={(event) => setDraft((current) => ({ ...current, workType: event.target.value as WorkOrderDraft['workType'], pmTemplateId: '' }))} className="field-input">
              <option value="">Select work type</option>
              <option value="NewInstallation">New Installation</option>
              <option value="PreventiveMaintenance">Preventive Maintenance</option>
              <option value="CorrectiveMaintenance">Corrective Maintenance</option>
              <option value="InspectionAssessment">Inspection / Assessment</option>
            </select>
          </FilterField>
          <FilterField label="Checklist">
            <select value={draft.pmTemplateId} onChange={(event) => setDraft((current) => ({ ...current, pmTemplateId: event.target.value }))} className="field-input">
              <option value="">Optional checklist</option>
              {availableTemplates.map((template) => <option key={template.id} value={template.id}>{template.name}</option>)}
            </select>
          </FilterField>
          <FilterField label="Priority">
            <select value={draft.priority} onChange={(event) => setDraft((current) => ({ ...current, priority: event.target.value }))} className="field-input">
              {['Low', 'Medium', 'High', 'Critical'].map((priority) => <option key={priority} value={priority}>{priority}</option>)}
            </select>
          </FilterField>
          <FilterField label="Due date">
            <input type="date" value={draft.dueDate} onChange={(event) => setDraft((current) => ({ ...current, dueDate: event.target.value }))} className="field-input" />
          </FilterField>
          <FilterField label="Assignment group" className="sm:col-span-2">
            <select value={draft.assignmentGroupId} onChange={(event) => setDraft((current) => ({ ...current, assignmentGroupId: event.target.value }))} className="field-input">
              <option value="">Leave unassigned</option>
              {data.assignmentGroups.filter((group) => group.isActive).map((group) => <option key={group.id} value={group.id}>{group.name}</option>)}
            </select>
          </FilterField>
          <div className="sm:col-span-2 space-y-3 border-t border-subtle pt-2">
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm font-medium text-app">Notify client on updates</span>
              <button
                type="button"
                onClick={() => setDraft((current) => ({ ...current, notifyClient: !current.notifyClient }))}
                className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${draft.notifyClient ? 'bg-brand' : 'bg-muted/30'}`}
                aria-pressed={draft.notifyClient}
              >
                <span className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${draft.notifyClient ? 'translate-x-6' : 'translate-x-1'}`} />
              </button>
            </div>
            {draft.notifyClient ? (
              <input
                type="email"
                value={draft.clientContactEmail}
                onChange={(event) => setDraft((current) => ({ ...current, clientContactEmail: event.target.value }))}
                className="field-input"
                placeholder="Client contact email"
              />
            ) : null}
          </div>
        </div>
        <div className="mt-5 flex justify-end gap-3">
          <button type="button" className="button-secondary" onClick={() => setEditorOpen(false)}>Back</button>
          <button type="button" className="button-primary" onClick={() => void createWorkOrder()} disabled={saving}>
            {saving ? 'Creating...' : 'Create Work Order'}
          </button>
        </div>
      </Modal>

    </div>
  )
}

function QueuePanel({
  rows,
  onOpenDetail,
  rowsPerPage,
}: {
  rows: WorkOrder[]
  onOpenDetail: (workOrder: WorkOrder) => void
  rowsPerPage: number
}) {
  const pagination = usePagination(rows, rowsPerPage)

  return (
    <div className="work-orders-queue-shell w-full bg-transparent">
      <div className="overflow-x-auto overflow-y-hidden">
        <DataTableWrapper className="work-orders-open-table">
          <table className="w-full min-w-[1180px]">
            <thead className="table-head sticky top-0 z-10">
              <tr>
                {['Work Order', 'Client / Site', 'Asset', 'Type', 'Status', 'Priority', 'Assignment', 'Due'].map((header) => (
                  <th key={header} className="px-2 py-1 text-left text-[9px] font-semibold uppercase tracking-[0.16em] text-muted">{header}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {pagination.pageItems.map((item) => (
                <tr
                  key={item.id}
                  className="table-row cursor-pointer border-t border-[var(--app-divider)] transition"
                  onClick={() => onOpenDetail(item)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                      event.preventDefault()
                      onOpenDetail(item)
                    }
                  }}
                  tabIndex={0}
                  role="button"
                >
                  <td className="px-2 py-1.5 align-top">
                    <div className="flex items-start gap-2">
                      <span className="mt-1 h-2 w-2 rounded-full bg-[var(--app-border-strong)]" />
                      <div>
                        <button
                          type="button"
                          className="font-semibold text-app hover:underline"
                          onClick={(event) => {
                            event.stopPropagation()
                            onOpenDetail(item)
                          }}
                        >
                          {item.workOrderNumber}
                        </button>
                        <p className="mt-0.5 max-w-[240px] text-xs text-muted">{item.title}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-2 py-1.5 align-top">
                    <p className="text-xs text-app">{item.clientName || '-'}</p>
                    <p className="mt-0.5 text-[11px] text-muted">{item.siteName || 'No site linked'}</p>
                  </td>
                  <td className="px-2 py-1.5 text-sm text-app">{item.assetName || '-'}</td>
                  <td className="px-2 py-1.5"><WorkTypeBadge workType={item.workType} className="!border-0 !bg-[var(--app-subtle)] !shadow-none" /></td>
                  <td className="px-2 py-1.5 align-top">
                    <WorkflowStatusBadge workOrder={item} className="!border-0 !shadow-none" />
                  </td>
                  <td className="px-2 py-1.5 align-top">
                    <PriorityBadge priority={item.priority} className="!border-0 !shadow-none" />
                  </td>
                  <td className="px-2 py-1.5 align-top">
                    <p className="text-xs text-app">{item.assignmentGroupName || '-'}</p>
                    <p className="mt-0.5 text-[11px] text-muted">{item.leadTechnicianName || item.assignedTechnicianName || 'No technician assigned'}</p>
                  </td>
                  <td className="px-2 py-1.5 text-sm text-app">{item.dueDate ? formatDateOnly(item.dueDate) : '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </DataTableWrapper>
      </div>
      <Pagination
        page={pagination.page}
        pageCount={pagination.pageCount}
        totalItems={pagination.totalItems}
        startItem={pagination.startItem}
        endItem={pagination.endItem}
        canPrevious={pagination.canPrevious}
        canNext={pagination.canNext}
        onPrevious={pagination.previousPage}
        onNext={pagination.nextPage}
      />
    </div>
  )
}

function FilterField({ label, children, className = '' }: { label: string; children: React.ReactNode; className?: string }) {
  return (
    <label className={`space-y-1.5 ${className}`.trim()}>
      <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">{label}</span>
      {children}
    </label>
  )
}

function ToolbarSelect({
  label,
  value,
  onChange,
  children,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  children: React.ReactNode
}) {
  return (
    <label className="flex h-8 min-w-0 items-center gap-2 rounded-full border border-[var(--app-border-strong)] bg-[var(--app-surface-strong)] px-3 text-[11px] font-semibold uppercase tracking-[0.14em] text-muted">
      <span className="shrink-0">{label}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="min-w-0 flex-1 bg-transparent text-[12px] font-medium normal-case tracking-normal text-app outline-none"
      >
        {children}
      </select>
    </label>
  )
}

function CompactStatChip({
  label,
  value,
  icon,
}: {
  label: string
  value: string | number
  icon: React.ReactNode
}) {
  return (
    <div className="inline-flex items-center gap-2 rounded-full border border-[var(--app-divider)] bg-[var(--app-surface-strong)] px-2.5 py-1 text-[11px] text-app">
      <span className="icon-accent flex size-4 shrink-0 items-center justify-center rounded-full">{icon}</span>
      <span className="font-medium text-muted">{label}</span>
      <span className="font-semibold tabular-nums text-app">{value}</span>
    </div>
  )
}
