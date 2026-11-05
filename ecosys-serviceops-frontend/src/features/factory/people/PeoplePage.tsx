import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ChevronLeft, ChevronRight, Copy, Plus, ShieldCheck, Users, X } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { Field, LocationSelect, formatNumber, inputClass, primaryButton, secondaryButton } from '../components/ui'
import { TRADES, useCertificateTypes, useCertificates, usePeopleAction, useRoster, useTeam, useWorkload, type CertificateStatus, type RosterCell, type TeamMember } from './api'

const TABS = [{ id: 'team', label: 'Team' }, { id: 'certificates', label: 'Certificates' }, { id: 'roster', label: 'Roster' }, { id: 'workload', label: 'Workload' }] as const
type Tab = (typeof TABS)[number]['id']

export const CERT_TONES: Record<CertificateStatus, BadgeTone> = { Valid: 'success', Expiring: 'warning', Expired: 'danger' }
const CERT_LABELS: Record<CertificateStatus, string> = { Valid: 'Valid', Expiring: 'Expiring', Expired: 'Expired' }

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const mondayOf = (d: Date) => { const x = new Date(d); x.setDate(x.getDate() - ((x.getDay() + 6) % 7)); x.setHours(0, 0, 0, 0); return x }
const addDays = (d: Date, n: number) => { const x = new Date(d); x.setDate(x.getDate() + n); return x }

/** The maintenance team: trades and skills, certificates, the roster and the workload. */
export function PeoplePage() {
  const [params, setParams] = useSearchParams()
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'team') as Tab
  return (
    <div className="space-y-4">
      <PageHeader title="Maintenance team" subtitle="Trades and skills, licences and tickets, who is on which shift, and how loaded each person is." />
      <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {TABS.map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setParams(item.id === 'team' ? {} : { tab: item.id }, { replace: true })}
            className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
        ))}
      </div>
      {tab === 'team' ? <TeamTab /> : tab === 'certificates' ? <CertificatesTab /> : tab === 'roster' ? <RosterTab /> : <WorkloadTab />}
    </div>
  )
}

