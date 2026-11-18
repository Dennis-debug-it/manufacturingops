import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Copy, FileText, Plus, Star, Trash2 } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { primaryButton, secondaryButton } from '../../features/factory/components/ui'
import { DOCUMENT_TYPES, useDocumentTemplates, useTemplateAction, type DocumentTemplate, type DocumentType } from './api'

const TYPE_HINTS: Record<DocumentType, string> = {
  Invoice: 'Sent to workspaces for subscriptions, renewals and one-off fees.',
  Quotation: 'Price quotations from Finance.',
  Receipt: 'Sent when a payment is recorded.',
}

/**
 * Document templates: how quotations, invoices and receipts look as PDFs. One per type is the default and is used
 * for every document of that type; the rest are kept for later.
 */
export function DocumentTemplatesPage() {
  const navigate = useNavigate()
  const templates = useDocumentTemplates()
  const action = useTemplateAction()
  const [deleting, setDeleting] = useState<DocumentTemplate | null>(null)
  const rows = templates.data ?? []
  const known = (type: string) => DOCUMENT_TYPES.some((x) => x.toLowerCase() === type.toLowerCase())
  const others = rows.filter((x) => !known(x.type))

  const run = (id: string, kind: 'duplicate' | 'make-default', done: string) =>
    action.mutate({ id, action: kind }, { onSuccess: () => toast.success(done) })

  return (
    <div className="space-y-5">
      <PageHeader
        title="Document Templates"
        subtitle="Design how quotations, invoices and receipts look: layout, colour, your company details, terms and payment details. The default template of each type is used for every PDF of that type."
      />
      {templates.isLoading ? <Spinner label="Loading templates" /> : (
        <div className="space-y-4">
          {DOCUMENT_TYPES.map((type) => {
            const ofType = rows.filter((x) => x.type.toLowerCase() === type.toLowerCase())
            return (
              <section key={type} className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h2 className="text-base font-semibold text-slate-900">{type}s</h2>
                    <p className="text-sm text-slate-600">{TYPE_HINTS[type]}</p>
                  </div>
                  <button type="button" className={secondaryButton} onClick={() => navigate(`/platform/settings/templates/new?type=${type}`)}>
                    <Plus className="h-4 w-4" />New {type.toLowerCase()} template
                  </button>
                </div>
                {ofType.length === 0 ? (
                  <div className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-dashed border-slate-300 p-4 text-sm text-slate-600">
                    <span>No {type.toLowerCase()} template yet: {type.toLowerCase()}s use the standard look.</span>
                    <button type="button" className={primaryButton} onClick={() => navigate(`/platform/settings/templates/new?type=${type}`)}>
                      <FileText className="h-4 w-4" />Design the {type.toLowerCase()}
                    </button>
                  </div>
                ) : (
                  <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
                    {ofType.map((x) => <TemplateRow key={x.id} template={x} onDefault={() => run(x.id, 'make-default', `${x.name} is now the default`)} onDuplicate={() => run(x.id, 'duplicate', 'Copy made')} onDelete={() => setDeleting(x)} />)}
                  </ul>
                )}
              </section>
            )
          })}
          {others.length > 0 ? (
            <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm">
              <h2 className="text-base font-semibold text-slate-900">Other templates</h2>
              <p className="text-sm text-slate-600">Older templates of a type no document uses. Open one to change its type, or delete it.</p>
              <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
                {others.map((x) => <TemplateRow key={x.id} template={x} onDuplicate={() => run(x.id, 'duplicate', 'Copy made')} onDelete={() => setDeleting(x)} />)}
              </ul>
            </section>
          ) : null}
          <p className="text-sm text-slate-500">Looking for the emails the platform sends? They are in <Link to="/platform/settings/email-templates" className="text-[var(--app-primary)] hover:underline">Email Templates</Link>.</p>
        </div>
      )}

      <ConfirmDialog
        open={deleting !== null}
        title="Delete template"
        message={deleting ? `Delete ${deleting.name}? If documents have already been issued with it, it is switched off instead, so their look is kept.` : ''}
        confirmLabel="Delete"
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (!deleting) return
          action.mutate({ id: deleting.id, action: 'delete' }, { onSuccess: () => toast.success('Template removed'), onSettled: () => setDeleting(null) })
        }}
      />
    </div>
  )
}

function TemplateRow({ template: x, onDefault, onDuplicate, onDelete }: { template: DocumentTemplate; onDefault?: () => void; onDuplicate: () => void; onDelete: () => void }) {
  return (
    <li className="flex flex-wrap items-center gap-3 px-3 py-3">
      <span aria-hidden className="h-8 w-8 shrink-0 rounded-lg border border-slate-200" style={{ background: x.accentColor }} />
      <div className="min-w-0 flex-1">
        <Link to={`/platform/settings/templates/${x.id}`} className="font-medium text-slate-900 hover:text-[var(--app-primary)] hover:underline">{x.name}</Link>
        <div className="text-xs text-slate-500">{x.layout} layout · {x.pageSize} {x.orientation.toLowerCase()} · updated {formatDate(x.updatedAt ?? x.createdAt)}</div>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {x.isDefault ? <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700"><Star className="h-3 w-3" />Default</span> : null}
        {!x.isActive ? <StatusBadge status="Inactive" /> : null}
        {onDefault && !x.isDefault ? <button type="button" className={cn(secondaryButton, 'h-8 px-3 text-xs')} onClick={onDefault}>Make default</button> : null}
        <Link to={`/platform/settings/templates/${x.id}`} className={cn(secondaryButton, 'h-8 px-3 text-xs')}>Open designer</Link>
        <button type="button" className={cn(secondaryButton, 'h-8 px-2')} onClick={onDuplicate} aria-label={`Duplicate ${x.name}`} title="Duplicate"><Copy className="h-4 w-4" /></button>
        <button type="button" className={cn(secondaryButton, 'h-8 px-2 text-rose-700')} onClick={onDelete} aria-label={`Delete ${x.name}`} title="Delete"><Trash2 className="h-4 w-4" /></button>
      </div>
    </li>
  )
}
