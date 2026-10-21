import { useState } from 'react'
import toast from 'react-hot-toast'
import { Link } from 'react-router-dom'
import { Link2, Unlink } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatCurrency } from '../../../shared/utils/format'
import { formatDate } from '../../../shared/utils/dates'
import { useWorkOrders } from '../../work-orders/api'
import { useMaterialIssues } from '../../inventory/api'
import { useLinkMaterialIssue, useLinkWorkOrder, useProjectMaterialIssues, useProjectWorkOrders, useUnlinkMaterialIssue, useUnlinkWorkOrder } from '../api'
import type { ProjectDetail, ProjectMaterialIssue, ProjectWorkOrder } from '../types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass } from './formStyles'

/**
 * Work orders and material issues stay in their own modules; a project only links to them.
 * Linking is limited to records for the same client.
 */
export function LinkedRecordsTab({ project, readOnly, showMaterials }: { project: ProjectDetail; readOnly: boolean; showMaterials: boolean }) {
  const projectId = project.summary.id
  const canViewMaterials = usePermission('materials.view')
  const { data: workOrders, isLoading: loadingWorkOrders } = useProjectWorkOrders(projectId)
  const { data: issues, isLoading: loadingIssues } = useProjectMaterialIssues(projectId, showMaterials)
  const linkWorkOrder = useLinkWorkOrder(projectId)
  const unlinkWorkOrder = useUnlinkWorkOrder(projectId)
  const linkIssue = useLinkMaterialIssue(projectId)
  const unlinkIssue = useUnlinkMaterialIssue(projectId)
  const [picker, setPicker] = useState<'work-order' | 'issue' | null>(null)
  const [selected, setSelected] = useState('')
  const { data: candidateWorkOrders } = useWorkOrders(undefined, picker === 'work-order')
  const { data: candidateIssues } = useMaterialIssues(undefined, picker === 'issue')

  const linkedWorkOrders = new Set((workOrders ?? []).map((w) => w.id))
  const linkedIssues = new Set((issues ?? []).map((i) => i.id))
  const workOrderOptions = (candidateWorkOrders ?? []).filter((w) => w.clientId === project.summary.clientId && !linkedWorkOrders.has(w.id))
  const issueOptions = (candidateIssues ?? []).filter((i) => (!i.clientId || i.clientId === project.summary.clientId) && !linkedIssues.has(i.id))

  const link = async () => {
    if (!selected || !picker) return
    try {
      if (picker === 'work-order') await linkWorkOrder.mutateAsync(selected)
      else await linkIssue.mutateAsync(selected)
      toast.success('Linked to the project')
      setPicker(null)
    } catch {
      // toast from the API client
    }
  }

  const workOrderColumns: DataTableColumn<ProjectWorkOrder>[] = [
    { key: 'workOrderNumber', header: 'Work order', cell: (row) => (<div><Link to={`/work-orders/${row.id}`} className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4">{row.workOrderNumber}</Link><div className="text-xs text-slate-500">{row.title}</div></div>) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'priority', header: 'Priority', cell: (row) => row.priority },
    { key: 'dueDate', header: 'Due', cell: (row) => (row.dueDate ? formatDate(row.dueDate) : '—') },
    { key: 'assignedTechnicianName', header: 'Technician', cell: (row) => row.assignedTechnicianName ?? '—' },
    ...(readOnly ? [] : [{
      key: 'actions',
      header: '',
      cell: (row: ProjectWorkOrder) => (
        <div className="flex justify-end">
          <button type="button" aria-label={`Unlink ${row.workOrderNumber}`} className={smallButtonClass} onClick={() => unlinkWorkOrder.mutate(row.id, { onSuccess: () => toast.success('Unlinked') })}><Unlink className="h-3.5 w-3.5" /></button>
        </div>
      ),
    }]),
  ]

  const issueColumns: DataTableColumn<ProjectMaterialIssue>[] = [
    { key: 'issueNumber', header: 'Issue', cell: (row) => <span className="font-mono">{row.issueNumber}</span> },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'issuedAt', header: 'Issued', cell: (row) => (row.issuedAt ? formatDate(row.issuedAt) : '—') },
    { key: 'lineCount', header: 'Lines', cell: (row) => row.lineCount },
    { key: 'totalIssuedCost', header: 'Cost', cell: (row) => formatCurrency(row.totalIssuedCost, project.summary.currency) },
    ...(readOnly ? [] : [{
      key: 'actions',
      header: '',
      cell: (row: ProjectMaterialIssue) => (
        <div className="flex justify-end">
          <button type="button" aria-label={`Unlink ${row.issueNumber}`} className={smallButtonClass} onClick={() => unlinkIssue.mutate(row.id, { onSuccess: () => toast.success('Unlinked') })}><Unlink className="h-3.5 w-3.5" /></button>
        </div>
      ),
    }]),
  ]

  const openPicker = (kind: 'work-order' | 'issue') => {
    setSelected('')
    setPicker(kind)
  }

  return (
    <div className="space-y-4">
      <SummarySection title="Work orders" description="Snag lists, commissioning visits and call-outs done for this project.">
        {!readOnly ? (
          <div className="mb-3 flex justify-end">
            <button type="button" className={primaryButtonClass} onClick={() => openPicker('work-order')}><Link2 className="h-4 w-4" />Link work order</button>
          </div>
        ) : null}
        <DataTable columns={workOrderColumns} data={workOrders ?? []} loading={loadingWorkOrders} emptyTitle="No linked work orders" emptyDescription="Link this client's work orders to the project." />
      </SummarySection>

      {showMaterials ? (
        <SummarySection title="Material issues" description="Stock issued to this project. Issuing still happens in Inventory.">
          {!readOnly && canViewMaterials ? (
            <div className="mb-3 flex justify-end">
              <button type="button" className={primaryButtonClass} onClick={() => openPicker('issue')}><Link2 className="h-4 w-4" />Link material issue</button>
            </div>
          ) : null}
          <DataTable columns={issueColumns} data={issues ?? []} loading={loadingIssues} emptyTitle="No linked material issues" emptyDescription="Link material issues made for this project." />
        </SummarySection>
      ) : null}

      <FormDrawer
        open={picker !== null}
        title={picker === 'work-order' ? 'Link a work order' : 'Link a material issue'}
        onClose={() => setPicker(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setPicker(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!selected || linkWorkOrder.isPending || linkIssue.isPending} onClick={() => void link()}>Link</button>
          </div>
        }
      >
        <div>
          <label className={labelClass} htmlFor="link-record">{picker === 'work-order' ? `Work orders for ${project.summary.clientName}` : 'Material issues'}</label>
          <select id="link-record" value={selected} onChange={(event) => setSelected(event.target.value)} className={fieldClass}>
            <option value="">Select one</option>
            {picker === 'work-order'
              ? workOrderOptions.map((w) => (<option key={w.id} value={w.id}>{w.workOrderNumber} · {w.title}</option>))
              : issueOptions.map((i) => (<option key={i.id} value={i.id}>{i.issueNumber}{i.workOrderNumber ? ` · ${i.workOrderNumber}` : ''}</option>))}
          </select>
          <p className="mt-2 text-xs text-slate-500">Only records for this project's client that aren't on another project can be linked.</p>
        </div>
      </FormDrawer>
    </div>
  )
}