function TeamTab() {
  const team = useTeam()
  const [open, setOpen] = useState<TeamMember | null>(null)
  if (team.isLoading) return <Spinner />
  const rows = team.data ?? []
  if (rows.length === 0) return <EmptyState icon={Users} title="No technicians" description="Add technicians in Users & Access." />
  return (
    <>
      <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
        <table className="w-full min-w-[760px] text-sm">
          <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Name</th><th className="px-4 py-2">Trade</th><th className="px-4 py-2">Works in</th><th className="px-4 py-2">Skills</th><th className="px-4 py-2">Certificates</th><th className="px-4 py-2 text-right">Rate / h</th></tr></thead>
          <tbody className="divide-y divide-slate-100">
            {rows.map((m) => (
              <tr key={m.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(m)}>
                <td className="px-4 py-2"><span className="font-medium text-slate-900">{m.fullName}</span><span className="block text-xs text-slate-500">{m.phone ?? m.email}</span></td>
                <td className="px-4 py-2 text-slate-700">{m.trade ?? <span className="text-slate-400">—</span>}</td>
                <td className="px-4 py-2 text-slate-600">{m.homeOrgNodeName ?? '—'}</td>
                <td className="px-4 py-2 text-xs text-slate-600">{m.skills.length ? m.skills.map((s) => `${s.skill}${s.level === 3 ? ' ★' : ''}`).join(', ') : '—'}</td>
                <td className="px-4 py-2">
                  <span className="flex flex-wrap gap-1">
                    {m.certificates.length === 0 ? <span className="text-slate-400">—</span> : m.certificates.map((c) => <Badge key={c.id} size="sm" tone={CERT_TONES[c.status]}>{c.name}</Badge>)}
                  </span>
                </td>
                <td className="px-4 py-2 text-right tabular-nums">{m.hourlyRate != null ? formatNumber(m.hourlyRate) : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ProfileDrawer member={open} onClose={() => setOpen(null)} />
    </>
  )
}

function ProfileDrawer({ member, onClose }: { member: TeamMember | null; onClose: () => void }) {
  const canManage = usePermission('work-orders.assign')
  const action = usePeopleAction()
  const types = useCertificateTypes()
  const team = useTeam()
  const current = team.data?.find((x) => x.id === member?.id) ?? member
  const [form, setForm] = useState({ trade: '', home: '', rate: '', overtime: '1.5' })
  const [skills, setSkills] = useState<{ skill: string; level: number }[]>([])
  const [newSkill, setNewSkill] = useState('')
  const [cert, setCert] = useState({ typeId: '', number: '', issued: '', expires: '' })

  useEffect(() => {
    if (!member) return
    setForm({ trade: member.trade ?? '', home: member.homeOrgNodeId ?? '', rate: member.hourlyRate?.toString() ?? '', overtime: String(member.overtimeMultiplier) })
    setSkills(member.skills)
    setCert({ typeId: '', number: '', issued: '', expires: '' })
  }, [member])

  const save = async () => {
    if (!member) return
    try {
      await action.mutateAsync({ method: 'put', path: `/technicians/${member.id}`, body: { trade: form.trade || null, homeOrgNodeId: form.home || null, hourlyRate: form.rate ? Number(form.rate) : null, overtimeMultiplier: Number(form.overtime) || 1.5, skills } })
      toast.success('Saved')
    } catch {
      // The API client shows the error.
    }
  }

  const addCertificate = async () => {
    if (!member) return
    try {
      await action.mutateAsync({ path: `/technicians/${member.id}/certificates`, body: { certificationDefinitionId: cert.typeId, certificateNumber: cert.number || null, issuedOn: cert.issued || null, expiresOn: cert.expires || null, notes: null } })
      toast.success('Certificate added')
      setCert({ typeId: '', number: '', issued: '', expires: '' })
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open={member !== null} title={member?.fullName ?? ''} onClose={onClose} footer={canManage ? (
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Close</button><button type="button" className={primaryButton} disabled={action.isPending} onClick={() => void save()}>Save profile</button></div>
    ) : undefined}>
      {current ? (
        <div className="space-y-5 text-sm">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Trade">
              <select className={inputClass} value={form.trade} disabled={!canManage} onChange={(event) => setForm({ ...form, trade: event.target.value })}>
                <option value="">—</option>{TRADES.map((t) => <option key={t}>{t}</option>)}
              </select>
            </Field>
            <Field label="Works in"><LocationSelect value={form.home} onChange={(value) => setForm({ ...form, home: value })} allowEmpty emptyLabel="Anywhere" includeGroups /></Field>
            <Field label="Hourly rate"><input type="number" min={0} className={inputClass} value={form.rate} disabled={!canManage} onChange={(event) => setForm({ ...form, rate: event.target.value })} /></Field>
            <Field label="Overtime ×" hint="Outside the rostered shift."><input type="number" min={1} max={4} step={0.25} className={inputClass} value={form.overtime} disabled={!canManage} onChange={(event) => setForm({ ...form, overtime: event.target.value })} /></Field>
          </div>

          <section>
            <h3 className="mb-2 font-semibold text-slate-800">Skills</h3>
            <ul className="space-y-1">
              {skills.map((s, i) => (
                <li key={s.skill} className="flex items-center gap-2">
                  <span className="flex-1">{s.skill}</span>
                  <select aria-label={`${s.skill} level`} className={cn(inputClass, 'h-8 w-36')} value={s.level} disabled={!canManage} onChange={(event) => setSkills(skills.map((x, j) => (j === i ? { ...x, level: Number(event.target.value) } : x)))}>
                    <option value={1}>Learning</option><option value={2}>Competent</option><option value={3}>Can train others</option>
                  </select>
                  {canManage ? <button type="button" aria-label={`Remove ${s.skill}`} className="text-slate-400 hover:text-slate-700" onClick={() => setSkills(skills.filter((_, j) => j !== i))}><X className="h-4 w-4" /></button> : null}
                </li>
              ))}
            </ul>
            {canManage ? (
              <div className="mt-2 flex gap-2">
                <input className={inputClass} value={newSkill} onChange={(event) => setNewSkill(event.target.value)} placeholder="e.g. CTC roller setting, PLC, TIG welding" />
                <button type="button" className={secondaryButton} disabled={!newSkill.trim()} onClick={() => { setSkills([...skills, { skill: newSkill.trim(), level: 2 }]); setNewSkill('') }}>Add</button>
              </div>
            ) : null}
          </section>

          <section>
            <h3 className="mb-2 font-semibold text-slate-800">Certificates</h3>
            {current.certificates.length === 0 ? <p className="text-slate-500">None recorded.</p> : (
              <ul className="space-y-1.5">
                {current.certificates.map((c) => (
                  <li key={c.id} className="flex items-center justify-between gap-2 rounded-lg border border-slate-200 px-3 py-2">
                    <span><span className="font-medium">{c.name}</span><span className="block text-xs text-slate-500">{c.certificateNumber ?? 'No number'}{c.expiresOn ? ` · expires ${formatDate(c.expiresOn)}` : ''}</span></span>
                    <span className="flex items-center gap-2">
                      <Badge size="sm" tone={CERT_TONES[c.status]}>{CERT_LABELS[c.status]}</Badge>
                      {canManage ? <button type="button" aria-label={`Remove ${c.name}`} className="text-slate-400 hover:text-slate-700" onClick={() => { if (globalThis.confirm(`Remove ${c.name}?`)) void action.mutateAsync({ method: 'delete', path: `/certificates/${c.id}` }).catch(() => undefined) }}><X className="h-4 w-4" /></button> : null}
                    </span>
                  </li>
                ))}
              </ul>
            )}
            {canManage ? (
              <div className="mt-3 grid gap-2 rounded-lg bg-slate-50 p-3 sm:grid-cols-2">
                <Field label="Certificate" className="sm:col-span-2">
                  <select className={inputClass} value={cert.typeId} onChange={(event) => setCert({ ...cert, typeId: event.target.value })}>
                    <option value="">Choose</option>{(types.data ?? []).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                  </select>
                </Field>
                <Field label="Number"><input className={inputClass} value={cert.number} onChange={(event) => setCert({ ...cert, number: event.target.value })} /></Field>
                <Field label="Issued"><input type="date" className={inputClass} value={cert.issued} onChange={(event) => setCert({ ...cert, issued: event.target.value })} /></Field>
                <Field label="Expires" hint="Blank: issued + renewal period."><input type="date" className={inputClass} value={cert.expires} onChange={(event) => setCert({ ...cert, expires: event.target.value })} /></Field>
                <div className="flex items-end"><button type="button" className={secondaryButton} disabled={!cert.typeId || action.isPending} onClick={() => void addCertificate()}><Plus className="h-4 w-4" />Add certificate</button></div>
              </div>
            ) : null}
          </section>
        </div>
      ) : null}
    </FormDrawer>
  )
}

function CertificatesTab() {
  const canManage = usePermission('work-orders.assign')
  const [status, setStatus] = useState('')
  const certificates = useCertificates(status || undefined)
  const types = useCertificateTypes()
  const action = usePeopleAction()
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <select aria-label="Status" className={cn(inputClass, 'w-48')} value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All certificates</option><option value="Expired">Expired</option><option value="Expiring">Expiring in 30 days</option><option value="Valid">Valid</option>
        </select>
        {canManage && (types.data ?? []).length < 3 ? (
          <button type="button" className={secondaryButton} onClick={() => void action.mutateAsync({ path: '/certificate-types/standard' }).then(() => toast.success('Standard certificate types added')).catch(() => undefined)}>
            <ShieldCheck className="h-4 w-4" />Add the standard certificate types
          </button>
        ) : null}
      </div>
      {certificates.isLoading ? <Spinner /> : (certificates.data ?? []).length === 0 ? <EmptyState title="No certificates" description="Add them from each person's profile on the Team tab." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Person</th><th className="px-4 py-2">Certificate</th><th className="px-4 py-2">Number</th><th className="px-4 py-2">Expires</th><th className="px-4 py-2">Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(certificates.data ?? []).map((c) => (
                <tr key={c.id}>
                  <td className="px-4 py-2 font-medium text-slate-900">{c.technicianName}</td>
                  <td className="px-4 py-2">{c.name}</td>
                  <td className="px-4 py-2 font-mono text-xs">{c.certificateNumber ?? '—'}</td>
                  <td className="px-4 py-2">{c.expiresOn ? formatDate(c.expiresOn) : 'No expiry'}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={CERT_TONES[c.status]}>{CERT_LABELS[c.status]}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function RosterTab() {
  const canManage = usePermission('work-orders.assign')
  const [week, setWeek] = useState(() => mondayOf(new Date()))
  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => addDays(week, i)), [week])
  const from = iso(days[0]!)
  const to = iso(days[6]!)
  const roster = useRoster(from, to)
  const team = useTeam()
  const action = usePeopleAction()

  const cellOf = (technicianId: string, date: string) => roster.data?.entries.find((x) => x.technicianId === technicianId && x.workDate === date)
  const leaveOf = (technicianId: string, date: string) => roster.data?.leave.find((x) => x.technicianId === technicianId && x.from <= date && x.to >= date)

  const set = async (technicianId: string, date: string, value: string) => {
    const [kind, shiftName] = value.split(':') as [string, string | undefined]
    const cell = { technicianId, workDate: date, kind: kind as RosterCell['kind'] | 'Clear', shiftName: shiftName ?? null, note: null }
    try {
      await action.mutateAsync({ method: 'put', path: '/roster', body: { cells: [cell] } })
    } catch {
      // The API client shows the error.
    }
  }

  const copy = async () => {
    try {
      const written = await action.mutateAsync({ path: '/roster/copy-week', body: { weekStart: from, weeks: 1 } }) as number
      toast.success(`${written} days copied to next week`)
    } catch {
      // The API client shows the error.
    }
  }

  const shiftTone = (cell: RosterCell | undefined) => !cell ? 'bg-white text-slate-400' : cell.kind === 'Off' ? 'bg-slate-100 text-slate-500' : cell.kind === 'OnCall' ? 'bg-amber-50 text-amber-800' : 'bg-emerald-50 text-emerald-900'

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" aria-label="Previous week" className={secondaryButton} onClick={() => setWeek(addDays(week, -7))}><ChevronLeft className="h-4 w-4" /></button>
        <span className="min-w-48 text-center text-sm font-medium">{formatDate(from, 'dd MMM')} – {formatDate(to, 'dd MMM yyyy')}</span>
        <button type="button" aria-label="Next week" className={secondaryButton} onClick={() => setWeek(addDays(week, 7))}><ChevronRight className="h-4 w-4" /></button>
        {canManage ? <button type="button" className={secondaryButton} onClick={() => void copy()}><Copy className="h-4 w-4" />Copy to next week</button> : null}
      </div>
      {roster.isLoading || team.isLoading ? <Spinner /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[900px] text-sm">
            <thead className="bg-slate-50 text-xs font-medium text-slate-500">
              <tr><th className="px-3 py-2 text-left">Technician</th>{days.map((d) => <th key={iso(d)} className="px-2 py-2">{formatDate(iso(d), 'EEE dd')}</th>)}</tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {(team.data ?? []).map((m) => (
                <tr key={m.id}>
                  <td className="px-3 py-1.5"><span className="font-medium text-slate-900">{m.fullName}</span><span className="block text-xs text-slate-500">{m.trade ?? ''}</span></td>
                  {days.map((d) => {
                    const date = iso(d)
                    const leave = leaveOf(m.id, date)
                    const cell = cellOf(m.id, date)
                    if (leave) return <td key={date} className="px-1 py-1"><span className="flex h-9 items-center justify-center rounded-md bg-sky-50 text-xs font-medium text-sky-800">Leave</span></td>
                    const value = cell ? (cell.kind === 'Shift' ? `Shift:${cell.shiftName}` : cell.kind) : ''
                    return (
                      <td key={date} className="px-1 py-1">
                        <select aria-label={`${m.fullName} ${date}`} disabled={!canManage} value={value} onChange={(event) => void set(m.id, date, event.target.value || 'Clear')}
                          className={cn('h-9 w-full rounded-md border border-slate-200 px-1 text-xs font-medium', shiftTone(cell))}>
                          <option value="">—</option>
                          {(roster.data?.shifts ?? []).map((s) => <option key={s} value={`Shift:${s}`}>{s}</option>)}
                          <option value="OnCall">On call</option>
                          <option value="Off">Off</option>
                        </select>
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="text-xs text-slate-500">Nobody rostered off or on leave can be assigned a job that day. People on call can be called out for breakdowns. Time logged outside a rostered shift is costed as overtime.</p>
    </div>
  )
}

function WorkloadTab() {
  const [range, setRange] = useState<'week' | 'month'>('week')
  const today = new Date()
  const from = range === 'week' ? iso(mondayOf(today)) : iso(new Date(today.getFullYear(), today.getMonth(), 1))
  const to = range === 'week' ? iso(addDays(mondayOf(today), 6)) : iso(new Date(today.getFullYear(), today.getMonth() + 1, 0))
  const workload = useWorkload(from, to)
  return (
    <div className="space-y-3">
      <select aria-label="Period" className={cn(inputClass, 'w-40')} value={range} onChange={(event) => setRange(event.target.value as 'week' | 'month')}>
        <option value="week">This week</option><option value="month">This month</option>
      </select>
      {workload.isLoading ? <Spinner /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[860px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
              <tr><th className="px-4 py-2">Technician</th><th className="px-4 py-2 text-right">Rostered h</th><th className="px-4 py-2 text-right">Logged h</th><th className="px-4 py-2 text-right">Utilisation</th><th className="px-4 py-2 text-right">Overtime h</th><th className="px-4 py-2 text-right">Labour cost</th><th className="px-4 py-2 text-right">Jobs done</th><th className="px-4 py-2 text-right">Open jobs</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {(workload.data ?? []).map((row) => (
                <tr key={row.technicianId}>
                  <td className="px-4 py-2"><span className="font-medium text-slate-900">{row.name}</span><span className="block text-xs text-slate-500">{row.trade ?? ''}{row.onCallDays ? ` · ${row.onCallDays} on call` : ''}{row.leaveDays ? ` · ${row.leaveDays} d leave` : ''}</span></td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.rosteredHours)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.loggedHours)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{row.utilisationPercent == null ? '—' : `${row.utilisationPercent}%`}</td>
                  <td className={cn('px-4 py-2 text-right tabular-nums', row.overtimeHours > 0 && 'font-medium text-amber-700')}>{formatNumber(row.overtimeHours)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(row.labourCost)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{row.jobsCompleted}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{row.openJobs}{row.openEstimatedHours ? <span className="block text-xs text-slate-500">{formatNumber(row.openEstimatedHours)} h</span> : null}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="text-xs text-slate-500">Utilisation is hours logged on jobs against rostered shift hours.</p>
    </div>
  )
}
