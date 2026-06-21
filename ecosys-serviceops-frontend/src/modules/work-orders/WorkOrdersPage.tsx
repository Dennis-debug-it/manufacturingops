import { AlertTriangle, ClipboardList, FolderClock, Plus, Search, ShieldAlert, SlidersHorizontal, Wrench } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { DataTableWrapper } from '../../components/ui/DataTableWrapper'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { SearchToolbar, SectionPanel } from '../../components/ui/Workspace'
import { useShellContext } from '../../components/layout/AppShell'
import { useAsyncData } from '../../hooks/useAsyncData'
import { assetService } from '../../services/assetService'
import { clientService } from '../../services/clientService'
import { pmTemplateService } from '../../services/pmTemplateService'
import { settingsService } from '../../services/settingsService'
import { siteService } from '../../services/siteService'
import { workOrderService } from '../../services/workOrderService'
import type { AssetRecord, AssignmentGroupRecord, ClientRecord, PmTemplateRecord, SiteRecord, WorkOrder } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import {
  CompactStatCard,
  ContextPrimaryButton,
  FilterChip,
  HoldReasonModal,
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
  statusTab: (typeof statusTabs)[number]
  priority: string
  workType: string
  clientId: string
  site: string
  dateFrom: string
  dateTo: string
  quickFilter: 'all' | 'overdue' | 'my-group' | 'assigned-to-me'
}

const statusTabs = ['All', 'New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected'] as const

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
  dateFrom: '',
  dateTo: '',
  quickFilter: 'all',
}

