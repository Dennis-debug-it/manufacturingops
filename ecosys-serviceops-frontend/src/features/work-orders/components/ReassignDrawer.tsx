import { useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useReassignWorkOrder } from '../api'
import { AssignmentFields, effectiveLead, emptyAssignment, type AssignmentValue } from './AssignmentFields'

export interface ReassignDrawerProps {
  workOrderId: string
  open: boolean
  onClose: () => void
}

export function ReassignDrawer({ workOrderId, open, onClose }: ReassignDrawerProps) {
  const reassign = useReassignWorkOrder(workOrderId)
  const [assignment, setAssignment] = useState<AssignmentValue>(emptyAssignment)
  const [notes, setNotes] = useState('')

  const closeDrawer = () => {
    setAssignment(emptyAssignment)
    setNotes('')
    onClose()
  }

  const submitReassign = async () => {
    try {
      await reassign.mutateAsync({
        assignmentGroupId: assignment.groupId || null,
        technicianIds: assignment.technicianIds.length > 0 ? assignment.technicianIds : null,
        leadTechnicianId: effectiveLead(assignment) || null,
        notes: notes.trim() || null,
      })
      toast.success('Work order reassigned')
      closeDrawer()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
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
            className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {reassign.isPending ? 'Reassigning…' : 'Reassign'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <AssignmentFields idPrefix="reassign" value={assignment} onChange={setAssignment} />
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="assign-notes">Notes</label>
          <textarea id="assign-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
      </div>
    </FormDrawer>
  )
}
