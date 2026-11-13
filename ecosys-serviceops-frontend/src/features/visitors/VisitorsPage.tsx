import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { AlertTriangle, BadgeCheck, Ban, Car, Clock, DoorOpen, LogIn, LogOut, Plus, Printer, QrCode, RefreshCw, Siren, UserCheck, Users } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatCard } from '../../shared/components/StatCard'
import { Badge, type BadgeTone } from '../../shared/components/Badge'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDate, formatDateTime, formatTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../factory/components/ui'
import {
  STATE_LABEL, STATUS_LABEL, ZONE_LEVELS, downloadBadges, useAccounted, useGateIndex, useBlockVisitor, useCancelVisit, useCheckOut, useDecideVisit, useOnSite,
  useResetRollCall, useSaveZone, useSeedZones, useVisitBoard, useVisitRegister, useVisitorAccess, useVisitorPeople, useVisitorReport, useZones,
  type AccessZone, type Attendee, type AttendeeState, type Visit, type VisitStatus, type ZoneLevel,
} from './api'
import { GateDrawer } from './GateDrawer'
import { VisitForm } from './VisitForm'
import { FactorySelect, useOnlyFactory } from './FactorySelect'
import { BadgeScanDrawer } from './BadgeScan'
import { useFactoryIndustry } from '../factory/industry'
import { PassesTab, VehiclesTab } from './GateTabs'
import { useAuth } from '../../shared/hooks/useAuth'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { startOfflinePersistence } from '../tech/offline/persist'

const TABS = [
  { id: 'today', label: 'Today', gate: false },
  { id: 'register', label: 'Register', gate: false },
  { id: 'vehicles', label: 'Vehicles', gate: true },
  { id: 'passes', label: 'Passes', gate: true },
  { id: 'roll-call', label: 'Roll call', gate: true },
  { id: 'people', label: 'People', gate: true },
  { id: 'zones', label: 'Zones', gate: true },
  { id: 'report', label: 'Report', gate: true },
] as const
type Tab = (typeof TABS)[number]['id']

const STATUS_TONE: Record<VisitStatus, BadgeTone> = {
  PendingApproval: 'warning', Approved: 'info', OnSite: 'success', Completed: 'neutral', Rejected: 'danger', Cancelled: 'neutral',
}
const STATE_TONE: Record<AttendeeState, BadgeTone> = {
  Expected: 'info', OnSite: 'success', Overstayed: 'danger', Left: 'neutral', NoShow: 'warning', Cancelled: 'neutral',
}

/**
 * Visitor Management. Gate staff get the desk (today's visits, walk-ins, badge scans, the roll call);
 * every other staff member sees the visits they host and can invite their own visitors.
 */
