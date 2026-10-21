import { useState } from 'react'
import toast from 'react-hot-toast'
import { Link } from 'react-router-dom'
import { Plus, Trash2 } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { useAddMember, useConsultants, useLinkConsultant, useProjectPeople, useProjectSettings, useRemoveMember, useUnlinkConsultant } from '../api'
import { memberRoleLabels, type MemberRole, type ProjectConsultantLink, type ProjectDetail, type ProjectMember } from '../types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textOrNull } from './formStyles'

const roles: MemberRole[] = ['ProjectManager', 'Supervisor', 'Member', 'Viewer']

export function TeamTab({ project, readOnly }: { project: ProjectDetail; readOnly: boolean }) {
  const projectId = project.summary.id
  const addMember = useAddMember(projectId)
  const removeMember = useRemoveMember(projectId)
  const linkConsultant = useLinkConsultant(projectId)
  const unlinkConsultant = useUnlinkConsultant(projectId)
  const [memberOpen, setMemberOpen] = useState(false)
  const [consultantOpen, setConsultantOpen] = useState(false)
  const { data: people } = useProjectPeople(memberOpen)
  const { data: settings } = useProjectSettings()
  const { data: directory } = useConsultants(undefined, false)
  const [person, setPerson] = useState('')
  const [role, setRole] = useState<MemberRole>('Member')
  const [trade, setTrade] = useState('')
  const [consultantId, setConsultantId] = useState('')
  const [consultantRole, setConsultantRole] = useState('')

  const onTeam = new Set(project.members.flatMap((m) => [m.userId, m.technicianId].filter(Boolean) as string[]))
  // A person is either a user (office) or a technician (field). Technicians linked to a login count as that user.
  const options = [
    ...(people?.users ?? []).filter((u) => !onTeam.has(u.id)).map((u) => ({ key: `user:${u.id}`, label: `${u.fullName}${u.jobTitle ? ` · ${u.jobTitle}` : ''}` })),
    ...(people?.technicians ?? []).filter((t) => !onTeam.has(t.id) && !(t.userId && onTeam.has(t.userId)) && !(t.userId && people?.users.some((u) => u.id === t.userId)))
      .map((t) => ({ key: `tech:${t.id}`, label: `${t.fullName} · technician${t.skillCategory ? ` (${t.skillCategory})` : ''}` })),
  ]

  const saveMember = async () => {
    if (!person) return
    const [kind, id = ''] = person.split(':')
    try {
      await addMember.mutateAsync({ ...(kind === 'user' ? { userId: id } : { technicianId: id }), role, trade: textOrNull(trade), fromDate: null, toDate: null })
      toast.success('Added to the team')
      setMemberOpen(false)
    } catch {
      // toast from the API client
    }
  }

  const saveConsultant = async () => {
    if (!consultantId) return
    try {
      await linkConsultant.mutateAsync({ consultantId, roleOnProject: textOrNull(consultantRole) })
      toast.success('Consultant added')
      setConsultantOpen(false)
    } catch {
      // toast from the API client
    }
  }

  const memberColumns: DataTableColumn<ProjectMember>[] = [
    { key: 'name', header: 'Name', cell: (row) => (<div><div className="font-medium text-slate-900">{row.name}</div>{row.email ? <div className="text-xs text-slate-500">{row.email}</div> : null}</div>) },
    { key: 'role', header: 'Role', cell: (row) => memberRoleLabels[row.role] ?? row.role },
    { key: 'trade', header: 'Trade', cell: (row) => row.trade ?? '—' },
    ...(readOnly ? [] : [{
      key: 'actions',
      header: '',
      cell: (row: ProjectMember) => row.userId && row.userId === project.summary.projectManagerUserId ? null : (
        <div className="flex justify-end">
          <button type="button" aria-label={`Remove ${row.name}`} className={smallButtonClass} onClick={() => removeMember.mutate(row.id, { onSuccess: () => toast.success('Removed from the team') })}><Trash2 className="h-3.5 w-3.5" /></button>
        </div>
      ),
    }]),
  ]

  const consultantColumns: DataTableColumn<ProjectConsultantLink>[] = [
    { key: 'firmName', header: 'Firm', cell: (row) => (<div><div className="font-medium text-slate-900">{row.firmName}</div>{row.discipline ? <div className="text-xs text-slate-500">{row.discipline}</div> : null}</div>) },
    { key: 'roleOnProject', header: 'Role on project', cell: (row) => row.roleOnProject ?? '—' },
    { key: 'contact', header: 'Contact', cell: (row) => [row.contactPerson, row.phone, row.email].filter(Boolean).join(' · ') || '—' },
    ...(readOnly ? [] : [{
      key: 'actions',
      header: '',
      cell: (row: ProjectConsultantLink) => (
        <div className="flex justify-end">
          <button type="button" aria-label={`Remove ${row.firmName}`} className={smallButtonClass} onClick={() => unlinkConsultant.mutate(row.id, { onSuccess: () => toast.success('Consultant removed') })}><Trash2 className="h-3.5 w-3.5" /></button>
        </div>
      ),
    }]),
  ]

  const linkedConsultants = new Set(project.consultants.map((c) => c.consultantId))

  return (
    <div className="space-y-4">
      <SummarySection title="Team" description="Office users and field technicians working on this project.">
        {!readOnly ? (
          <div className="mb-3 flex justify-end">
            <button type="button" className={primaryButtonClass} onClick={() => { setPerson(''); setRole('Member'); setTrade(''); setMemberOpen(true) }}><Plus className="h-4 w-4" />Add person</button>
          </div>
        ) : null}
        <DataTable columns={memberColumns} data={project.members} emptyTitle="No team yet" emptyDescription="Add the project manager, supervisors and technicians." />
      </SummarySection>

      <SummarySection title="Consultants" description="Firms from the consultant directory, such as the M&E engineer or QS.">
        {!readOnly ? (
          <div className="mb-3 flex flex-wrap justify-end gap-2">
            <Link to="/projects/consultants" className={secondaryButtonClass}>Manage directory</Link>
            <button type="button" className={primaryButtonClass} onClick={() => { setConsultantId(''); setConsultantRole(''); setConsultantOpen(true) }}><Plus className="h-4 w-4" />Add consultant</button>
          </div>
        ) : null}
        <DataTable columns={consultantColumns} data={project.consultants} emptyTitle="No consultants" emptyDescription="Link consultants from the directory." />
      </SummarySection>

      <FormDrawer
        open={memberOpen}
        title="Add to the team"
        onClose={() => setMemberOpen(false)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setMemberOpen(false)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!person || addMember.isPending} onClick={() => void saveMember()}>Add</button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className={labelClass} htmlFor="member-person">Person</label>
            <select id="member-person" value={person} onChange={(event) => setPerson(event.target.value)} className={fieldClass}>
              <option value="">Select a person</option>
              {options.map((option) => (<option key={option.key} value={option.key}>{option.label}</option>))}
            </select>
          </div>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className={labelClass} htmlFor="member-role">Role</label>
              <select id="member-role" value={role} onChange={(event) => setRole(event.target.value as MemberRole)} className={fieldClass}>
                {roles.map((r) => (<option key={r} value={r}>{memberRoleLabels[r]}</option>))}
              </select>
            </div>
            <div>
              <label className={labelClass} htmlFor="member-trade">Trade</label>
              {settings?.trades.length ? (
                <select id="member-trade" value={trade} onChange={(event) => setTrade(event.target.value)} className={fieldClass}>
                  <option value="">None</option>
                  {settings.trades.map((t) => (<option key={t} value={t}>{t}</option>))}
                </select>
              ) : (
                <input id="member-trade" value={trade} onChange={(event) => setTrade(event.target.value)} placeholder="e.g. HVAC, Electrical" className={fieldClass} />
              )}
            </div>
          </div>
        </div>
      </FormDrawer>

      <FormDrawer
        open={consultantOpen}
        title="Add consultant"
        onClose={() => setConsultantOpen(false)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setConsultantOpen(false)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!consultantId || linkConsultant.isPending} onClick={() => void saveConsultant()}>Add</button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className={labelClass} htmlFor="consultant-firm">Consultant</label>
            <select id="consultant-firm" value={consultantId} onChange={(event) => setConsultantId(event.target.value)} className={fieldClass}>
              <option value="">Select from the directory</option>
              {(directory ?? []).filter((c) => !linkedConsultants.has(c.id)).map((c) => (<option key={c.id} value={c.id}>{c.firmName}{c.discipline ? ` (${c.discipline})` : ''}</option>))}
            </select>
            {(directory ?? []).length === 0 ? <p className="mt-1 text-xs text-slate-500">The directory is empty. <Link className="underline" to="/projects/consultants">Add consultants</Link> first.</p> : null}
          </div>
          <div>
            <label className={labelClass} htmlFor="consultant-role">Role on this project</label>
            <input id="consultant-role" value={consultantRole} onChange={(event) => setConsultantRole(event.target.value)} placeholder="e.g. Lead consultant, QS" className={fieldClass} />
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
