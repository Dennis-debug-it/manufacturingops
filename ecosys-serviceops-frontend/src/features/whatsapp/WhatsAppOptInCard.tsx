import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { MessageCircle } from 'lucide-react'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { cn } from '../../shared/utils/cn'
import { cleanVerificationCode, codeTimeLeft, formatWhatsAppNumber, normalizeWhatsAppNumber, optInCardState } from './reminderLogic'
import { useWhatsAppMyStatus, useWhatsAppOptInMutations, type WhatsAppOptInApi } from './remindersApi'

const inputClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const primaryButton = 'inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60'
const secondaryButton = 'inline-flex h-11 items-center justify-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60'

/** Ticks every second while a code is waiting, so the countdown moves. */
function useNow(active: boolean) {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    if (!active) return
    const timer = window.setInterval(() => setNow(new Date()), 1000)
    return () => window.clearInterval(timer)
  }, [active])
  return now
}

/**
 * Opt in to WhatsApp messages on a confirmed number (Release 3 design 4.2): enter a number, get a
 * six-digit code over WhatsApp, enter it. Used on the office preferences page, the technician app's
 * account page and the client portal profile. Hidden when the workspace doesn't have WhatsApp; a
 * short note while the shared number isn't live.
 */
export function WhatsAppOptInCard({ source, audience }: { source: WhatsAppOptInApi; audience: 'staff' | 'client' }) {
  const { data: status, isLoading, isError } = useWhatsAppMyStatus(source)
  const { start, verify, optIn, optOut } = useWhatsAppOptInMutations(source)
  const [editing, setEditing] = useState(false)
  const [number, setNumber] = useState('')
  const [code, setCode] = useState('')
  const waiting = Boolean(status?.pendingNumber && status.pendingExpiresAt)
  const now = useNow(waiting)

  const cardState = optInCardState(status)
  if (isLoading || isError || !status || cardState === 'hidden') return null

  // The workspace has WhatsApp but the shared number isn't live: say so rather than hiding the card,
  // since staff already see WhatsApp toggles on their preferences (kept for when it goes live).
  if (cardState === 'unavailable') {
    return (
      <section className="flex items-start gap-3 rounded-xl border border-slate-200 bg-white p-4">
        <MessageCircle className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" />
        <div className="min-w-0 flex-1">
          <h2 className="text-sm font-semibold text-slate-900">WhatsApp messages</h2>
          <p className="mt-0.5 text-sm text-slate-500">
            {audience === 'staff'
              ? 'The Ecosys WhatsApp number isn’t live yet, so you can’t confirm your number for now. Your WhatsApp choices below are kept and apply once it is.'
              : 'WhatsApp updates aren’t available yet. You can turn them on here once they are.'}
          </p>
        </div>
      </section>
    )
  }

  const run = async (action: () => Promise<unknown>, success?: string) => {
    try {
      await action()
      if (success) toast.success(success)
      return true
    } catch (error) {
      if (source.showErrors) toast.error(resolveErrorMessage(error))
      return false
    }
  }

  const callingCode = status.defaultCallingCode || '254'
  const preview = normalizeWhatsAppNumber(number, callingCode)
  const showForm = editing || (!status.optedIn && !waiting && !status.number)
  const timeLeft = waiting && status.pendingExpiresAt ? codeTimeLeft(status.pendingExpiresAt, now) : null
  const sendCode = async () => {
    if (!preview) return
    if (await run(() => start.mutateAsync(preview), 'Code sent on WhatsApp')) {
      setEditing(false)
      setCode('')
    }
  }

  return (
    <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-start gap-3">
        <MessageCircle className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" />
        <div className="min-w-0 flex-1">
          <h2 className="text-sm font-semibold text-slate-900">WhatsApp messages</h2>
          <p className="mt-0.5 text-sm text-slate-500">
            {audience === 'staff'
              ? 'Get chosen reminders on WhatsApp as well. Pick which ones under each notification below.'
              : 'Get updates about your jobs, invoices and valuations on WhatsApp.'}
            {' '}Messages come from the shared Ecosys number{status.sharedNumber ? ` (${status.sharedNumber})` : ''} and name the business they are from. Reply STOP at any time to stop them.
          </p>
        </div>
        {status.optedIn ? <span className="shrink-0 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-200">On</span> : null}
      </div>

      {status.blockedByStop ? (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">This number replied STOP, so nothing is sent to it. Confirm it again below to turn messages back on.</p>
      ) : null}

      {waiting && !editing ? (
        <form
          className="space-y-2"
          onSubmit={(event) => {
            event.preventDefault()
            if (code.length === 6) void run(() => verify.mutateAsync(code), 'WhatsApp is on').then((ok) => ok && setCode(''))
          }}
        >
          <label htmlFor="whatsapp-code" className="block text-sm text-slate-700">
            Enter the 6-digit code sent on WhatsApp to <span className="font-medium">{formatWhatsAppNumber(status.pendingNumber)}</span>
            {timeLeft ? <span className="text-slate-500"> · expires in {timeLeft}</span> : null}
          </label>
          <div className="flex gap-2">
            <input
              id="whatsapp-code"
              inputMode="numeric"
              autoComplete="one-time-code"
              value={code}
              onChange={(event) => setCode(cleanVerificationCode(event.target.value))}
              placeholder="123456"
              className={cn(inputClass, 'max-w-[10rem] tracking-widest')}
            />
            <button type="submit" disabled={code.length !== 6 || verify.isPending} className={primaryButton}>
              {verify.isPending ? 'Checking…' : 'Confirm'}
            </button>
          </div>
          <button type="button" onClick={() => { setNumber(status.pendingNumber ?? ''); setEditing(true) }} className="text-xs font-medium text-[var(--app-primary)] hover:underline">
            Use a different number or send a new code
          </button>
        </form>
      ) : showForm ? (
        <form className="space-y-2" onSubmit={(event) => { event.preventDefault(); void sendCode() }}>
          <label htmlFor="whatsapp-number" className="block text-sm text-slate-700">Your WhatsApp number</label>
          <div className="flex flex-wrap gap-2">
            <input
              id="whatsapp-number"
              type="tel"
              inputMode="tel"
              autoComplete="tel"
              value={number}
              onChange={(event) => setNumber(event.target.value)}
              placeholder="0712 345 678"
              className={cn(inputClass, 'max-w-xs')}
            />
            <button type="submit" disabled={!preview || start.isPending} className={primaryButton}>
              {start.isPending ? 'Sending…' : 'Send code'}
            </button>
            {editing ? <button type="button" onClick={() => setEditing(false)} className={secondaryButton}>Cancel</button> : null}
          </div>
          <p className="text-xs text-slate-500">
            {number.trim() && !preview ? 'That doesn’t look like a mobile number.' : preview ? `We’ll send a code to ${formatWhatsAppNumber(preview)}.` : 'We’ll send a one-time code on WhatsApp to confirm it.'}
          </p>
        </form>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-slate-700">
            {status.optedIn ? 'Sending to ' : 'Off for '}
            <span className="font-medium">{formatWhatsAppNumber(status.number)}</span>
          </p>
          <span className="flex gap-2">
            <button type="button" onClick={() => { setNumber(''); setEditing(true) }} className={secondaryButton}>Change number</button>
            {status.optedIn ? (
              <button type="button" disabled={optOut.isPending} onClick={() => void run(() => optOut.mutateAsync(), 'WhatsApp messages turned off')} className={secondaryButton}>
                Turn off
              </button>
            ) : status.verifiedAt && source.optIn && !status.blockedByStop ? (
              <button type="button" disabled={optIn.isPending} onClick={() => void run(() => optIn.mutateAsync(), 'WhatsApp is on')} className={primaryButton}>
                Turn on
              </button>
            ) : (
              <button type="button" disabled={start.isPending} onClick={() => { setNumber(status.number ?? ''); setEditing(true) }} className={primaryButton}>
                Turn on
              </button>
            )}
          </span>
        </div>
      )}
    </section>
  )
}