export function VisitorsPage() {
  const access = useVisitorAccess()
  const [params, setParams] = useSearchParams()
  const onlyFactory = useOnlyFactory()
  const [factoryId, setFactoryId] = useState('')
  const [form, setForm] = useState<'invite' | 'walk-in' | null>(null)
  const [scanning, setScanning] = useState(false)
  const canGate = access.data?.canGate ?? false
  const canManage = access.data?.canManage ?? false
  const tabs = TABS.filter((x) => canGate || !x.gate)
  const tab = (tabs.find((x) => x.id === params.get('tab'))?.id ?? 'today') as Tab

  useEffect(() => { if (onlyFactory && !factoryId) setFactoryId(onlyFactory) }, [onlyFactory, factoryId])
  const [checkIn, setCheckIn] = useState<{ visitId: string; attendeeId: string } | null>(null)
  // The gate keeps today's badges on the device, so scanning works with no signal.
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const userId = user?.userId
  useEffect(() => (canGate && userId ? startOfflinePersistence(queryClient, userId) : undefined), [canGate, queryClient, userId])
  useGateIndex(canGate)

  if (access.isLoading) return <Spinner label="Loading" />

  return (
    <div className="space-y-4">
      <PageHeader
        title="Visitors"
        subtitle={canGate
          ? 'Who is expected, who is inside and who has left: gate checks, badges and the emergency roll call.'
          : 'The visitors you host. Invite them ahead so the gate is ready, and approve walk-ins who ask for you.'}
        primaryAction={
          <div className="flex flex-wrap gap-2">
            {canGate ? <button type="button" className={secondaryButton} onClick={() => setScanning(true)}><QrCode className="h-4 w-4" />Scan badge</button> : null}
            {canGate ? <button type="button" className={secondaryButton} onClick={() => setForm('walk-in')}><DoorOpen className="h-4 w-4" />Walk-in</button> : null}
            <button type="button" className={primaryButton} onClick={() => setForm('invite')}><Plus className="h-4 w-4" />Invite visitor</button>
          </div>
        }
        secondaryActions={canGate ? <div className="w-56"><FactorySelect value={factoryId} onChange={setFactoryId} emptyLabel="All factories" /></div> : undefined}
      />

      {tabs.length > 1 ? (
        <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
          {tabs.map((item) => (
            <button key={item.id} type="button" role="tab" aria-selected={tab === item.id}
              onClick={() => setParams(item.id === 'today' ? {} : { tab: item.id }, { replace: true })}
              className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>
              {item.label}
            </button>
          ))}
        </div>
      ) : null}

      {tab === 'today' ? <TodayTab factoryId={factoryId} canGate={canGate} canManage={canManage} />
        : tab === 'register' ? <RegisterTab factoryId={factoryId} canGate={canGate} canManage={canManage} />
          : tab === 'vehicles' ? <VehiclesTab factoryId={factoryId || onlyFactory} />
          : tab === 'passes' ? <PassesTab factoryId={factoryId || onlyFactory} canManage={canManage} onCheckIn={(visitId, attendeeId) => setCheckIn({ visitId, attendeeId })} />
          : tab === 'roll-call' ? <RollCallTab factoryId={factoryId} />
            : tab === 'people' ? <PeopleTab canManage={canManage} />
              : tab === 'zones' ? <ZonesTab factoryId={factoryId || onlyFactory} canManage={canManage} />
                : <ReportTab factoryId={factoryId} />}

      {form ? <VisitForm walkIn={form === 'walk-in'} canGate={canGate} defaultFactoryId={factoryId || onlyFactory} onClose={() => setForm(null)} /> : null}
      {scanning ? <BadgeScanDrawer canManage={canManage} onClose={() => setScanning(false)} /> : null}
      {checkIn ? <CheckInById {...checkIn} canManage={canManage} onClose={() => setCheckIn(null)} /> : null}
    </div>
  )
}

/** Opens the gate checks for someone by id (a pass holder who just arrived). */
function CheckInById({ visitId, attendeeId, canManage, onClose }: { visitId: string; attendeeId: string; canManage: boolean; onClose: () => void }) {
  const visit = useQuery({ queryKey: ['visitors', 'visit', visitId], queryFn: () => api.get<Visit>(`/api/visitors/visits/${visitId}`) })
  const attendee = visit.data?.attendees.find((x) => x.id === attendeeId)
  const notExpected = attendee !== undefined && attendee.state !== 'Expected'
  useEffect(() => {
    if (!attendee || !notExpected) return
    toast(`${attendee.fullName} is already ${STATE_LABEL[attendee.state].toLowerCase()}.`)
    onClose()
  }, [attendee, notExpected, onClose])
  if (!visit.data || !attendee || notExpected) return null
  return <GateDrawer visit={visit.data} attendee={attendee} canManage={canManage} onClose={onClose} />
}

/* ── Today ───────────────────────────────────────────────────────────────── */

