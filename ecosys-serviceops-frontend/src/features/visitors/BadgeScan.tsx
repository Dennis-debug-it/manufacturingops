import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { CloudOff, LogIn, LogOut } from 'lucide-react'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../shared/components/Badge'
import { normalizeError } from '../../shared/api/apiError'
import { formatDateTime, formatTime } from '../../shared/utils/dates'
import { inputClass, primaryButton, secondaryButton } from '../factory/components/ui'
import { CameraScanner } from '../factory/components/CameraScanner'
import { performAction } from '../tech/offline/outbox'
import { refreshAfterSync } from '../tech/offline/hooks'
import {
  GATE_INDEX_KEY, STATE_LABEL, findOfflineBadge, lookupBadge, useCheckOut, useGateIndex,
  type Attendee, type AttendeeState, type GateIndex, type OfflineBadge, type Visit,
} from './api'
import { GateDrawer } from './GateDrawer'

const STATE_TONE: Record<AttendeeState, BadgeTone> = {
  Expected: 'info', OnSite: 'success', Overstayed: 'danger', Left: 'neutral', NoShow: 'warning', Cancelled: 'neutral',
}

/**
 * Scans a visitor badge or standing pass. Online it asks the server (a pass raises the day's visit).
 * With no signal it looks the badge up in the device's copy of today's gate index: the guard sees who
 * it is, and a check-out is queued and synced later with the time it was scanned. Checking in needs a
 * connection, because the gate checks run on the server.
 */