export function WorkOrdersPage() {
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
  const [holdTarget, setHoldTarget] = useState<WorkOrder | null>(null)
  const [holdReason, setHoldReason] = useState('')
  const [holdNote, setHoldNote] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
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

  const sites = useMemo(
    () => Array.from(new Set(data.workOrders.map((item) => item.siteName).filter((item): item is string => Boolean(item)))).sort(),
    [data.workOrders],
  )

  const filteredRows = useMemo(() => {
    return data.workOrders
      .filter((item) => appliedFilters.statusTab === 'All' || resolveWorkflowStatus(item) === appliedFilters.statusTab)
      .filter((item) => appliedFilters.priority === 'All' || item.priority === appliedFilters.priority)
      .filter((item) => appliedFilters.workType === 'All' || (item.workType || '') === appliedFilters.workType)
      .filter((item) => appliedFilters.clientId === 'All' || item.clientId === appliedFilters.clientId)
      .filter((item) => appliedFilters.site === 'All' || (item.siteName || 'Unassigned') === appliedFilters.site)
      .filter((item) => !appliedFilters.dateFrom || (item.dueDate ? new Date(item.dueDate) >= new Date(appliedFilters.dateFrom) : false))
      .filter((item) => !appliedFilters.dateTo || (item.dueDate ? new Date(item.dueDate) <= new Date(appliedFilters.dateTo) : false))
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
        `${item.workOrderNumber} ${item.clientName || ''} ${item.siteName || ''} ${item.assetName || ''} ${item.assignedTechnicianName || ''} ${item.leadTechnicianName || ''}`
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
      completed: all.filter((item) => resolveWorkflowStatus(item) === 'Completed').length,
    }
  }, [data.workOrders])

  const availableTemplates = useMemo(
    () => data.templates
      .filter((template) => template.isActive)
      .filter((template) => {
        if (!draft.workType) {
          return true
        }

        // TODO: When checklist templates support installation and inspection categories,
        // filter those work types directly instead of using the shared non-PM bucket.
        if (draft.workType === 'PreventiveMaintenance') {
          return (template.workType || 'PreventiveMaintenance') === 'PreventiveMaintenance'
        }

        return (template.workType || 'PreventiveMaintenance') !== 'PreventiveMaintenance'
      })
      .sort((left, right) => left.name.localeCompare(right.name)),
    [data.templates, draft.workType],
  )

  const selectedTemplate = useMemo(
    () => data.templates.find((template) => template.id === draft.pmTemplateId) ?? null,
    [data.templates, draft.pmTemplateId],
  )

  const hasAppliedFilters = useMemo(
    () => JSON.stringify(appliedFilters) !== JSON.stringify(defaultQueueFilters),
    [appliedFilters],
  )

  const appliedFilterLabels = useMemo(() => {
    const labels: string[] = []
    if (appliedFilters.statusTab !== 'All') labels.push(`Status: ${appliedFilters.statusTab}`)
    if (appliedFilters.priority !== 'All') labels.push(`Priority: ${appliedFilters.priority}`)
    if (appliedFilters.workType !== 'All') labels.push(`Type: ${formatWorkTypeLabel(appliedFilters.workType)}`)
    if (appliedFilters.clientId !== 'All') {
      const client = data.clients.find((item) => item.id === appliedFilters.clientId)
      labels.push(`Client: ${client?.clientName || 'Selected client'}`)
    }
    if (appliedFilters.site !== 'All') labels.push(`Site: ${appliedFilters.site}`)
    if (appliedFilters.quickFilter !== 'all') {
      labels.push(`Focus: ${appliedFilters.quickFilter === 'assigned-to-me' ? 'Assigned to Me' : appliedFilters.quickFilter === 'my-group' ? 'My Group' : 'Overdue'}`)
    }
    if (appliedFilters.dateFrom) labels.push(`From: ${appliedFilters.dateFrom}`)
    if (appliedFilters.dateTo) labels.push(`To: ${appliedFilters.dateTo}`)
    return labels
  }, [appliedFilters, data.clients])

  useEffect(() => {
    if (!editorOpen) return
    setDraftSites([])
    setDraftAssets([])
    setDraft(d => ({ ...d, siteId: '', assetId: '' }))
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
    setDraft(d => ({ ...d, assetId: '' }))
    if (!draft.siteId) return
    setLoadingAssets(true)
    assetService.list(null, undefined, { clientId: draft.clientId, siteId: draft.siteId })
      .then(setDraftAssets)
      .catch(() => {})
      .finally(() => setLoadingAssets(false))
  }, [draft.clientId, draft.siteId, editorOpen])

  async function runRowAction(workOrder: WorkOrder, action: string) {
    if (action === 'hold') {
      setHoldTarget(workOrder)
      setHoldReason('')
      setHoldNote('')
      return
    }

    setBusyId(workOrder.id)
    try {
      if (action === 'start') {
        await workOrderService.start(workOrder.id)
      } else if (action === 'resume') {
        await workOrderService.resume(workOrder.id)
      } else if (action === 'complete') {
        await workOrderService.complete(workOrder.id, {
          workDoneNotes: workOrder.workDoneNotes || 'Completed from work order list.',
          reportSummary: workOrder.workDoneNotes || 'Completed from work order list.',
          technicianId: workOrder.leadTechnicianId || workOrder.assignedTechnicianId || null,
        })
      }

      pushToast({ title: 'Work order updated', description: `${workOrder.workOrderNumber} moved forward in the workflow.`, tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Action failed', description: nextError instanceof Error ? nextError.message : 'Unable to update the work order.', tone: 'danger' })
    } finally {
      setBusyId(null)
    }
  }

  async function submitHold() {
    if (!holdTarget || !holdReason) return
    setBusyId(holdTarget.id)
    try {
      await workOrderService.pause(holdTarget.id, {
        holdReason,
        note: holdNote.trim() || null,
      })
      pushToast({ title: 'Work order placed on hold', description: `${holdTarget.workOrderNumber} is now on hold.`, tone: 'success' })
      setHoldTarget(null)
      setHoldReason('')
      setHoldNote('')
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Hold failed', description: nextError instanceof Error ? nextError.message : 'Unable to place the work order on hold.', tone: 'danger' })
    } finally {
      setBusyId(null)
    }
  }

  async function createWorkOrder() {
    if (!draft.clientId || !draft.title.trim()) {
      pushToast({ title: 'Missing details', description: 'Client and title are required before creating a work order.', tone: 'warning' })
      return
    }
    if (!draft.workType) {
      pushToast({ title: 'Work type required', description: 'Choose the kind of job before creating the work order.', tone: 'warning' })
      return
    }
    if (draft.workType === 'PreventiveMaintenance' && !draft.pmTemplateId) {
      pushToast({ title: 'Checklist required', description: 'Select a preventive maintenance checklist before creating the job.', tone: 'warning' })
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

      if (draft.assignmentGroupId) {
        pushToast({ title: 'Work order created', description: 'The new job was added to the queue and routed to the selected assignment group.', tone: 'success' })
      } else {
        pushToast({ title: 'Work order created', description: 'The new job was added to the queue.', tone: 'success' })
      }

      setEditorOpen(false)
      setDraft(emptyDraft)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Create failed', description: nextError instanceof Error ? nextError.message : 'Unable to create the work order.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

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

  if (loading) {
    return <LoadingState label="Loading work orders" />
  }

  if (error) {
    return <ErrorState title="Unable to load work orders" description={error} />
  }

  return (
    <div className="space-y-3 pb-6">
      <PageHeader
        eyebrow="Operations"
        title="Work Orders"
        description="Dispatch, assign, and progress service work from one queue."
        meta={[
          { label: 'In view', value: `${filteredRows.length} records` },
          { label: 'Branch scope', value: selectedBranchId === 'all' ? 'All branches' : 'Current branch' },
        ]}
        primaryAction={(
          <button type="button" className="button-primary" onClick={() => setEditorOpen(true)}>
            <Plus className="size-4" />
            New Work Order
          </button>
        )}
      />

      <div className="grid gap-2.5 md:grid-cols-2 xl:grid-cols-5">
        <CompactStatCard label="Total" value={summary.total} helper="All work orders in the queue" icon={<ClipboardList className="size-5" />} />
        <CompactStatCard label="New" value={summary.newCount} helper="Waiting for assignment" icon={<FolderClock className="size-5" />} />
        <CompactStatCard label="In Progress" value={summary.inProgress} helper="Currently active in the field" icon={<Wrench className="size-5" />} />
        <CompactStatCard label="Overdue" value={summary.overdue} helper="Require attention today" icon={<AlertTriangle className="size-5" />} />
        <CompactStatCard label="Completed" value={summary.completed} helper="Finished and awaiting closeout" icon={<ShieldAlert className="size-5" />} />
      </div>

      <SearchToolbar
        searchSlot={(
          <label className="flex items-center gap-2.5 rounded-[12px] border border-app bg-[var(--app-surface-strong)] px-3.5 py-2.5">
            <Search className="size-4 text-muted" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
              placeholder="Search by work order number, client, asset, or technician"
            />
          </label>
        )}
        actions={(
          <>
            <button type="button" className="button-secondary" onClick={() => setFiltersOpen((current) => !current)}>
              <SlidersHorizontal className="size-4" />
              {filtersOpen ? 'Hide Filters' : 'Filters'}
            </button>
            <button type="button" className="button-secondary" onClick={resetFilters} disabled={!hasAppliedFilters && !search}>
              Reset
            </button>
          </>
        )}
      />

      {filtersOpen ? (
        <SectionPanel
          title="Queue filters"
          description="Apply only the filters needed for the current queue view."
          action={(
            <div className="flex flex-wrap gap-2">
              <button type="button" className="button-secondary" onClick={() => setDraftFilters(appliedFilters)}>Use current</button>
              <button type="button" className="button-primary" onClick={applyFilters}>Apply</button>
            </div>
          )}
        >
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <label className="space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Status</span>
              <select value={draftFilters.statusTab} onChange={(event) => setDraftFilters((current) => ({ ...current, statusTab: event.target.value as QueueFilters['statusTab'] }))} className="field-input">
                {statusTabs.map((tab) => (
                  <option key={tab} value={tab}>{tab === 'All' ? 'All statuses' : tab}</option>
                ))}
              </select>
            </label>
            <label className="space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Focus</span>
              <select value={draftFilters.quickFilter} onChange={(event) => setDraftFilters((current) => ({ ...current, quickFilter: event.target.value as QueueFilters['quickFilter'] }))} className="field-input">
                <option value="all">All focus views</option>
                <option value="overdue">Overdue</option>
                <option value="my-group">My Group</option>
                <option value="assigned-to-me">Assigned to Me</option>
              </select>
            </label>
            <label className="space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Priority</span>
              <select value={draftFilters.priority} onChange={(event) => setDraftFilters((current) => ({ ...current, priority: event.target.value }))} className="field-input">
                <option value="All">All priorities</option>
                {Array.from(new Set(data.workOrders.map((item) => item.priority))).filter(Boolean).map((item) => (
                  <option key={item} value={item}>{item}</option>
                ))}
              </select>
            </label>
            <label className="space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Work type</span>
              <select value={draftFilters.workType} onChange={(event) => setDraftFilters((current) => ({ ...current, workType: event.target.value }))} className="field-input">
                <option value="All">All work types</option>
                <option value="NewInstallation">New Installation</option>
                <option value="PreventiveMaintenance">Preventive Maintenance</option>
                <option value="CorrectiveMaintenance">Corrective Maintenance</option>
                <option value="InspectionAssessment">Inspection / Assessment</option>
              </select>
            </label>
            <label className="space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Client</span>
              <select value={draftFilters.clientId} onChange={(event) => setDraftFilters((current) => ({ ...current, clientId: event.target.value }))} className="field-input">
                <option value="All">All clients</option>
                {data.clients.map((client) => (
                  <option key={client.id} value={client.id}>{client.clientName}</option>
                ))}
              </select>
            </label>
            <label className="space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Site</span>
              <select value={draftFilters.site} onChange={(event) => setDraftFilters((current) => ({ ...current, site: event.target.value }))} className="field-input">
                <option value="All">All sites</option>
                {sites.map((item) => (
                  <option key={item} value={item}>{item}</option>
                ))}
              </select>
            </label>
            <label className="space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Due from</span>
              <input type="date" value={draftFilters.dateFrom} onChange={(event) => setDraftFilters((current) => ({ ...current, dateFrom: event.target.value }))} className="field-input" aria-label="Date from" />
            </label>
            <label className="space-y-1.5">
              <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Due to</span>
              <input type="date" value={draftFilters.dateTo} onChange={(event) => setDraftFilters((current) => ({ ...current, dateTo: event.target.value }))} className="field-input" aria-label="Date to" />
            </label>
          </div>
        </SectionPanel>
      ) : null}

      {hasAppliedFilters ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted">
          <span className="font-medium text-app">Applied:</span>
          {appliedFilterLabels.map((label) => (
            <FilterChip key={label} active>{label}</FilterChip>
          ))}
        </div>
      ) : null}

      <SectionPanel
        title="Operational queue"
        description={filteredRows.length === 1 ? '1 work order in view.' : `${filteredRows.length} work orders in view.`}
      >
        <div className="hidden lg:block">
          <DataTableWrapper>
            <table className="w-full min-w-[1040px]">
            <thead className="table-head">
              <tr>
                {['Work Order', 'Client / Site', 'Asset', 'Type', 'Status', 'Assignment', 'Due', 'Action'].map((header) => (
                  <th key={header} className="px-3 py-2.5 text-left text-[10px] font-semibold uppercase tracking-[0.18em] text-muted">{header}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {filteredRows.map((item) => (
                <tr key={item.id} className="table-row border-t border-app">
                  <td className="px-3 py-3 align-top">
                    <Link to={`/work-orders/${item.id}`} className="font-semibold text-app hover:underline">{item.workOrderNumber}</Link>
                    <p className="mt-0.5 max-w-[240px] text-sm text-muted">{item.title}</p>
                  </td>
                  <td className="px-3 py-3 align-top">
                    <p className="text-sm text-app">{item.clientName || '-'}</p>
                    <p className="mt-0.5 text-xs text-muted">{item.siteName || 'No site linked'}</p>
                  </td>
                  <td className="px-3 py-3 text-sm text-app">{item.assetName || '-'}</td>
                  <td className="px-3 py-3"><WorkTypeBadge workType={item.workType} /></td>
                  <td className="px-3 py-3 align-top">
                    <div className="flex flex-wrap gap-1.5">
                      <WorkflowStatusBadge workOrder={item} />
                      <PriorityBadge priority={item.priority} />
                      {isOverdue(item) ? <span className="inline-flex rounded-full border border-[var(--app-badge-danger-border)] bg-[var(--app-badge-danger-bg)] px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-[var(--app-badge-danger-text)]">Overdue</span> : null}
                    </div>
                    {item.holdReason ? <p className="mt-1.5 text-xs font-medium text-secondary">Hold: {item.holdReason}</p> : null}
                    {item.holdNote ? <p className="mt-1 max-w-[220px] text-xs text-muted">{item.holdNote}</p> : null}
                  </td>
                  <td className="px-3 py-3 align-top">
                    <p className="text-sm text-app">{item.assignmentGroupName || '-'}</p>
                    <p className="mt-0.5 text-xs text-muted">{item.leadTechnicianName || item.assignedTechnicianName || item.technicianAssignments?.map((assignment) => assignment.technicianName || '').filter(Boolean).join(', ') || 'No technician assigned'}</p>
                  </td>
                  <td className="px-3 py-3 text-sm text-app">{item.dueDate ? formatDateOnly(item.dueDate) : '-'}</td>
                  <td className="px-3 py-3">
                    <ContextPrimaryButton workOrder={item} onAction={(action) => void runRowAction(item, action.kind)} />
                    {busyId === item.id ? <p className="mt-1.5 text-xs text-muted">Updating...</p> : null}
                  </td>
                </tr>
              ))}
              {filteredRows.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-8 text-center text-sm text-muted">No work orders match the current filters.</td>
                </tr>
              ) : null}
            </tbody>
            </table>
          </DataTableWrapper>
        </div>

        <div className="divide-y divide-[var(--app-divider)] lg:hidden">
          {filteredRows.map((item) => (
            <article key={item.id} className="space-y-2.5 py-3 first:pt-0 last:pb-0">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <Link to={`/work-orders/${item.id}`} className="text-sm font-semibold text-app hover:underline">{item.workOrderNumber}</Link>
                  <p className="mt-0.5 text-sm text-secondary">{item.title}</p>
                  <p className="mt-0.5 text-xs text-muted">{item.clientName || 'No client'} | {item.siteName || 'No site'}</p>
                </div>
                <div className="flex flex-wrap justify-end gap-1.5">
                  <WorkflowStatusBadge workOrder={item} />
                  <PriorityBadge priority={item.priority} />
                </div>
              </div>

              <dl className="grid gap-x-4 gap-y-2 sm:grid-cols-2">
                <MobileMeta label="Asset" value={item.assetName || '-'} />
                <MobileMeta label="Work Type" value={formatWorkTypeLabel(item.workType)} />
                <MobileMeta label="Technician" value={item.leadTechnicianName || item.assignedTechnicianName || '-'} />
                <MobileMeta label="Assigned Group" value={item.assignmentGroupName || '-'} />
                <MobileMeta label="Due Date" value={item.dueDate ? formatDateOnly(item.dueDate) : '-'} />
              </dl>

              {item.holdReason ? <p className="text-xs font-medium text-secondary">Hold: {item.holdReason}</p> : null}
              {item.holdNote ? <p className="text-xs text-muted">{item.holdNote}</p> : null}

              <div>
                <ContextPrimaryButton workOrder={item} onAction={(action) => void runRowAction(item, action.kind)} />
              </div>
            </article>
          ))}

          {filteredRows.length === 0 ? (
            <EmptyState title="No matching records" description="Adjust the queue filters or create a new work order to start the workflow." />
          ) : null}
        </div>
      </SectionPanel>

      <Modal
        open={editorOpen}
        onClose={() => setEditorOpen(false)}
        title="New Work Order"
        description="Create a clean work order shell that can be assigned and progressed through the guided workflow."
        maxWidth="max-w-2xl"
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="space-y-2 sm:col-span-2">
            <span className="text-sm font-medium text-app">Client</span>
            <select value={draft.clientId} onChange={(event) => setDraft((current) => ({ ...current, clientId: event.target.value }))} className="field-input">
              <option value="">Select client</option>
              {data.clients.map((client) => (
                <option key={client.id} value={client.id}>{client.clientName}</option>
              ))}
            </select>
          </label>
          {draft.clientId && (
            <label className="space-y-2 sm:col-span-2">
              <span className="text-sm font-medium text-app">Site <span className="text-slate-400 text-xs">(optional)</span></span>
              <select
                aria-label="Site"
                className="field-input"
                value={draft.siteId}
                onChange={e => setDraft(d => ({ ...d, siteId: e.target.value, assetId: '' }))}
                disabled={loadingSites}
              >
                <option value="">{loadingSites ? 'Loading sites…' : 'Select site…'}</option>
                {draftSites.map(s => (
                  <option key={s.id} value={s.id}>{s.siteName}</option>
                ))}
              </select>
            </label>
          )}
          {draft.clientId && (
            <label className="space-y-2 sm:col-span-2">
              <span className="text-sm font-medium text-app">Asset <span className="text-slate-400 text-xs">(optional)</span></span>
              <select
                aria-label="Asset"
                className="field-input"
                value={draft.assetId}
                onChange={e => setDraft(d => ({ ...d, assetId: e.target.value }))}
                disabled={!draft.siteId || loadingAssets}
              >
                <option value="">
                  {!draft.siteId ? 'Select a site first' : loadingAssets ? 'Loading assets…' : 'Select asset…'}
                </option>
                {draftAssets.map(a => (
                  <option key={a.id} value={a.id}>{a.assetName}{a.assetCode ? ` (${a.assetCode})` : ''}</option>
                ))}
              </select>
            </label>
          )}
          <label className="space-y-2 sm:col-span-2">
            <span className="text-sm font-medium text-app">Title</span>
            <input value={draft.title} onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))} className="field-input" placeholder="Describe the service request" />
          </label>
          <label className="space-y-2 sm:col-span-2">
            <span className="text-sm font-medium text-app">Description</span>
            <textarea value={draft.description} onChange={(event) => setDraft((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[120px]" placeholder="Add concise notes for dispatch and field execution" />
          </label>
          <label className="space-y-2">
            <span className="text-sm font-medium text-app">Work Type</span>
            <select
              value={draft.workType}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  workType: event.target.value as WorkOrderDraft['workType'],
                  pmTemplateId: '',
                }))}
              className="field-input"
              aria-label="Work Type"
            >
              <option value="">Select work type</option>
              <option value="NewInstallation">New Installation</option>
              <option value="PreventiveMaintenance">Preventive Maintenance</option>
              <option value="CorrectiveMaintenance">Corrective Maintenance</option>
              <option value="InspectionAssessment">Inspection / Assessment</option>
            </select>
          </label>
          <label className="space-y-2">
            <span className="text-sm font-medium text-app">Service checklist</span>
            <select
              value={draft.pmTemplateId}
              onChange={(event) => setDraft((current) => ({ ...current, pmTemplateId: event.target.value }))}
              className="field-input"
            >
              <option value="">{availableTemplates.length === 0 ? 'No checklist available' : 'Select checklist'}</option>
              {availableTemplates.map((template) => (
                <option key={template.id} value={template.id}>{template.name}</option>
              ))}
            </select>
            <p className="text-xs text-muted">
              {draft.workType === 'PreventiveMaintenance'
                ? 'A preventive maintenance job needs a checklist template.'
                : draft.workType === 'NewInstallation'
                  ? 'Choose a checklist when you want the team to follow an installation or commissioning flow.'
                  : draft.workType === 'InspectionAssessment'
                    ? 'Choose a checklist when you want the team to follow an inspection or assessment flow.'
                    : 'Choose a checklist when you want the job team to follow a predefined corrective workflow.'}
            </p>
          </label>
          {data.assignmentGroups.length > 0 ? (
            <label className="space-y-2 sm:col-span-2">
              <span className="text-sm font-medium text-app">Assignment group</span>
              <select value={draft.assignmentGroupId} onChange={(event) => setDraft((current) => ({ ...current, assignmentGroupId: event.target.value }))} className="field-input">
                <option value="">Leave unassigned</option>
                {data.assignmentGroups.filter((group) => group.isActive).map((group) => (
                  <option key={group.id} value={group.id}>{group.name}</option>
                ))}
              </select>
              <p className="text-xs text-muted">If selected, the work order will be created first and then routed to this group.</p>
            </label>
          ) : null}
          <label className="space-y-2">
            <span className="text-sm font-medium text-app">Priority</span>
            <select value={draft.priority} onChange={(event) => setDraft((current) => ({ ...current, priority: event.target.value }))} className="field-input">
              <option>Low</option>
              <option>Medium</option>
              <option>High</option>
              <option>Critical</option>
            </select>
          </label>
          <label className="space-y-2">
            <span className="text-sm font-medium text-app">Due date</span>
            <input type="date" value={draft.dueDate} onChange={(event) => setDraft((current) => ({ ...current, dueDate: event.target.value }))} className="field-input" />
          </label>
          <div className="space-y-3 sm:col-span-2 pt-1 border-t border-subtle">
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-app">Notify client on updates</span>
              <button
                type="button"
                onClick={() => setDraft((c) => ({ ...c, notifyClient: !c.notifyClient }))}
                className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none ${draft.notifyClient ? 'bg-brand' : 'bg-muted/30'}`}
                aria-pressed={draft.notifyClient}
              >
                <span className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${draft.notifyClient ? 'translate-x-6' : 'translate-x-1'}`} />
              </button>
            </div>
            {draft.notifyClient && (
              <label className="block space-y-1">
                <span className="text-xs text-muted">Client contact email (optional)</span>
                <input
                  type="email"
                  value={draft.clientContactEmail}
                  onChange={(e) => setDraft((c) => ({ ...c, clientContactEmail: e.target.value }))}
                  className="field-input"
                  placeholder="Leave blank to use client’s default email"
                />
              </label>
            )}
          </div>
          {selectedTemplate ? (
            <div className="panel-subtle rounded-[20px] px-4 py-4 text-sm text-app sm:col-span-2">
              <p className="font-semibold">{selectedTemplate.name}</p>
              <p className="mt-1 text-muted">{selectedTemplate.description || 'Reusable checklist selected for this work order.'}</p>
              <p className="mt-2 text-xs text-muted">
                {selectedTemplate.checklist.length} item{selectedTemplate.checklist.length === 1 ? '' : 's'}
                {selectedTemplate.defaultIntervalMonths ? ` • Default interval ${selectedTemplate.defaultIntervalMonths} month${selectedTemplate.defaultIntervalMonths === 1 ? '' : 's'}` : ''}
              </p>
            </div>
          ) : null}
        </div>
        <div className="mt-5 flex justify-end gap-3">
          <button type="button" className="button-secondary" onClick={() => setEditorOpen(false)}>Back</button>
          <button type="button" className="button-primary" onClick={() => void createWorkOrder()} disabled={saving}>
            {saving ? 'Creating...' : 'Create Work Order'}
          </button>
        </div>
      </Modal>

      <HoldReasonModal
        open={Boolean(holdTarget)}
        reason={holdReason}
        note={holdNote}
        busy={busyId === holdTarget?.id}
        onReasonChange={setHoldReason}
        onNoteChange={setHoldNote}
        onClose={() => {
          setHoldTarget(null)
          setHoldReason('')
          setHoldNote('')
        }}
        onConfirm={() => void submitHold()}
      />
    </div>
  )
}

function MobileMeta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-[10px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</dt>
      <dd className="mt-1 text-sm text-app">{value}</dd>
    </div>
  )
}