function TodayTab({ factoryId, canGate, canManage }: { factoryId: string; canGate: boolean; canManage: boolean }) {
  const board = useVisitBoard(factoryId)
  if (board.isLoading) return <Spinner label="Loading today's visits" />
  const data = board.data
  if (!data) return null
  const waiting = data.visits.filter((x) => x.status === 'PendingApproval')
  const rest = data.visits.filter((x) => x.status !== 'PendingApproval')
  return (
    <div className="space-y-4">
      <div className={cn('grid grid-cols-2 gap-3', canGate ? 'lg:grid-cols-6' : 'lg:grid-cols-5')}>
        <StatCard label="On site now" value={data.onSite} icon={<Users className="h-4 w-4 text-emerald-600" />} />
        <StatCard label="Still expected" value={data.expected} icon={<Clock className="h-4 w-4 text-slate-400" />} />
        <StatCard label="Awaiting host" value={data.awaitingApproval} tone={data.awaitingApproval > 0 ? 'warning' : 'default'} />
        <StatCard label="Overstayed" value={data.overstayed} tone={data.overstayed > 0 ? 'danger' : 'default'} icon={<AlertTriangle className="h-4 w-4 text-rose-500" />} />
        <StatCard label="Left today" value={data.leftToday} icon={<LogOut className="h-4 w-4 text-slate-400" />} />
        {canGate ? <StatCard label="Vehicles inside" value={data.vehiclesInside} icon={<Car className="h-4 w-4 text-slate-400" />} /> : null}
      </div>
      {data.visits.length === 0 ? (
        <EmptyState title="No visits today" description={canGate ? 'Register a walk-in when someone arrives, or ask hosts to invite their visitors ahead.' : 'Invite your visitors ahead so the gate has their details and badges ready.'} />
      ) : (
        <>
          {waiting.length > 0 ? <VisitList title="Waiting for the host" visits={waiting} canGate={canGate} canManage={canManage} /> : null}
          {rest.length > 0 ? <VisitList title="Today" visits={rest} canGate={canGate} canManage={canManage} /> : null}
        </>
      )}
    </div>
  )
}

function VisitList({ title, visits, canGate, canManage }: { title: string; visits: Visit[]; canGate: boolean; canManage: boolean }) {
  return (
    <section className="space-y-2">
      <h3 className="text-sm font-semibold text-slate-700">{title}</h3>
      {visits.map((visit) => <VisitCard key={visit.id} visit={visit} canGate={canGate} canManage={canManage} />)}
    </section>
  )
}

