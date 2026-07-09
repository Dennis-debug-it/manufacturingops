import { useState } from 'react'
import toast from 'react-hot-toast'
import { UserCog } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import { useAssignmentHistory, useReassignWorkOrder } from '../api'
import { useAssignmentGroupOptions, useTechnicianOptions } from '../pickerApi'
import type { WorkOrder } from '../types'

export function AssignmentTab({ workOrder }: { workOrder: WorkOrder }) {
  const canAssign = usePermission('work-orders.assign')
  const { data: history, isLoading, isError } = useAssignmentHistory(workOrder.id)
  const { data: technicians } = useTechnicianOptions()
  const { data: groups } = useAssignmentGroupOptions()
  const reassign = useReassignWorkOrder(workOrder.id)

  const [open, setOpen] = useState(false)
  const [groupId, setGroupId] = useState('')
  const [technicianId, setTechnicianId] = useState('')
  const [notes, setNotes] = useState('')

  const closeDrawer = () => {
    setOpen(false)
    setGroupId('')
    setTechnicianId('')
    setNotes('')
  }

  const submitReassign = async () => {
    try {
      await reassign.mutateAsync({
        assignmentGroupId: groupId || null,
        technicianIds: technicianId ? [technicianId] : null,
        leadTechnicianId: technicianId || null,
        notes: notes.trim() || null,
      })
      toast.success('Work order reassigned')
      closeDrawer()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-6">
      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Current Assignment</div>
            <div className="mt-2 text-lg font-semibold text-slate-900">{workOrder.assignmentSummary || 'Unassigned'}</div>
            <div className="mt-1"><StatusBadge status={workOrder.assignmentStatus || 'Unassigned'} /></div>
            {workOrder.technicianAssignments.length > 0 ? (
              <ul className="mt-3 space-y-1 text-sm text-slate-600">
                {workOrder.technicianAssignments.map((assignment) => (
                  <li key={assignment.id}>
                    {assignment.technicianName ?? 'Technician'} {assignment.isLead ? '(Lead)' : ''} — {assignment.status}
                  </li>
                ))}
              </ul>
            ) : null}
          </div>
          {canAssign ? (
            <button
              type="button"
              onClick={() => setOpen(true)}
              className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800"
            >
              <UserCog className="h-4 w-4" />
              Reassign
            </button>
          ) : null}
        </div>
      </div>

      <div>
        <div className="mb-3 text-sm font-semibold text-slate-900">Assignment History</div>
        {isLoading ? (
          <Spinner label="Loading assignment history" />
        ) : isError ? (
          <EmptyState title="History unavailable" description="Assignment history could not be loaded." />
        ) : !history || history.length === 0 ? (
          <EmptyState title="No assignment history" description="Reassignments and status changes will appear here." />
        ) : (
          <ol className="space-y-3">
            {history.map((item) => (
              <li key={item.id} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="text-sm font-medium text-slate-900">{item.action}</span>
                  <span className="text-xs text-slate-500">{formatDateTime(item.performedAt)}</span>
                </div>
                <p className="mt-2 text-sm text-slate-600">
                  {item.fromGroupName ?? item.fromTechnicianName ?? '—'} → {item.toGroupName ?? item.toTechnicianName ?? '—'}
                </p>
                {item.notes ? <p className="mt-1 text-xs text-slate-500">{item.notes}</p> : null}
                <p className="mt-1 text-xs text-slate-500">By {item.performedByUserName ?? 'System'}</p>
              </li>
            ))}
          </ol>
        )}
      </div>

      <FormDrawer
        open={open}
        title="Reassign Work Order"
        onClose={closeDrawer}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={closeDrawer} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Cancel
            </button>
            <button
              type="button"
              onClick={submitReassign}
              disabled={reassign.isPending}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {reassign.isPending ? 'Reassigning…' : 'Reassign'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="assign-group">Assignment Group</label>
            <select id="assign-group" value={groupId} onChange={(event) => setGroupId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option value="">No group</option>
              {(groups ?? []).map((group) => (
                <option key={group.id} value={group.id}>{group.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="assign-technician">Technician</label>
            <select id="assign-technician" value={technicianId} onChange={(event) => setTechnicianId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option value="">Unassigned</option>
              {(technicians ?? []).map((technician) => (
                <option key={technician.id} value={technician.id}>{technician.fullName}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="assign-notes">Notes</label>
            <textarea id="assign-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
