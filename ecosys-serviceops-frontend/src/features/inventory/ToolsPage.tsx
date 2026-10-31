import { useState, type ReactNode } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus, Wrench } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { EmptyState } from '../../shared/components/EmptyState'
import { ErrorState } from '../../shared/components/ErrorState'
import { Spinner } from '../../shared/components/Spinner'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useTechnicians } from '../team/api'
import { useBranches } from './api'
import { SummaryStrip } from './components/SummaryStrip'
import {
  toolConditions,
  toolStatusLabels,
  useCheckInTool,
  useCheckOutTool,
  useCreateTool,
  useRecordToolCalibration,
  useSetToolStatus,
  useTool,
  useToolSummary,
  useTools,
  useUpdateTool,
  type ToolCondition,
  type ToolDetail,
  type ToolFilters,
  type ToolListItem,
  type ToolStatus,
} from './toolsApi'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const smallButton = 'rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50'

function useCanManageTools() {
  const canManage = usePermission('inventory.materials.manage')
  const canIssue = usePermission('inventory.requests.issue')
  return canManage || canIssue
}

/** yyyy-mm-dd for date inputs, in local time. */
function dateInput(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function CalibrationBadge({ tool }: { tool: ToolListItem }) {
  if (!tool.calibrationState || !tool.nextCalibrationDue) return <span className="text-slate-400">—</span>
  const label = tool.calibrationState === 'Overdue' ? 'Calibration Overdue' : tool.calibrationState === 'DueSoon' ? 'Calibration Due Soon' : 'Calibrated'
  const tone = tool.calibrationState === 'Overdue' ? 'text-red-600' : tool.calibrationState === 'DueSoon' ? 'text-amber-700' : 'text-slate-500'
  return (
    <div>
      <span className={cn('text-xs font-semibold', tone)}>{label}</span>
      <p className="text-xs text-slate-500">{tool.calibrationState === 'Overdue' ? 'Was due' : 'Due'} {formatDate(tool.nextCalibrationDue)}</p>
    </div>
  )
}

function HolderCell({ tool }: { tool: ToolListItem }) {
  if (!tool.holderName) return <span className="text-slate-400">{tool.homeBranchName ?? 'Store'}</span>
  return (
    <div>
      <span className="text-slate-900">{tool.holderName}</span>
      <p className={cn('text-xs', tool.returnOverdue ? 'font-medium text-red-600' : 'text-slate-500')}>
        {tool.expectedReturnAt ? `${tool.returnOverdue ? 'Overdue since' : 'Back by'} ${formatDate(tool.expectedReturnAt)}` : `Since ${formatDate(tool.checkedOutAt!)}`}
        {tool.awaitingAcknowledgement ? ' · not yet confirmed' : ''}
      </p>
    </div>
  )
}

export const toolColumns: DataTableColumn<ToolListItem>[] = [
  {
    key: 'name',
    header: 'Tool',
    cell: (row) => (
      <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/tools/${row.id}`}>
        {row.name} <span className="font-mono text-xs text-slate-500">{row.toolCode}</span>
      </Link>
    ),
  },
  { key: 'category', header: 'Category', cell: (row) => row.category ?? '—' },
  { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={toolStatusLabels[row.status]} /> },
  { key: 'holder', header: 'With', cell: (row) => <HolderCell tool={row} /> },
  { key: 'condition', header: 'Condition', cell: (row) => row.condition },
  { key: 'calibration', header: 'Calibration', cell: (row) => <CalibrationBadge tool={row} />, sortValue: (row) => row.nextCalibrationDue ?? '' },
  { key: 'value', header: 'Value', cell: (row) => (row.purchaseCost != null ? formatCurrency(row.purchaseCost) : '—') },
]

const statusFilters: Array<{ id: ToolFilters['status'] | ''; label: string }> = [
  { id: '', label: 'Active' },
  { id: 'InStore', label: 'In Store' },
  { id: 'CheckedOut', label: 'Checked Out' },
  { id: 'UnderRepair', label: 'Under Repair' },
  { id: 'Lost', label: 'Lost' },
  { id: 'Retired', label: 'Retired' },
]

export function ToolsPage() {
  const canManage = useCanManageTools()
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState('')
  const [showNew, setShowNew] = useState(false)
  const debounced = useDebounce(search, 300)
  const status = (params.get('status') ?? '') as ToolFilters['status'] | ''
  const attention = (params.get('attention') ?? undefined) as ToolFilters['attention']
  const { data, isLoading, isError, refetch } = useTools({ status: status || undefined, attention, search: debounced.trim() || undefined })
  const { data: summary } = useToolSummary()

  const setFilter = (next: Record<string, string>) => setParams(next, { replace: true })
  const attentionFigure = (label: string, value: number | undefined, key: NonNullable<ToolFilters['attention']>, tone: 'danger' | 'warning') => ({
    label,
    value: value ?? '—',
    tone: value ? tone : ('default' as const),
    active: attention === key,
    onClick: () => setFilter(attention === key ? {} : { attention: key }),
  })

  return (
    <div className="space-y-4">
      <PageHeader
        title="Tools"
        subtitle="Tools and test equipment: who has what, what is due back, and what needs calibration."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Tool
            </button>
          ) : undefined
        }
      />

      <SummaryStrip
        figures={[
          { label: 'Checked out', value: summary ? `${summary.checkedOut} of ${summary.total}` : '—' },
          attentionFigure('Calibration overdue', summary?.calibrationOverdue, 'calibration', 'danger'),
          attentionFigure('Due back (overdue)', summary?.returnOverdue, 'overdue-return', 'danger'),
          attentionFigure('Awaiting confirmation', summary?.awaitingAcknowledgement, 'unacknowledged', 'warning'),
          { label: 'Value out with technicians', value: summary ? formatCurrency(summary.valueCheckedOut) : '—' },
        ]}
      />
      {summary && summary.calibrationDueSoon > 0 ? (
        <p className="text-sm text-amber-700">{summary.calibrationDueSoon} tool{summary.calibrationDueSoon === 1 ? '' : 's'} due for calibration in the next 14 days (included under Calibration).</p>
      ) : null}
      {summary && summary.heldByInactiveTechnicians > 0 ? (
        <p className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {summary.heldByInactiveTechnicians} tool{summary.heldByInactiveTechnicians === 1 ? ' is' : 's are'} still checked out to deactivated technicians. Recover or mark {summary.heldByInactiveTechnicians === 1 ? 'it' : 'them'} lost.
        </p>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tool, code, serial or technician…" aria-label="Search tools" className="h-10 min-w-[14rem] flex-1 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        {statusFilters.map((filter) => (
          <button
            key={filter.id || 'active'}
            type="button"
            onClick={() => setFilter(filter.id ? { status: filter.id } : {})}
            className={cn('inline-flex h-10 items-center rounded-lg border px-3 text-sm font-medium transition', !attention && status === filter.id ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
          >
            {filter.label}
          </button>
        ))}
      </div>

      <DataTable
        columns={toolColumns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Tools unavailable' : attention ? 'Nothing needs attention here' : 'No tools'}
        emptyDescription={isError ? 'The tool register could not be loaded.' : 'Add tools with New Tool to track who has them.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />
      {showNew ? <ToolFormDrawer onClose={() => setShowNew(false)} /> : null}
    </div>
  )
}

function ToolFormDrawer({ detail, onClose }: { detail?: ToolDetail; onClose: () => void }) {
  const tool = detail?.tool
  const create = useCreateTool()
  const update = useUpdateTool(tool?.id ?? '')
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const [values, setValues] = useState({
    toolCode: tool?.toolCode ?? '',
    name: tool?.name ?? '',
    category: tool?.category ?? '',
    serialNumber: tool?.serialNumber ?? '',
    manufacturer: detail?.manufacturer ?? '',
    model: detail?.model ?? '',
    purchaseDate: detail?.purchaseDate?.slice(0, 10) ?? '',
    purchaseCost: tool?.purchaseCost != null ? String(tool.purchaseCost) : '',
    homeBranchId: tool?.homeBranchId ?? '',
    calibrationIntervalDays: detail?.calibrationIntervalDays != null ? String(detail.calibrationIntervalDays) : '',
    nextCalibrationDue: tool?.nextCalibrationDue?.slice(0, 10) ?? '',
    notes: detail?.notes ?? '',
  })
  const set = (key: keyof typeof values) => (event: { target: { value: string } }) => setValues((prev) => ({ ...prev, [key]: event.target.value }))
  const pending = create.isPending || update.isPending

  const submit = async () => {
    if (!values.toolCode.trim() || !values.name.trim()) return toast.error('Code and name are required')
    const payload = {
      toolCode: values.toolCode.trim(),
      name: values.name.trim(),
      category: values.category.trim() || null,
      serialNumber: values.serialNumber.trim() || null,
      manufacturer: values.manufacturer.trim() || null,
      model: values.model.trim() || null,
      purchaseDate: values.purchaseDate || null,
      purchaseCost: values.purchaseCost ? Number(values.purchaseCost) : null,
      homeBranchId: values.homeBranchId || null,
      calibrationIntervalDays: values.calibrationIntervalDays ? Number(values.calibrationIntervalDays) : null,
      nextCalibrationDue: values.nextCalibrationDue || null,
      notes: values.notes.trim() || null,
    }
    try {
      if (tool) await update.mutateAsync(payload)
      else await create.mutateAsync(payload)
      toast.success(tool ? 'Tool updated' : 'Tool added')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={tool ? `Edit ${tool.name}` : 'New Tool'}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
          <button type="button" disabled={pending} onClick={() => void submit()} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {pending ? 'Saving…' : tool ? 'Save Changes' : 'Add Tool'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="tool-code">Asset Tag / Code</label>
            <input id="tool-code" value={values.toolCode} onChange={set('toolCode')} placeholder="e.g. MM-014" className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tool-category">Category</label>
            <input id="tool-category" value={values.category} onChange={set('category')} placeholder="e.g. Test equipment" className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="tool-name">Name</label>
          <input id="tool-name" value={values.name} onChange={set('name')} placeholder="e.g. Fluke 117 multimeter" className={fieldClass} />
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className={labelClass} htmlFor="tool-serial">Serial No.</label>
            <input id="tool-serial" value={values.serialNumber} onChange={set('serialNumber')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tool-make">Make</label>
            <input id="tool-make" value={values.manufacturer} onChange={set('manufacturer')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tool-model">Model</label>
            <input id="tool-model" value={values.model} onChange={set('model')} className={fieldClass} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="tool-bought">Purchase Date</label>
            <input id="tool-bought" type="date" value={values.purchaseDate} onChange={set('purchaseDate')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tool-cost">Purchase Cost</label>
            <input id="tool-cost" type="number" min="0" step="0.01" value={values.purchaseCost} onChange={set('purchaseCost')} className={fieldClass} />
          </div>
        </div>
        {activeBranches.length > 0 ? (
          <div>
            <label className={labelClass} htmlFor="tool-branch">Home Branch</label>
            <select id="tool-branch" value={values.homeBranchId} onChange={set('homeBranchId')} className={fieldClass}>
              <option value="">Whole workspace</option>
              {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          </div>
        ) : null}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="tool-interval">Calibrate Every (days)</label>
            <input id="tool-interval" type="number" min="1" step="1" value={values.calibrationIntervalDays} onChange={set('calibrationIntervalDays')} placeholder="Not required" className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="tool-next-cal">Next Calibration Due</label>
            <input id="tool-next-cal" type="date" value={values.nextCalibrationDue} onChange={set('nextCalibrationDue')} disabled={!values.calibrationIntervalDays} className={cn(fieldClass, 'disabled:bg-slate-50')} />
          </div>
        </div>
        <p className="text-xs text-slate-500">A tool past its calibration date cannot be checked out without an override. Recording a calibration moves the due date on by the interval.</p>
        <div>
          <label className={labelClass} htmlFor="tool-notes">Notes</label>
          <textarea id="tool-notes" rows={2} value={values.notes} onChange={set('notes')} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
      </div>
    </FormDrawer>
  )
}

function ActionDrawer({ title, submitLabel, pending, onSubmit, onClose, children }: { title: string; submitLabel: string; pending: boolean; onSubmit: () => void; onClose: () => void; children: ReactNode }) {
  return (
    <FormDrawer
      open
      title={title}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
          <button type="button" disabled={pending} onClick={onSubmit} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {pending ? 'Saving…' : submitLabel}
          </button>
        </div>
      }
    >
      <div className="space-y-4">{children}</div>
    </FormDrawer>
  )
}

function ConditionSelect({ id, value, onChange }: { id: string; value: ToolCondition; onChange: (value: ToolCondition) => void }) {
  return (
    <select id={id} value={value} onChange={(event) => onChange(event.target.value as ToolCondition)} className={fieldClass}>
      {toolConditions.map((condition) => <option key={condition} value={condition}>{condition}</option>)}
    </select>
  )
}

function CheckOutDrawer({ tool, onClose }: { tool: ToolListItem; onClose: () => void }) {
  const checkOut = useCheckOutTool(tool.id)
  const { data: technicians } = useTechnicians()
  const [technicianId, setTechnicianId] = useState('')
  const [expected, setExpected] = useState('')
  const [condition, setCondition] = useState<ToolCondition>(tool.condition)
  const [notes, setNotes] = useState('')
  const [override, setOverride] = useState(false)
  const overdue = tool.calibrationState === 'Overdue'

  const submit = async () => {
    if (!technicianId) return toast.error('Choose a technician')
    if (overdue && !override) return toast.error('Calibration is overdue — tick the override to check it out anyway')
    try {
      await checkOut.mutateAsync({ technicianId, expectedReturnAt: expected || null, condition, notes: notes.trim() || null, overrideCalibration: override })
      toast.success('Tool checked out')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <ActionDrawer title={`Check out ${tool.name}`} submitLabel="Check Out" pending={checkOut.isPending} onSubmit={() => void submit()} onClose={onClose}>
      <div>
        <label className={labelClass} htmlFor="co-tech">Technician</label>
        <select id="co-tech" value={technicianId} onChange={(event) => setTechnicianId(event.target.value)} className={fieldClass}>
          <option value="">Select…</option>
          {(technicians ?? []).filter((tech) => tech.isActive).map((tech) => <option key={tech.id} value={tech.id}>{tech.fullName}</option>)}
        </select>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className={labelClass} htmlFor="co-expected">Expected Back</label>
          <input id="co-expected" type="date" min={dateInput(new Date())} value={expected} onChange={(event) => setExpected(event.target.value)} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="co-condition">Condition Out</label>
          <ConditionSelect id="co-condition" value={condition} onChange={setCondition} />
        </div>
      </div>
      <div>
        <label className={labelClass} htmlFor="co-notes">Notes</label>
        <input id="co-notes" value={notes} onChange={(event) => setNotes(event.target.value)} placeholder="e.g. For the hospital chiller job" className={fieldClass} />
      </div>
      {overdue ? (
        <label className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <input type="checkbox" className="mt-1" checked={override} onChange={(event) => setOverride(event.target.checked)} />
          Calibration was due {formatDate(tool.nextCalibrationDue!)}. Check it out anyway (recorded in the audit log).
        </label>
      ) : null}
      <p className="text-xs text-slate-500">The technician sees the tool under Me → My tools and confirms they have it.</p>
    </ActionDrawer>
  )
}

function CheckInDrawer({ tool, onClose }: { tool: ToolListItem; onClose: () => void }) {
  const checkIn = useCheckInTool(tool.id)
  const [condition, setCondition] = useState<ToolCondition>(tool.condition)
  const [notes, setNotes] = useState('')
  const [repair, setRepair] = useState(false)

  const submit = async () => {
    try {
      await checkIn.mutateAsync({ condition, notes: notes.trim() || null, sendToRepair: repair })
      toast.success(repair ? 'Tool returned and sent for repair' : 'Tool returned to store')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <ActionDrawer title={`Check in ${tool.name}`} submitLabel="Check In" pending={checkIn.isPending} onSubmit={() => void submit()} onClose={onClose}>
      <p className="text-sm text-slate-600">From {tool.holderName}, out since {formatDate(tool.checkedOutAt!)}.</p>
      <div>
        <label className={labelClass} htmlFor="ci-condition">Condition Returned</label>
        <ConditionSelect id="ci-condition" value={condition} onChange={(value) => { setCondition(value); if (value === 'Damaged') setRepair(true) }} />
      </div>
      <div>
        <label className={labelClass} htmlFor="ci-notes">Notes</label>
        <input id="ci-notes" value={notes} onChange={(event) => setNotes(event.target.value)} placeholder="e.g. Probe lead frayed" className={fieldClass} />
      </div>
      <label className="flex items-center gap-2 text-sm text-slate-700">
        <input type="checkbox" checked={repair} onChange={(event) => setRepair(event.target.checked)} />
        Send straight to repair
      </label>
    </ActionDrawer>
  )
}

function CalibrationDrawer({ detail, onClose }: { detail: ToolDetail; onClose: () => void }) {
  const record = useRecordToolCalibration(detail.tool.id)
  const [calibratedOn, setCalibratedOn] = useState(dateInput(new Date()))
  const [nextDueOn, setNextDueOn] = useState('')
  const [result, setResult] = useState<'Pass' | 'Fail'>('Pass')
  const [certificateNumber, setCertificateNumber] = useState('')
  const [performedBy, setPerformedBy] = useState('')
  const [notes, setNotes] = useState('')

  const submit = async () => {
    if (!calibratedOn) return toast.error('Enter the calibration date')
    try {
      await record.mutateAsync({ calibratedOn, nextDueOn: nextDueOn || null, result, certificateNumber: certificateNumber.trim() || null, performedBy: performedBy.trim() || null, notes: notes.trim() || null })
      toast.success(result === 'Pass' ? 'Calibration recorded' : 'Failed calibration recorded — tool sent to repair')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <ActionDrawer title={`Record calibration · ${detail.tool.name}`} submitLabel="Record" pending={record.isPending} onSubmit={() => void submit()} onClose={onClose}>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className={labelClass} htmlFor="cal-date">Calibrated / Inspected On</label>
          <input id="cal-date" type="date" max={dateInput(new Date())} value={calibratedOn} onChange={(event) => setCalibratedOn(event.target.value)} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="cal-result">Result</label>
          <select id="cal-result" value={result} onChange={(event) => setResult(event.target.value as 'Pass' | 'Fail')} className={fieldClass}>
            <option value="Pass">Pass</option>
            <option value="Fail">Fail</option>
          </select>
        </div>
      </div>
      {result === 'Pass' ? (
        <div>
          <label className={labelClass} htmlFor="cal-next">Next Due</label>
          <input id="cal-next" type="date" value={nextDueOn} onChange={(event) => setNextDueOn(event.target.value)} className={fieldClass} />
          <p className="mt-1 text-xs text-slate-500">{detail.calibrationIntervalDays ? `Leave blank for ${detail.calibrationIntervalDays} days after the calibration date.` : 'Leave blank if it needs no further calibration.'}</p>
        </div>
      ) : (
        <p className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">A failed calibration marks the tool overdue and sends it to repair until a passing calibration is recorded.</p>
      )}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className={labelClass} htmlFor="cal-cert">Certificate No.</label>
          <input id="cal-cert" value={certificateNumber} onChange={(event) => setCertificateNumber(event.target.value)} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="cal-by">Performed By</label>
          <input id="cal-by" value={performedBy} onChange={(event) => setPerformedBy(event.target.value)} placeholder="Lab or person" className={fieldClass} />
        </div>
      </div>
      <div>
        <label className={labelClass} htmlFor="cal-notes">Notes</label>
        <input id="cal-notes" value={notes} onChange={(event) => setNotes(event.target.value)} className={fieldClass} />
      </div>
    </ActionDrawer>
  )
}

function StatusDrawer({ tool, target, onClose }: { tool: ToolListItem; target: 'InStore' | 'UnderRepair' | 'Lost' | 'Retired'; onClose: () => void }) {
  const setStatus = useSetToolStatus(tool.id)
  const [notes, setNotes] = useState('')
  const needsReason = target === 'Lost' || target === 'Retired'

  const submit = async () => {
    if (needsReason && !notes.trim()) return toast.error('Give a reason')
    try {
      await setStatus.mutateAsync({ status: target, notes: notes.trim() || null })
      toast.success(`Tool marked ${toolStatusLabels[target].toLowerCase()}`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <ActionDrawer title={`Mark ${tool.name} ${toolStatusLabels[target].toLowerCase()}`} submitLabel="Confirm" pending={setStatus.isPending} onSubmit={() => void submit()} onClose={onClose}>
      {target === 'Lost' && tool.holderName ? <p className="text-sm text-slate-600">It is checked out to {tool.holderName}; that check-out is closed against them.</p> : null}
      <div>
        <label className={labelClass} htmlFor="status-notes">{needsReason ? 'Reason (required)' : 'Notes'}</label>
        <input id="status-notes" value={notes} onChange={(event) => setNotes(event.target.value)} className={fieldClass} />
      </div>
    </ActionDrawer>
  )
}

type ToolAction = 'edit' | 'check-out' | 'check-in' | 'calibrate' | Exclude<ToolStatus, 'CheckedOut'>

export function ToolDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canManage = useCanManageTools()
  const { data, isLoading, isError, refetch } = useTool(id)
  const [action, setAction] = useState<ToolAction | null>(null)

  if (isLoading) return <Spinner label="Loading tool" />
  if (isError) return <ErrorState title="Tool unavailable" description="This tool could not be loaded." onRetry={() => void refetch()} />
  if (!data) return <EmptyState title="Tool not found" description="It may have been removed." />
  const tool = data.tool

  const actions = canManage ? (
    <div className="flex flex-wrap items-center gap-2">
      <StatusBadge status={toolStatusLabels[tool.status]} />
      {tool.status === 'InStore' ? <button type="button" onClick={() => setAction('check-out')} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)]">Check Out</button> : null}
      {tool.status === 'CheckedOut' ? <button type="button" onClick={() => setAction('check-in')} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)]">Check In</button> : null}
      {tool.status !== 'Retired' ? <button type="button" onClick={() => setAction('calibrate')} className={smallButton}>Record Calibration</button> : null}
      {tool.status === 'InStore' ? <button type="button" onClick={() => setAction('UnderRepair')} className={smallButton}>Send to Repair</button> : null}
      {tool.status === 'UnderRepair' || tool.status === 'Lost' ? <button type="button" onClick={() => setAction('InStore')} className={smallButton}>{tool.status === 'Lost' ? 'Found — Back in Store' : 'Back in Store'}</button> : null}
      {tool.status !== 'Lost' && tool.status !== 'Retired' ? <button type="button" onClick={() => setAction('Lost')} className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50">Mark Lost</button> : null}
      {tool.status !== 'CheckedOut' && tool.status !== 'Retired' ? <button type="button" onClick={() => setAction('Retired')} className={smallButton}>Retire</button> : null}
      <button type="button" onClick={() => setAction('edit')} className={smallButton}>Edit</button>
    </div>
  ) : <StatusBadge status={toolStatusLabels[tool.status]} />

  return (
    <div className="space-y-4">
      <PageHeader
        title={tool.name}
        subtitle={[tool.toolCode, tool.category, tool.serialNumber ? `S/N ${tool.serialNumber}` : null].filter(Boolean).join(' · ')}
        breadcrumbs={[{ label: 'Tools', to: '/inventory/tools' }, { label: tool.toolCode }]}
        secondaryActions={actions}
      />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">With</div>
          <div className="mt-2 text-sm"><HolderCell tool={tool} /></div>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Calibration</div>
          <div className="mt-2 text-sm"><CalibrationBadge tool={tool} />{data.calibrationIntervalDays ? <p className="text-xs text-slate-500">Every {data.calibrationIntervalDays} days</p> : null}</div>
        </div>
        {[
          ['Condition', tool.condition],
          ['Home Branch', tool.homeBranchName ?? 'Whole workspace'],
          ['Make / Model', [data.manufacturer, data.model].filter(Boolean).join(' / ') || '—'],
          ['Purchased', [data.purchaseDate ? formatDate(data.purchaseDate) : null, tool.purchaseCost != null ? formatCurrency(tool.purchaseCost) : null].filter(Boolean).join(' · ') || '—'],
        ].map(([label, value]) => (
          <div key={label} className="rounded-xl border border-slate-200 bg-white px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
            <div className="mt-2 text-sm text-slate-900">{value}</div>
          </div>
        ))}
      </div>
      {data.notes ? <p className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700">{data.notes}</p> : null}

      <section className="space-y-2">
        <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Holding History</h2>
        <DataTable
          columns={[
            { key: 'technicianName', header: 'Technician', cell: (row) => <Link className="underline decoration-slate-300 underline-offset-4" to={`/team/technicians/${row.technicianId}`}>{row.technicianName ?? '—'}</Link> },
            { key: 'out', header: 'Out', cell: (row) => <div>{formatDateTime(row.checkedOutAt)}<p className="text-xs text-slate-500">by {row.checkedOutByName ?? '—'} · {row.conditionOut}</p></div> },
            { key: 'ack', header: 'Confirmed', cell: (row) => (row.acknowledgedAt ? formatDateTime(row.acknowledgedAt) : row.returnedAt ? '—' : <span className="text-amber-700">Not yet</span>) },
            { key: 'back', header: 'Back', cell: (row) => (row.returnedAt ? <div>{formatDateTime(row.returnedAt)}<p className="text-xs text-slate-500">{row.conditionIn}{row.returnNotes ? ` · ${row.returnNotes}` : ''}</p></div> : row.expectedReturnAt ? `Due ${formatDate(row.expectedReturnAt)}` : 'Still out') },
            { key: 'notes', header: 'Notes', cell: (row) => row.notes ?? '—' },
          ]}
          data={data.history}
          emptyTitle="Never checked out"
          emptyDescription="Check-outs and returns appear here."
        />
      </section>

      <section className="space-y-2">
        <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Calibration & Inspection Records</h2>
        <DataTable
          columns={[
            { key: 'calibratedOn', header: 'Date', cell: (row) => formatDate(row.calibratedOn) },
            { key: 'result', header: 'Result', cell: (row) => <StatusBadge status={row.result === 'Pass' ? 'Passed' : 'Failed'} /> },
            { key: 'certificateNumber', header: 'Certificate', cell: (row) => row.certificateNumber ?? '—' },
            { key: 'performedBy', header: 'By', cell: (row) => row.performedBy ?? '—' },
            { key: 'nextDueOn', header: 'Next Due', cell: (row) => (row.nextDueOn ? formatDate(row.nextDueOn) : '—') },
            { key: 'notes', header: 'Notes', cell: (row) => row.notes ?? '—' },
          ]}
          data={data.calibrations}
          emptyTitle="No records"
          emptyDescription="Record calibrations and inspections to keep the evidence on file."
        />
      </section>

      {action === 'edit' ? <ToolFormDrawer detail={data} onClose={() => setAction(null)} /> : null}
      {action === 'check-out' ? <CheckOutDrawer tool={tool} onClose={() => setAction(null)} /> : null}
      {action === 'check-in' ? <CheckInDrawer tool={tool} onClose={() => setAction(null)} /> : null}
      {action === 'calibrate' ? <CalibrationDrawer detail={data} onClose={() => setAction(null)} /> : null}
      {action === 'InStore' || action === 'UnderRepair' || action === 'Lost' || action === 'Retired' ? <StatusDrawer tool={tool} target={action} onClose={() => setAction(null)} /> : null}
    </div>
  )
}

/** The tools a technician holds, for their profile page. */
export function TechnicianToolsSection({ technicianId }: { technicianId: string }) {
  const { data, isLoading, isError } = useTools({ technicianId })
  const total = (data ?? []).reduce((sum, tool) => sum + (tool.purchaseCost ?? 0), 0)
  return (
    <section className="space-y-2">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900"><Wrench className="h-4 w-4 text-slate-500" />Tools held</h2>
        {data && data.length > 0 ? <span className="text-sm text-slate-500">{data.length} tool{data.length === 1 ? '' : 's'} · {formatCurrency(total)}</span> : null}
      </div>
      <DataTable
        columns={toolColumns.filter((column) => column.key !== 'holder' && column.key !== 'status')}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Tools unavailable' : 'No tools checked out'}
        emptyDescription={isError ? 'The tools this technician holds could not be loaded.' : 'Tools checked out to this technician appear here.'}
      />
    </section>
  )
}