function VisitCard({ visit, canGate, canManage }: { visit: Visit; canGate: boolean; canManage: boolean }) {
  const [checkingIn, setCheckingIn] = useState<Attendee | null>(null)
  const [deciding, setDeciding] = useState<'reject' | 'cancel' | null>(null)
  const decide = useDecideVisit()
  const checkOut = useCheckOut()
  const canDecide = visit.status === 'PendingApproval' && (visit.iAmHost || canManage)
  const canCancel = (visit.status === 'PendingApproval' || visit.status === 'Approved') && (visit.iAmHost || canGate)
  const canPrint = canGate && (visit.status === 'Approved' || visit.status === 'OnSite')

  const approve = async () => {
    try {
      await decide.mutateAsync({ id: visit.id, approve: true, note: null })
      toast.success(`${visit.visitNumber} approved`)
    } catch { /* shown by the API client */ }
  }
  const leave = async (attendee: Attendee) => {
    try {
      await checkOut.mutateAsync(attendee.id)
      toast.success(`${attendee.fullName} checked out`)
    } catch { /* shown by the API client */ }
  }

  return (
    <article className="rounded-lg border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-xs text-slate-500">{visit.visitNumber}</span>
            <Badge size="sm" tone={STATUS_TONE[visit.status]}>{STATUS_LABEL[visit.status]}</Badge>
            {visit.source === 'WalkIn' ? <Badge size="sm">Walk-in</Badge> : null}
            <span className="text-sm font-medium text-slate-800">{visit.purpose}</span>
          </div>
          <div className="mt-1 text-sm text-slate-600">
            Host <span className="font-medium text-slate-800">{visit.hostName}</span> · {visit.factoryName} · {formatTime(visit.expectedFrom)}–{formatTime(visit.expectedTo)}
            {formatDate(visit.expectedFrom) !== formatDate(new Date()) ? ` on ${formatDate(visit.expectedFrom)}` : ''}
          </div>
          <div className="mt-1 flex flex-wrap gap-1">
            {visit.zones.map((zone) => <Badge key={zone.id} size="sm" tone={zone.level === 'Restricted' ? 'warning' : 'info'}>{zone.name}</Badge>)}
            {visit.contractorName ? <Badge size="sm">{visit.contractorName}</Badge> : null}
            {visit.workOrderNumber ? <Badge size="sm">Job {visit.workOrderNumber}</Badge> : null}
            {visit.permitNumber ? <Badge size="sm">Permit {visit.permitNumber}</Badge> : null}
            {visit.vehicleRegistration ? <Badge size="sm"><Car className="mr-1 inline h-3 w-3" />{visit.vehicleRegistration}</Badge> : null}
          </div>
          {visit.notes ? <p className="mt-1 text-xs text-slate-500">{visit.notes}</p> : null}
          {visit.decisionNote ? <p className="mt-1 text-xs text-rose-700">{visit.decidedByName}: {visit.decisionNote}</p> : null}
        </div>
        <div className="flex flex-wrap gap-2">
          {canDecide ? <button type="button" className={primaryButton} disabled={decide.isPending} onClick={() => void approve()}><UserCheck className="h-4 w-4" />Approve</button> : null}
          {canDecide ? <button type="button" className={secondaryButton} onClick={() => setDeciding('reject')}>Turn down</button> : null}
          {canPrint ? <button type="button" className={secondaryButton} onClick={() => void downloadBadges(visit)}><Printer className="h-4 w-4" />Badges</button> : null}
          {canCancel && !canDecide ? <button type="button" className={secondaryButton} onClick={() => setDeciding('cancel')}>Cancel</button> : null}
        </div>
      </div>

      <ul className="mt-3 divide-y divide-slate-100 rounded-lg border border-slate-100">
        {visit.attendees.map((attendee) => (
          <li key={attendee.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
            <div className="min-w-0 text-sm">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium text-slate-900">{attendee.fullName}</span>
                <Badge size="sm" tone={STATE_TONE[attendee.state]}>{STATE_LABEL[attendee.state]}</Badge>
                {attendee.isBlocked ? <Badge size="sm" tone="danger"><Ban className="mr-1 inline h-3 w-3" />Blocked</Badge> : null}
                {attendee.overrideReason ? <Badge size="sm" tone="warning">Override</Badge> : null}
              </div>
              <div className="text-xs text-slate-500">
                {[attendee.company, attendee.phone, attendee.badgeNumber].filter(Boolean).join(' · ')}
                {attendee.checkedInAt ? ` · in ${formatTime(attendee.checkedInAt)}` : ''}
                {attendee.checkedOutAt ? ` · out ${formatTime(attendee.checkedOutAt)}` : ''}
                {attendee.escortName ? ` · escort ${attendee.escortName}` : ''}
              </div>
              {attendee.overrideReason ? <div className="text-xs text-amber-700">{attendee.overriddenByName}: {attendee.overrideReason}</div> : null}
            </div>
            {canGate ? (
              <div className="flex gap-2">
                {attendee.state === 'Expected' && (visit.status === 'Approved' || visit.status === 'OnSite' || visit.status === 'PendingApproval') ? (
                  <button type="button" className={secondaryButton} onClick={() => setCheckingIn(attendee)}><LogIn className="h-4 w-4" />Check in</button>
                ) : null}
                {attendee.state === 'OnSite' || attendee.state === 'Overstayed' ? (
                  <button type="button" className={secondaryButton} disabled={checkOut.isPending} onClick={() => void leave(attendee)}><LogOut className="h-4 w-4" />Check out</button>
                ) : null}
              </div>
            ) : null}
          </li>
        ))}
      </ul>

      {checkingIn ? <GateDrawer visit={visit} attendee={checkingIn} canManage={canManage} onClose={() => setCheckingIn(null)} /> : null}
      {deciding ? <ReasonDrawer visit={visit} mode={deciding} onClose={() => setDeciding(null)} /> : null}
    </article>
  )
}

function ReasonDrawer({ visit, mode, onClose }: { visit: Visit; mode: 'reject' | 'cancel'; onClose: () => void }) {
  const [reason, setReason] = useState('')
  const decide = useDecideVisit()
  const cancel = useCancelVisit()
  const busy = decide.isPending || cancel.isPending
  const submit = async () => {
    try {
      if (mode === 'reject') await decide.mutateAsync({ id: visit.id, approve: false, note: reason.trim() || null })
      else await cancel.mutateAsync({ id: visit.id, reason: reason.trim() || null })
      toast.success(mode === 'reject' ? `${visit.visitNumber} turned down` : `${visit.visitNumber} cancelled`)
      onClose()
    } catch { /* shown by the API client */ }
  }
  return (
    <FormDrawer open title={mode === 'reject' ? `Turn down ${visit.visitNumber}` : `Cancel ${visit.visitNumber}`} onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Back</button>
        <button type="button" className={primaryButton} disabled={busy || (mode === 'reject' && !reason.trim())} onClick={() => void submit()}>{mode === 'reject' ? 'Turn down' : 'Cancel visit'}</button></div>}>
      <Field label="Reason" required={mode === 'reject'} hint="The gate sees this if the visitor turns up.">
        <textarea className={textareaClass} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Field>
    </FormDrawer>
  )
}

