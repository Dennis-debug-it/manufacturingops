import { useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useReassignWorkOrder } from '../api'
import { useAssignmentGroupOptions, useTechnicianOptions } from '../pickerApi'

export interface ReassignDrawerProps {
  workOrderId: string
  open: boolean
  onClose: () => void
}

export function ReassignDrawer({ workOrderId, open, onClose }: ReassignDrawerProps) {
  const { data: technicians } = useTechnicianOptions()
  const { data: groups } = useAssignmentGroupOptions()
  const reassign = useReassignWorkOrder(workOrderId)

  const [groupId, setGroupId] = useState('')
  const [technicianIds, setTechnicianIds] = useState<string[]>([])
  const [leadTechnicianId, setLeadTechnicianId] = useState('')
  const [notes, setNotes] = useState('')

  const closeDrawer = () => {
    setGroupId('')
    setTechnicianIds([])
    setLeadTechnicianId('')
    setNotes('')
    onClose()
  }

  const toggleTechnician = (id: string) => {
    setTechnicianIds((current) => {
      const next = current.includes(id) ? current.filter((x) => x !== id) : [...current, id]
      setLeadTechnicianId((lead) => (next.includes(lead) ? lead : next[0] ?? ''))
      return next
    })
  }

  const submitReassign = async () => {
    try {
      await reassign.mutateAsync({
        assignmentGroupId: groupId || null,
        technicianIds: technicianIds.length > 0 ? technicianIds : null,
        leadTechnicianId: leadTechnicianId || technicianIds[0] || null,
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
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="assign-group">Assignment Group</label>
          <select id="assign-group" value={groupId} onChange={(event) => setGroupId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
            <option value="">No group</option>
            {(groups ?? []).map((group) => (
              <option key={group.id} value={group.id}>{group.name}</option>
            ))}
          </select>
        </div>
        <div>
          <span className="mb-1.5 block text-sm font-medium text-slate-700">Technicians</span>
          <div className="max-h-56 space-y-1 overflow-y-auto rounded-lg border border-slate-300 p-2">
            {(technicians ?? []).length === 0 ? (
              <p className="px-1 py-2 text-sm text-slate-500">No technicians available.</p>
            ) : (
              (technicians ?? []).map((technician) => (
                <label key={technician.id} className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50">
                  <input
                    type="checkbox"
                    checked={technicianIds.includes(technician.id)}
                    onChange={() => toggleTechnician(technician.id)}
                    className="h-4 w-4 rounded border-slate-300"
                  />
                  {technician.fullName}
                </label>
              ))
            )}
          </div>
          <p className="mt-1 text-xs text-slate-500">Select one or more technicians. Leave empty to assign to a group only.</p>
        </div>
        {technicianIds.length > 1 ? (
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="assign-lead">Lead Technician</label>
            <select id="assign-lead" value={leadTechnicianId} onChange={(event) => setLeadTechnicianId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              {technicianIds.map((id) => {
                const technician = (technicians ?? []).find((t) => t.id === id)
                return <option key={id} value={id}>{technician?.fullName ?? id}</option>
              })}
            </select>
          </div>
        ) : null}
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="assign-notes">Notes</label>
          <textarea id="assign-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
      </div>
    </FormDrawer>
  )
}
