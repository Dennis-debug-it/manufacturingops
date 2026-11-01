import toast from 'react-hot-toast'
import { Copy } from 'lucide-react'
import { Link } from 'react-router-dom'
import { EMAIL_INTAKE_ENABLED } from '../../../shared/config/features'

/** The public address of the website enquiry form for a key. */
export function leadFormUrl(key: string, origin = window.location.origin) {
  return `${origin}/enquiry/${key}`
}

const copy = (text: string, what: string) =>
  void navigator.clipboard.writeText(text).then(() => toast.success(`${what} copied`), () => toast.error('Could not copy'))

/**
 * Where leads come in from without anyone typing them: a sales email address (an alias of the intake
 * mailbox) and the website enquiry form. Saved with the rest of the page.
 */
export function LeadCaptureSection({ leadEmailAddress, webFormEnabled, webFormKey, intakeEmailAddress, onChange }: {
  leadEmailAddress: string
  webFormEnabled: boolean
  webFormKey: string | null
  intakeEmailAddress: string | null
  onChange: (patch: { leadEmailAddress?: string; webFormEnabled?: boolean }) => void
}) {
  const url = webFormKey ? leadFormUrl(webFormKey) : null
  const embed = url ? `<iframe src="${url}?embed=1" title="Enquiry form" style="width:100%;max-width:560px;height:640px;border:0"></iframe>` : null
  return (
    <section className="space-y-4 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
      <div>
        <h2 className="text-sm font-semibold text-slate-900">Lead capture</h2>
        <p className="mt-1 text-sm text-slate-500">
          Enquiries become leads with a follow-up for their owner (assignment rules decide who). A second message from the same person within a week joins their lead.
        </p>
      </div>

      {EMAIL_INTAKE_ENABLED ? (
      <div className="space-y-1.5">
        <label className="block text-sm font-medium text-slate-700" htmlFor="lead-email">Sales email address</label>
        <input
          id="lead-email"
          type="email"
          value={leadEmailAddress}
          onChange={(event) => onChange({ leadEmailAddress: event.target.value })}
          placeholder="sales@yourcompany.co.ke"
          className="h-10 w-full max-w-md rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
        <p className="text-xs text-slate-500">
          {intakeEmailAddress
            ? <>Email to this address that reaches the intake mailbox ({intakeEmailAddress}), e.g. as an alias or forward, becomes a lead instead of a work order. Replies to emails sent from deals come back onto the deal.</>
            : <>Needs the intake mailbox, which is off. Set it up in <Link to="/settings/email-intake" className="underline">Email intake</Link>, then add this address to it as an alias or forward.</>}
        </p>
      </div>
      ) : null}

      <div className="space-y-2">
        <label className="flex items-start gap-3 text-sm">
          <input type="checkbox" className="mt-1" checked={webFormEnabled} onChange={(event) => onChange({ webFormEnabled: event.target.checked })} />
          <span>
            <span className="block font-medium text-slate-900">Website enquiry form</span>
            <span className="mt-0.5 block text-slate-500">A short form (name, company, contact details, what they need) to link to from your website or put on it.</span>
          </span>
        </label>
        {webFormEnabled && !url ? <p className="text-xs text-slate-500">Save to get the form's link.</p> : null}
        {webFormEnabled && url && embed ? (
          <div className="space-y-2 rounded-lg bg-slate-50 p-3 text-sm">
            <div className="flex flex-wrap items-center gap-2">
              <a href={url} target="_blank" rel="noreferrer" className="min-w-0 break-all text-[var(--app-primary)] underline">{url}</a>
              <button type="button" onClick={() => copy(url, 'Link')} className="inline-flex h-8 items-center gap-1 rounded-md border border-slate-200 bg-white px-2 text-xs text-slate-600"><Copy className="h-3.5 w-3.5" />Copy link</button>
            </div>
            <div>
              <div className="mb-1 text-xs text-slate-500">To put the form on a page of your website, paste this where it should appear:</div>
              <div className="flex items-start gap-2">
                <code className="block min-w-0 flex-1 break-all rounded border border-slate-200 bg-white p-2 text-xs text-slate-700">{embed}</code>
                <button type="button" onClick={() => copy(embed, 'Code')} className="inline-flex h-8 shrink-0 items-center gap-1 rounded-md border border-slate-200 bg-white px-2 text-xs text-slate-600"><Copy className="h-3.5 w-3.5" />Copy</button>
              </div>
            </div>
          </div>
        ) : null}
      </div>
    </section>
  )
}
