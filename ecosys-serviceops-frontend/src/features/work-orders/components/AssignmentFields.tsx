import { useMemo } from 'react'
import { useAssignmentGroupOptions, useTechnicianOptions } from '../pickerApi'

export interface AssignmentValue {
  groupId: string
  technicianIds: string[]
  leadTechnicianId: string
}

export const emptyAssignment: AssignmentValue = { groupId: '', technicianIds: [], leadTechnicianId: '' }

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

/** The lead defaults to the first technician picked; a lone technician leads implicitly. */
export function effectiveLead(value: AssignmentValue) {
  return value.technicianIds.includes(value.leadTechnicianId) ? value.leadTechnicianId : value.technicianIds[0] ?? ''
}

/** Maps the picker state to the create-work-order assignment fields the API expects. */
export function toCreateAssignmentPayload(value: AssignmentValue) {
  const lead = effectiveLead(value) || null
  if (value.groupId) {
    return {
      assignmentType: 'AssignmentGroup',
      assignmentGroupId: value.groupId,
      assignedTechnicianIds: value.technicianIds.length > 0 ? value.technicianIds : null,
      leadTechnicianId: lead,
    }
  }
  if (value.technicianIds.length === 1) {
    return { assignmentType: 'IndividualTechnician', assignedTechnicianId: value.technicianIds[0]!, leadTechnicianId: null }
  }
  if (value.technicianIds.length > 1) {
    return { assignmentType: 'MultipleTechnicians', assignedTechnicianIds: value.technicianIds, leadTechnicianId: lead }
  }
  return { assignmentType: 'Unassigned' }
}

export interface AssignmentFieldsProps {
  value: AssignmentValue
  onChange: (value: AssignmentValue) => void
  idPrefix: string
}

/**
 * Team, technicians and lead in one place. Picking a team narrows the technician list to that
 * team's members, because the server only accepts technicians who belong to the chosen team.
 */
export function AssignmentFields({ value, onChange, idPrefix }: AssignmentFieldsProps) {
  const { data: technicians, isLoading: techniciansLoading } = useTechnicianOptions()
  const { data: groups } = useAssignmentGroupOptions()

  const activeGroups = useMemo(() => (groups ?? []).filter((group) => group.isActive !== false), [groups])
  const selectedGroup = activeGroups.find((group) => group.id === value.groupId)
  const memberIds = useMemo(
    () => (selectedGroup?.members ? new Set(selectedGroup.members.filter((m) => m.isActive).map((m) => m.technicianId)) : null),
    [selectedGroup],
  )
  const choices = (technicians ?? []).filter((technician) => !memberIds || memberIds.has(technician.id))
  const nameOf = (id: string) => (technicians ?? []).find((t) => t.id === id)?.fullName ?? id

  const changeGroup = (groupId: string) => {
    const group = activeGroups.find((item) => item.id === groupId)
    const allowed = group?.members ? new Set(group.members.filter((m) => m.isActive).map((m) => m.technicianId)) : null
    const technicianIds = allowed ? value.technicianIds.filter((id) => allowed.has(id)) : value.technicianIds
    // Prefer the team's own lead when they are picked.
    const teamLead = group?.members?.find((m) => m.isLead && m.isActive && technicianIds.includes(m.technicianId))?.technicianId
    onChange({ groupId, technicianIds, leadTechnicianId: teamLead ?? (technicianIds.includes(value.leadTechnicianId) ? value.leadTechnicianId : '') })
  }

  const toggleTechnician = (id: string) => {
    const technicianIds = value.technicianIds.includes(id) ? value.technicianIds.filter((x) => x !== id) : [...value.technicianIds, id]
    onChange({ ...value, technicianIds, leadTechnicianId: technicianIds.includes(value.leadTechnicianId) ? value.leadTechnicianId : '' })
  }

  const summary = value.technicianIds.length === 0
    ? selectedGroup ? `Assigned to ${selectedGroup.name} (no technician yet)` : 'Unassigned'
    : value.technicianIds.length === 1
      ? `Assigned to ${nameOf(value.technicianIds[0]!)}${selectedGroup ? ` · ${selectedGroup.name}` : ''}`
      : `${value.technicianIds.length} technicians, lead ${nameOf(effectiveLead(value))}${selectedGroup ? ` · ${selectedGroup.name}` : ''}`

  return (
    <fieldset className="space-y-3 rounded-xl border border-slate-200 p-4">
      <legend className="px-1 text-sm font-semibold text-slate-900">Assign</legend>
      {activeGroups.length > 0 ? (
        <div>
          <label className={labelClass} htmlFor={`${idPrefix}-team`}>Team</label>
          <select id={`${idPrefix}-team`} value={value.groupId} onChange={(event) => changeGroup(event.target.value)} className={fieldClass}>
            <option value="">No team</option>
            {activeGroups.map((group) => (
              <option key={group.id} value={group.id}>{group.name}</option>
            ))}
          </select>
        </div>
      ) : null}
      <div>
        <span className={labelClass} id={`${idPrefix}-technicians-label`}>Technicians</span>
        <div role="group" aria-labelledby={`${idPrefix}-technicians-label`} className="max-h-48 space-y-0.5 overflow-y-auto rounded-lg border border-slate-300 p-1.5">
          {techniciansLoading ? (
            <p className="px-2 py-1.5 text-sm text-slate-500">Loading technicians…</p>
          ) : choices.length === 0 ? (
            <p className="px-2 py-1.5 text-sm text-slate-500">{selectedGroup ? 'This team has no active members.' : 'No technicians available.'}</p>
          ) : (
            choices.map((technician) => (
              <label key={technician.id} className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50">
                <input
                  type="checkbox"
                  checked={value.technicianIds.includes(technician.id)}
                  onChange={() => toggleTechnician(technician.id)}
                  className="h-4 w-4 rounded border-slate-300"
                />
                <span className="flex-1">{technician.fullName}</span>
                {technician.skillCategory ? <span className="text-xs text-slate-400">{technician.skillCategory}</span> : null}
              </label>
            ))
          )}
        </div>
      </div>
      {value.technicianIds.length > 1 ? (
        <div>
          <label className={labelClass} htmlFor={`${idPrefix}-lead`}>Lead technician</label>
          <select id={`${idPrefix}-lead`} value={effectiveLead(value)} onChange={(event) => onChange({ ...value, leadTechnicianId: event.target.value })} className={fieldClass}>
            {value.technicianIds.map((id) => (
              <option key={id} value={id}>{nameOf(id)}</option>
            ))}
          </select>
        </div>
      ) : null}
      <p className="text-xs text-slate-500">{summary}</p>
    </fieldset>
  )
}