/* ── Register ────────────────────────────────────────────────────────────── */

function RegisterTab({ factoryId, canGate, canManage }: { factoryId: string; canGate: boolean; canManage: boolean }) {
  const today = new Date()
  const [filters, setFilters] = useState({
    from: new Date(today.getTime() - 30 * 86_400_000).toISOString().slice(0, 10),
    to: new Date(today.getTime() + 30 * 86_400_000).toISOString().slice(0, 10),
    status: '',
    q: '',
  })
  const [query, setQuery] = useState(filters.q)
  useEffect(() => {
    const timer = window.setTimeout(() => setFilters((current) => ({ ...current, q: query })), 300)
    return () => window.clearTimeout(timer)
  }, [query])
  const register = useVisitRegister({ ...filters, factoryId })
  return (
    <div className="space-y-3">
      <div className="grid gap-2 sm:grid-cols-4">
        <input className={inputClass} placeholder="Search name, company, phone, host, vehicle or number" value={query} onChange={(e) => setQuery(e.target.value)} />
        <input type="date" className={inputClass} value={filters.from} onChange={(e) => setFilters({ ...filters, from: e.target.value })} aria-label="From" />
        <input type="date" className={inputClass} value={filters.to} onChange={(e) => setFilters({ ...filters, to: e.target.value })} aria-label="To" />
        <select className={inputClass} value={filters.status} onChange={(e) => setFilters({ ...filters, status: e.target.value })} aria-label="Status">
          <option value="">Any status</option>
          {(Object.keys(STATUS_LABEL) as VisitStatus[]).map((x) => <option key={x} value={x}>{STATUS_LABEL[x]}</option>)}
        </select>
      </div>
      {register.isLoading ? <Spinner /> : (register.data?.length ?? 0) === 0 ? <EmptyState title="No visits match" description="Widen the dates or clear the search." /> : (
        <div className="space-y-2">{register.data!.map((visit) => <VisitCard key={visit.id} visit={visit} canGate={canGate} canManage={canManage} />)}</div>
      )}
    </div>
  )
}

/* ── Roll call ───────────────────────────────────────────────────────────── */