export function BadgeScanDrawer({ canManage, onClose }: { canManage: boolean; onClose: () => void }) {
  const queryClient = useQueryClient()
  const index = useGateIndex(true)
  const [code, setCode] = useState('')
  const [result, setResult] = useState<{ attendee: Attendee; visit: Visit } | null>(null)
  const [offline, setOffline] = useState<OfflineBadge | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [cameraOff, setCameraOff] = useState<string | null>(null)
  const [checkingIn, setCheckingIn] = useState(false)
  const [queuedOut, setQueuedOut] = useState<string | null>(null)
  const checkOut = useCheckOut()

  const reset = () => { setResult(null); setOffline(null); setNotFound(false); setQueuedOut(null); setCode('') }

  const find = async (value: string) => {
    if (!value.trim()) return
    setResult(null); setOffline(null); setNotFound(false); setQueuedOut(null)
    const local = () => {
      const hit = findOfflineBadge(index.data, value)
      if (hit) setOffline(hit)
      else setNotFound(true)
    }
    if (!navigator.onLine) return local()
    try {
      setResult(await lookupBadge(value))
    } catch (error) {
      // No signal (or the server can't be reached): fall back to the copy on the device.
      if (normalizeError(error).isNetworkError) local()
      else setNotFound(true)
    }
  }

  const leave = async () => {
    if (!result) return
    try {
      const visit = await checkOut.mutateAsync(result.attendee.id)
      toast.success(`${result.attendee.fullName} checked out`)
      setResult({ visit, attendee: visit.attendees.find((x) => x.id === result.attendee.id) ?? result.attendee })
    } catch { /* shown by the API client */ }
  }

  const leaveOffline = async (attendeeId: string, name: string) => {
    try {
      const outcome = await performAction({
        kind: 'visitor-checkout', label: `${name} checked out at the gate`, workOrderId: null, method: 'POST',
        url: `/api/visitors/attendees/${attendeeId}/check-out`, body: {},
      }, () => refreshAfterSync(queryClient))
      // Show them as gone on this device until the next refresh.
      queryClient.setQueryData<GateIndex>(GATE_INDEX_KEY, (current) => current ? { ...current, people: current.people.filter((x) => x.attendeeId !== attendeeId) } : current)
      setQueuedOut(outcome.queued ? `${name} checked out at ${formatTime(new Date())}. Saved on this device; it is sent when the signal is back.` : `${name} checked out.`)
      void queryClient.invalidateQueries({ queryKey: ['visitors'] })
    } catch { /* shown by the API client */ }
  }

  return (
    <FormDrawer open title="Scan a visitor badge" onClose={onClose}>
      <div className="space-y-4">
        {result || offline ? null : cameraOff ? <p className="text-sm text-slate-500">{cameraOff} Type the code under the QR instead.</p>
          : <CameraScanner onCode={(value) => { setCode(value); void find(value) }} onUnavailable={setCameraOff} />}
        <form className="flex gap-2" onSubmit={(e) => { e.preventDefault(); void find(code) }}>
          <input className={inputClass} placeholder="Badge code" aria-label="Badge code" value={code} onChange={(e) => setCode(e.target.value)} />
          <button type="submit" className={secondaryButton}>Find</button>
        </form>
        {index.data ? <p className="text-xs text-slate-500">Badges work offline from this device's list, updated {formatTime(index.data.generatedAt)}.</p> : null}
        {notFound ? <p className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">This badge is not one of ours, or is not valid today.</p> : null}

        {result ? (
          <div className="space-y-3 rounded-lg border border-slate-200 p-4">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-lg font-semibold text-slate-900">{result.attendee.fullName}</span>
              <Badge tone={STATE_TONE[result.attendee.state]}>{STATE_LABEL[result.attendee.state]}</Badge>
              {result.visit.source === 'Pass' ? <Badge>Standing pass</Badge> : null}
              {result.attendee.isBlocked ? <Badge tone="danger">Blocked</Badge> : null}
            </div>
            <div className="text-sm text-slate-600">{[result.attendee.company, result.visit.purpose].filter(Boolean).join(' · ')}</div>
            <div className="text-sm">Host <span className="font-medium">{result.visit.hostName}</span> · {result.visit.factoryName}</div>
            <div className="text-sm">Valid until {formatDateTime(result.visit.expectedTo)}</div>
            <div className="text-sm">May enter: {result.visit.zones.length === 0 ? 'public areas only' : `public areas, ${result.visit.zones.map((z) => z.name).join(', ')}`}</div>
            {result.attendee.escortName ? <div className="text-sm">Escort: {result.attendee.escortName}</div> : null}
            <div className="flex gap-2">
              {result.attendee.state === 'OnSite' || result.attendee.state === 'Overstayed'
                ? <button type="button" className={primaryButton} disabled={checkOut.isPending} onClick={() => void leave()}><LogOut className="h-4 w-4" />Check out</button> : null}
              {result.attendee.state === 'Expected'
                ? <button type="button" className={primaryButton} onClick={() => setCheckingIn(true)}><LogIn className="h-4 w-4" />Check in</button> : null}
              <button type="button" className={secondaryButton} onClick={reset}>Scan another</button>
            </div>
          </div>
        ) : null}

        {offline ? (
          <div className="space-y-3 rounded-lg border border-amber-200 bg-amber-50/40 p-4">
            <div className="flex items-center gap-2 text-xs font-medium text-amber-800"><CloudOff className="h-4 w-4" />No signal: from this device's list</div>
            {offline.kind === 'person' ? (
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-lg font-semibold text-slate-900">{offline.person.fullName}</span>
                  <Badge tone={offline.person.checkedInAt ? 'success' : 'info'}>{offline.person.checkedInAt ? 'On site' : 'Expected'}</Badge>
                  {offline.person.isBlocked ? <Badge tone="danger">Blocked</Badge> : null}
                </div>
                <div className="text-sm text-slate-600">{[offline.person.company, offline.person.purpose, offline.person.badgeNumber].filter(Boolean).join(' · ')}</div>
                <div className="text-sm">Host <span className="font-medium">{offline.person.hostName}</span> · {offline.person.factoryName}</div>
                <div className="text-sm">Valid until {formatDateTime(offline.person.expectedTo)}</div>
                <div className="text-sm">May enter: {offline.person.zones.length === 0 ? 'public areas only' : `public areas, ${offline.person.zones.join(', ')}`}</div>
                {queuedOut ? <p className="text-sm font-medium text-emerald-800">{queuedOut}</p> : (
                  <div className="flex gap-2">
                    {offline.person.checkedInAt
                      ? <button type="button" className={primaryButton} onClick={() => void leaveOffline(offline.person.attendeeId, offline.person.fullName)}><LogOut className="h-4 w-4" />Check out</button>
                      : <p className="text-sm text-slate-600">Checking in needs a signal: the gate checks run on the server. Call the host or wait for the connection.</p>}
                  </div>
                )}
              </>
            ) : (
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-lg font-semibold text-slate-900">{offline.pass.fullName}</span>
                  <Badge>Standing pass {offline.pass.passNumber}</Badge>
                  {offline.pass.isBlocked ? <Badge tone="danger">Blocked</Badge> : null}
                </div>
                <div className="text-sm text-slate-600">{[offline.pass.company, offline.pass.factoryName].filter(Boolean).join(' · ')} · host {offline.pass.hostName}</div>
                <p className="text-sm text-slate-600">The pass is valid today. Checking in needs a signal so the day's checks (papers, induction, permit) can run.</p>
              </>
            )}
            <button type="button" className={secondaryButton} onClick={reset}>Scan another</button>
          </div>
        ) : null}
      </div>
      {checkingIn && result ? <GateDrawer visit={result.visit} attendee={result.attendee} canManage={canManage} onClose={() => { setCheckingIn(false); reset() }} /> : null}
    </FormDrawer>
  )
}
