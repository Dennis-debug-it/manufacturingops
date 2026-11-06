import { useState } from 'react'
import toast from 'react-hot-toast'
import { Copy, Link2, RefreshCw, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { apiBaseUrl } from '../../shared/api/apiClient'
import { formatDateTime } from '../../shared/utils/dates'
import { fieldClass, primaryButtonClass, secondaryButtonClass } from '../../shared/components/formStyles'
import { useCalendarFeed, useCreateCalendarFeed, useRevokeCalendarFeed } from './api'
import { feedUrl, webcalUrl } from './logic'

/** Create, copy and revoke the private one-way calendar feed (design 3.5). */
export function CalendarFeedPage() {
  const { data: status, isLoading } = useCalendarFeed()
  const create = useCreateCalendarFeed()
  const revoke = useRevokeCalendarFeed()
  const [link, setLink] = useState<string | null>(null)
  const [confirm, setConfirm] = useState<'replace' | 'revoke' | null>(null)

  const makeLink = async () => {
    try {
      const created = await create.mutateAsync()
      if (created.path) setLink(feedUrl(apiBaseUrl, created.path, window.location.origin))
      toast.success('Your calendar link is ready')
    } catch {
      // toast from the API client
    } finally {
      setConfirm(null)
    }
  }

  const revokeLink = async () => {
    try {
      await revoke.mutateAsync()
      setLink(null)
      toast.success('Link turned off')
    } catch {
      // toast from the API client
    } finally {
      setConfirm(null)
    }
  }

  const copy = async (value: string) => {
    try {
      await navigator.clipboard.writeText(value)
      toast.success('Copied')
    } catch {
      toast.error('Copy failed; select the link and copy it yourself.')
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Calendar link"
        subtitle="See your Ecosys calendar in Google Calendar, Outlook or your phone."
        breadcrumbs={[{ label: 'Calendar', to: '/calendar' }, { label: 'Calendar link' }]}
      />
      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-700">
        <p>This is a private link to <strong>your</strong> calendar: your meetings and visits, and the work order, project and contract dates you can open. It shows titles, times and places only, never amounts or notes.</p>
        <p><strong>It only goes one way.</strong> Changes you make in Google or Outlook don't come back to Ecosys. Google and Outlook check the link every few hours, so a new or moved meeting can take a while to show there. Ecosys itself is always up to date.</p>
        <p>Anyone who has the link can see these items, so keep it to yourself. If it gets out, turn it off or make a new one; the old link then stops working.</p>
      </section>

      {isLoading ? <Spinner label="Loading" /> : (
        <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-4">
          {link ? (
            <>
              <p className="text-sm font-medium text-slate-900">Your link. Copy it now: for your safety it is only shown once.</p>
              <div className="flex min-w-0 flex-col gap-2 sm:flex-row">
                <input readOnly value={link} onFocus={(e) => e.target.select()} aria-label="Calendar link" className={`${fieldClass} min-w-0 font-mono text-xs`} />
                <button type="button" className={primaryButtonClass} onClick={() => void copy(link)}><Copy className="h-4 w-4" />Copy</button>
              </div>
              <ul className="list-disc space-y-1 pl-5 text-sm text-slate-600">
                <li><strong>Google Calendar</strong> (on a computer): Other calendars → + → From URL, paste the link, then Add calendar.</li>
                <li><strong>Outlook</strong>: Add calendar → Subscribe from web, paste the link.</li>
                <li><strong>iPhone or Mac</strong>: <a className="underline" href={webcalUrl(link)}>open this link</a> and choose Subscribe.</li>
              </ul>
            </>
          ) : status?.active ? (
            <p className="text-sm text-slate-700">
              You have a link, made {status.createdAt ? formatDateTime(status.createdAt) : 'earlier'}.
              {status.lastUsedAt ? ` A calendar last checked it ${formatDateTime(status.lastUsedAt)}.` : ' No calendar has used it yet.'}
              {' '}The link itself isn't shown again; make a new one if you need it.
            </p>
          ) : (
            <p className="text-sm text-slate-700">You don't have a calendar link yet.</p>
          )}
          <div className="flex flex-wrap gap-2">
            {status?.active || link ? (
              <>
                <button type="button" className={secondaryButtonClass} disabled={create.isPending} onClick={() => setConfirm('replace')}><RefreshCw className="h-4 w-4" />Make a new link</button>
                <button type="button" className={secondaryButtonClass} disabled={revoke.isPending} onClick={() => setConfirm('revoke')}><Trash2 className="h-4 w-4" />Turn off</button>
              </>
            ) : (
              <button type="button" className={primaryButtonClass} disabled={create.isPending} onClick={() => void makeLink()}><Link2 className="h-4 w-4" />Create my link</button>
            )}
          </div>
        </section>
      )}

      <ConfirmDialog
        open={confirm !== null}
        title={confirm === 'revoke' ? 'Turn off your calendar link?' : 'Make a new link?'}
        message={confirm === 'revoke'
          ? 'Calendars subscribed to it stop updating, and your Ecosys items disappear from them at their next check.'
          : 'Your old link stops working. Subscribe again with the new one.'}
        confirmLabel={confirm === 'revoke' ? 'Turn off' : 'Make new link'}
        danger={confirm === 'revoke'}
        onCancel={() => setConfirm(null)}
        onConfirm={() => void (confirm === 'revoke' ? revokeLink() : makeLink())}
      />
    </div>
  )
}