function RollCallTab({ factoryId }: { factoryId: string }) {
  const onSite = useOnSite(factoryId)
  const accounted = useAccounted()
  const reset = useResetRollCall()
  const rows = onSite.data ?? []
  const done = rows.filter((x) => x.accountedForAt).length
  const startNew = async () => {
    if (!window.confirm('Start a new roll call? Every "accounted for" mark is cleared.')) return
    try {
      await reset.mutateAsync(factoryId || null)
      toast.success('Roll call started')
    } catch { /* shown by the API client */ }
  }
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-rose-200 bg-rose-50 p-4">
        <div className="flex items-center gap-3">
          <Siren className="h-6 w-6 text-rose-600" />
          <div>
            <div className="text-lg font-semibold text-rose-900">{rows.length} visitor{rows.length === 1 ? '' : 's'} on site · {done} accounted for</div>
            <div className="text-sm text-rose-800">Refreshes every 15 seconds. Tick each person at the assembly point.</div>
          </div>
        </div>
        <div className="flex gap-2 print:hidden">
          <button type="button" className={secondaryButton} onClick={() => window.print()}><Printer className="h-4 w-4" />Print</button>
          <button type="button" className={secondaryButton} disabled={reset.isPending} onClick={() => void startNew()}><RefreshCw className="h-4 w-4" />New roll call</button>
        </div>
      </div>
      {onSite.isLoading ? <Spinner /> : rows.length === 0 ? <EmptyState title="Nobody is on site" description="No visitor is checked in at the moment." /> : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="px-3 py-2">Accounted</th><th className="px-3 py-2">Person</th><th className="px-3 py-2">Host · purpose</th><th className="px-3 py-2">Zones</th><th className="px-3 py-2">In since</th><th className="px-3 py-2">Vehicle</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((row) => (
                <tr key={row.attendeeId} className={cn(row.accountedForAt ? 'bg-emerald-50/50' : '')}>
                  <td className="px-3 py-2">
                    <input type="checkbox" aria-label={`${row.fullName} accounted for`} checked={Boolean(row.accountedForAt)} disabled={accounted.isPending}
                      onChange={(e) => accounted.mutate({ attendeeId: row.attendeeId, accounted: e.target.checked })} />
                    {row.accountedForAt ? <div className="text-xs text-emerald-700">{formatTime(row.accountedForAt)}</div> : null}
                  </td>
                  <td className="px-3 py-2">
                    <div className="font-medium text-slate-900">{row.fullName}</div>
                    <div className="text-xs text-slate-500">{[row.company, row.phone, row.badgeNumber].filter(Boolean).join(' · ')}</div>
                  </td>
                  <td className="px-3 py-2 text-slate-600">{row.hostName}<div className="text-xs text-slate-500">{row.purpose} · {row.factoryName}</div></td>
                  <td className="px-3 py-2 text-slate-600">{row.zones.length === 0 ? 'Public areas' : row.zones.join(', ')}{row.escortName ? <div className="text-xs">Escort {row.escortName}</div> : null}</td>
                  <td className={cn('px-3 py-2', row.overstayed ? 'font-medium text-rose-700' : 'text-slate-600')}>{formatTime(row.checkedInAt)}{row.overstayed ? <div className="text-xs">Overstayed</div> : null}</td>
                  <td className="px-3 py-2 font-mono text-xs text-slate-600">{row.vehicleRegistration ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

/* ── People ──────────────────────────────────────────────────────────────── */

function PeopleTab({ canManage }: { canManage: boolean }) {
  const [q, setQ] = useState('')
  const [blockedOnly, setBlockedOnly] = useState(false)
  const people = useVisitorPeople(q, blockedOnly)
  const block = useBlockVisitor()
  const toggle = async (id: string, name: string, blocked: boolean) => {
    const reason = blocked ? window.prompt(`Why is ${name} blocked? The gate will see this.`) : null
    if (blocked && !reason?.trim()) return
    try {
      await block.mutateAsync({ id, blocked, reason })
      toast.success(blocked ? `${name} blocked` : `${name} unblocked`)
    } catch { /* shown by the API client */ }
  }
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <input className={cn(inputClass, 'max-w-md')} placeholder="Search name, company, phone or ID number" value={q} onChange={(e) => setQ(e.target.value)} />
        <label className="flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" checked={blockedOnly} onChange={(e) => setBlockedOnly(e.target.checked)} />Blocked only</label>
      </div>
      {people.isLoading ? <Spinner /> : (people.data?.length ?? 0) === 0 ? <EmptyState title="Nobody found" description="Visitors appear here after their first visit." /> : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="px-3 py-2">Name</th><th className="px-3 py-2">Company</th><th className="px-3 py-2">Phone</th><th className="px-3 py-2">ID</th><th className="px-3 py-2">Last visit</th><th className="px-3 py-2" /></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {people.data!.map((person) => (
                <tr key={person.id}>
                  <td className="px-3 py-2 font-medium text-slate-900">
                    {person.fullName}
                    {person.isBlocked ? <div className="text-xs font-normal text-rose-700">Blocked by {person.blockedByName}: {person.blockedReason}</div> : null}
                  </td>
                  <td className="px-3 py-2 text-slate-600">{person.company ?? person.contractorName ?? '—'}</td>
                  <td className="px-3 py-2 text-slate-600">{person.phone ?? '—'}</td>
                  <td className="px-3 py-2 text-slate-600">{person.idNumberMasked ? `${person.idType ?? 'ID'} ${person.idNumberMasked}` : '—'}</td>
                  <td className="px-3 py-2 text-slate-600">{person.lastVisitAt ? formatDate(person.lastVisitAt) : '—'}</td>
                  <td className="px-3 py-2 text-right">
                    {canManage ? (
                      <button type="button" className="text-sm font-medium text-[var(--app-primary)] hover:underline" onClick={() => void toggle(person.id, person.fullName, !person.isBlocked)}>
                        {person.isBlocked ? 'Unblock' : 'Block'}
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="text-xs text-slate-500">Visitors not seen for the retention period (12 months by default) have their details removed automatically. Blocked visitors are kept.</p>
    </div>
  )
}

/* ── Zones ───────────────────────────────────────────────────────────────── */

const LEVEL_TONE: Record<ZoneLevel, BadgeTone> = { Public: 'success', Controlled: 'info', Restricted: 'warning' }

function ZonesTab({ factoryId, canManage }: { factoryId: string; canManage: boolean }) {
  // The page's factory (or the only one) until a factory is picked here; it may load after this mounts.
  const [picked, setPicked] = useState('')
  const factory = picked || factoryId
  const setFactory = setPicked
  const industry = useFactoryIndustry(factory)
  const zones = useZones(factory, true)
  const seed = useSeedZones()
  const [editing, setEditing] = useState<AccessZone | 'new' | null>(null)
  const rows = (zones.data ?? []).filter((x) => !factory || x.factoryId === factory)
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="w-64"><FactorySelect value={factory} onChange={setFactory} emptyLabel="All factories" /></div>
        {canManage && factory ? <button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add zone</button> : null}
      </div>
      {zones.isLoading ? <Spinner /> : rows.length === 0 ? (
        <EmptyState title="No zones yet" description={factory ? 'Zones say where visitors may go and what they need: an induction, PPE, an escort or a permit.' : 'Choose a factory to set up its zones.'}
          actionLabel={canManage && factory ? `Add the standard ${(industry?.name ?? 'factory').toLowerCase()} zones` : undefined}
          onAction={canManage && factory ? () => void seed.mutateAsync(factory).then(() => toast.success('Standard zones added')).catch(() => undefined) : undefined} />
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="px-3 py-2">Zone</th><th className="px-3 py-2">Level</th><th className="px-3 py-2">Needs</th><th className="px-3 py-2">PPE</th><th className="px-3 py-2" /></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((zone) => (
                <tr key={zone.id} className={cn(!zone.isActive && 'text-slate-400')}>
                  <td className="px-3 py-2 font-medium">{zone.name}{factory ? null : <div className="text-xs font-normal text-slate-500">{zone.factoryName}</div>}{zone.isActive ? null : <div className="text-xs">Inactive</div>}</td>
                  <td className="px-3 py-2"><Badge size="sm" tone={LEVEL_TONE[zone.level]}>{zone.level}</Badge></td>
                  <td className="px-3 py-2 text-slate-600">{[zone.requiresInduction && 'Induction', zone.requiresEscort && 'Escort', zone.requiresPermit && 'Permit'].filter(Boolean).join(', ') || '—'}</td>
                  <td className="px-3 py-2 text-slate-600">{zone.requiredPpe ?? '—'}</td>
                  <td className="px-3 py-2 text-right">{canManage ? <button type="button" className="text-sm font-medium text-[var(--app-primary)] hover:underline" onClick={() => setEditing(zone)}>Edit</button> : null}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {editing ? <ZoneDrawer zone={editing === 'new' ? null : editing} factoryId={factory} onClose={() => setEditing(null)} /> : null}
    </div>
  )
}

function ZoneDrawer({ zone, factoryId, onClose }: { zone: AccessZone | null; factoryId: string; onClose: () => void }) {
  const save = useSaveZone()
  const [form, setForm] = useState({
    name: zone?.name ?? '',
    level: (zone?.level ?? 'Controlled') as ZoneLevel,
    requiresEscort: zone?.requiresEscort ?? false,
    requiresInduction: zone?.requiresInduction ?? false,
    requiresPermit: zone?.requiresPermit ?? false,
    requiredPpe: zone?.requiredPpe ?? '',
    isActive: zone?.isActive ?? true,
  })
  const submit = async () => {
    try {
      await save.mutateAsync({ id: zone?.id, payload: { factoryId: zone?.factoryId ?? factoryId, ...form, name: form.name.trim(), requiredPpe: form.requiredPpe.trim() || null } })
      toast.success('Zone saved')
      onClose()
    } catch { /* shown by the API client */ }
  }
  return (
    <FormDrawer open title={zone ? `Edit ${zone.name}` : 'Add zone'} onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={save.isPending || !form.name.trim()} onClick={() => void submit()}>Save</button></div>}>
      <div className="space-y-4">
        <Field label="Name" required><input className={inputClass} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        <Field label="Level" hint="Public: every visitor. Controlled and restricted: only visits given the zone.">
          <select className={inputClass} value={form.level} onChange={(e) => setForm({ ...form, level: e.target.value as ZoneLevel })}>
            {ZONE_LEVELS.map((x) => <option key={x}>{x}</option>)}
          </select>
        </Field>
        <div className="space-y-2 text-sm">
          <label className="flex items-center gap-2"><input type="checkbox" checked={form.requiresInduction} onChange={(e) => setForm({ ...form, requiresInduction: e.target.checked })} />Needs a current safety induction</label>
          <label className="flex items-center gap-2"><input type="checkbox" checked={form.requiresEscort} onChange={(e) => setForm({ ...form, requiresEscort: e.target.checked })} />Visitors must be escorted</label>
          <label className="flex items-center gap-2"><input type="checkbox" checked={form.requiresPermit} onChange={(e) => setForm({ ...form, requiresPermit: e.target.checked })} />Only with an approved permit to work</label>
          {zone ? <label className="flex items-center gap-2"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />Active</label> : null}
        </div>
        <Field label="PPE to confirm at the gate" hint="Comma separated, e.g. Hairnet, Dust coat, Safety boots">
          <input className={inputClass} value={form.requiredPpe} onChange={(e) => setForm({ ...form, requiredPpe: e.target.value })} />
        </Field>
      </div>
    </FormDrawer>
  )
}

/* ── Report ──────────────────────────────────────────────────────────────── */

function ReportTab({ factoryId }: { factoryId: string }) {
  const [days, setDays] = useState(30)
  const report = useVisitorReport(factoryId, days)
  const data = report.data
  const peak = Math.max(1, ...(data?.perDay.map((x) => x.count) ?? [1]))
  return (
    <div className="space-y-4">
      <select className={cn(inputClass, 'w-48')} value={days} onChange={(e) => setDays(Number(e.target.value))} aria-label="Period">
        <option value={7}>Last 7 days</option><option value={30}>Last 30 days</option><option value={90}>Last 90 days</option>
      </select>
      {report.isLoading || !data ? <Spinner /> : (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
            <StatCard label="Entries" value={data.entries} icon={<BadgeCheck className="h-4 w-4 text-slate-400" />} />
            <StatCard label="Contractor entries" value={data.contractorEntries} />
            <StatCard label="Average time on site" value={data.averageMinutesOnSite === null ? '—' : `${Math.floor(data.averageMinutesOnSite / 60)}h ${data.averageMinutesOnSite % 60}m`} />
            <StatCard label="Overstays" value={data.overstays} tone={data.overstays > 0 ? 'warning' : 'default'} />
            <StatCard label="No-show visits" value={data.noShowVisits} />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <section className="rounded-lg border border-slate-200 bg-white p-4">
              <h3 className="mb-3 text-sm font-semibold text-slate-700">Entries per day</h3>
              {data.perDay.length === 0 ? <p className="text-sm text-slate-500">No entries in this period.</p> : (
                <div className="flex h-40 items-end gap-1" role="img" aria-label="Entries per day">
                  {data.perDay.map((day) => (
                    <div key={day.day} className="group relative flex-1">
                      <div className="w-full rounded-t bg-[var(--app-primary)]" style={{ height: `${(day.count / peak) * 150}px` }} title={`${formatDate(day.day)}: ${day.count}`} />
                    </div>
                  ))}
                </div>
              )}
            </section>
            <section className="rounded-lg border border-slate-200 bg-white p-4">
              <h3 className="mb-3 text-sm font-semibold text-slate-700">By purpose</h3>
              <ul className="space-y-1 text-sm">
                {data.byPurpose.map((x) => <li key={x.purpose} className="flex justify-between"><span>{x.purpose}</span><span className="tabular-nums text-slate-600">{x.count}</span></li>)}
              </ul>
            </section>
          </div>
          <section className="rounded-lg border border-slate-200 bg-white p-4">
            <h3 className="mb-3 text-sm font-semibold text-slate-700">Gate overrides</h3>
            {data.overrides.length === 0 ? <p className="text-sm text-slate-500">Nobody was let in past a failed check.</p> : (
              <ul className="divide-y divide-slate-100 text-sm">
                {data.overrides.map((x) => (
                  <li key={`${x.visitNumber}-${x.at}`} className="py-2">
                    <span className="font-medium">{x.fullName}</span> <span className="text-slate-500">({x.visitNumber}, {formatDateTime(x.at)})</span>
                    <div className="text-slate-600">{x.byName}: {x.reason}</div>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  )
}
