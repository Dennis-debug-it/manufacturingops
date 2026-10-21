import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { formatCurrency } from '../../../shared/utils/format'
import { formatDate } from '../../../shared/utils/dates'
import { useCreateVariation, useDecideVariation, useProjectVariations, useSubmitVariation, useUpdateVariation, useVoidVariation } from '../api'
import type { ProjectDetail, ProjectVariation, VariationKind } from '../types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textAreaClass, textOrNull } from './formStyles'

interface Draft {
  id?: string
  kind: VariationKind
  amount: string
  title: string
  description: string
  clientReference: string
}

const emptyDraft: Draft = { kind: 'Addition', amount: '', title: '', description: '', clientReference: '' }

type PendingAction = { variation: ProjectVariation; action: 'approve' | 'reject' | 'void' }

export function VariationsTab({ project }: { project: ProjectDetail }) {
  const projectId = project.summary.id
  const { abilities } = project
  const isFinal = project.summary.stageCategory === 'Closed' || project.summary.stageCategory === 'Cancelled'
  const canEdit = abilities.canSubmitVariations && !isFinal
  const { data: variations, isLoading } = useProjectVariations(projectId)
  const createVariation = useCreateVariation(projectId)
  const updateVariation = useUpdateVariation(projectId)
  const submitVariation = useSubmitVariation(projectId)
  const decideVariation = useDecideVariation(projectId)
  const voidVariation = useVoidVariation(projectId)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [pending, setPending] = useState<PendingAction | null>(null)
  const [note, setNote] = useState('')
  const money = (value: number) => formatCurrency(value, project.summary.currency)

  const save = async (andSubmit: boolean) => {
    if (!draft) return
    const payload = { kind: draft.kind, amount: Number(draft.amount), title: draft.title.trim(), description: textOrNull(draft.description), clientReference: textOrNull(draft.clientReference) }
    try {
      const saved = draft.id
        ? await updateVariation.mutateAsync({ variationId: draft.id, ...payload })
        : await createVariation.mutateAsync(payload)
      if (andSubmit) await submitVariation.mutateAsync(saved.id)
      toast.success(andSubmit ? `${saved.variationNumber} submitted for approval` : `${saved.variationNumber} saved as draft`)
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const runPending = async () => {
    if (!pending) return
    const { variation, action } = pending
    try {
      if (action === 'void') await voidVariation.mutateAsync({ variationId: variation.id, reason: textOrNull(note) })
      else await decideVariation.mutateAsync({ variationId: variation.id, approve: action === 'approve', note: textOrNull(note) })
      toast.success(`${variation.variationNumber} ${action === 'void' ? 'voided' : action === 'approve' ? 'approved' : 'rejected'}`)
    } catch {
      // toast from the API client (including "the approval matrix says you can't decide this level")
    } finally {
      setPending(null)
      setNote('')
    }
  }

  const columns: DataTableColumn<ProjectVariation>[] = [
    {
      key: 'variationNumber',
      header: 'Variation',
      cell: (row) => (<div><div className="font-medium text-slate-900">{row.title}</div><div className="font-mono text-xs text-slate-500">{row.variationNumber}{row.clientReference ? ` · ref ${row.clientReference}` : ''}</div></div>),
    },
    { key: 'kind', header: 'Type', cell: (row) => row.kind },
    { key: 'amount', header: 'Amount', cell: (row) => <span className={row.kind === 'Omission' ? 'text-rose-700' : 'text-emerald-700'}>{row.kind === 'Omission' ? '− ' : '+ '}{money(row.amount)}</span> },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'date', header: 'Date', cell: (row) => formatDate(row.decidedAt ?? row.submittedAt ?? row.createdAt) },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <div className="flex flex-wrap justify-end gap-2">
          {row.status === 'Draft' && canEdit ? (
            <>
              <button type="button" className={smallButtonClass} onClick={() => setDraft({ id: row.id, kind: row.kind, amount: String(row.amount), title: row.title, description: row.description ?? '', clientReference: row.clientReference ?? '' })}>Edit</button>
              <button type="button" className={smallButtonClass} onClick={() => submitVariation.mutate(row.id, { onSuccess: () => toast.success(`${row.variationNumber} submitted`) })}>Submit</button>
            </>
          ) : null}
          {/* Matrix approvers who lack projects.variations.approve decide from the Approvals inbox. */}
          {row.status === 'Submitted' && abilities.canApproveVariations ? (
            <>
              <button type="button" className={smallButtonClass} onClick={() => setPending({ variation: row, action: 'approve' })}>Approve</button>
              <button type="button" className={smallButtonClass} onClick={() => setPending({ variation: row, action: 'reject' })}>Reject</button>
            </>
          ) : null}
          {(row.status === 'Approved' && abilities.canApproveVariations) || ((row.status === 'Draft' || row.status === 'Rejected') && canEdit) ? (
            <button type="button" className={smallButtonClass} onClick={() => setPending({ variation: row, action: 'void' })}>Void</button>
          ) : null}
        </div>
      ),
    },
  ]

  const voidingApproved = pending?.action === 'void' && pending.variation.status === 'Approved'

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-1 gap-3 rounded-xl border border-slate-200 bg-white p-4 text-sm sm:grid-cols-4">
        <div><div className="text-xs uppercase tracking-[0.18em] text-slate-500">Original</div><div className="mt-1 font-semibold text-slate-900">{money(project.summary.originalValue)}</div></div>
        <div><div className="text-xs uppercase tracking-[0.18em] text-slate-500">Approved additions</div><div className="mt-1 font-semibold text-emerald-700">+ {money(project.approvedAdditions)}</div></div>
        <div><div className="text-xs uppercase tracking-[0.18em] text-slate-500">Approved omissions</div><div className="mt-1 font-semibold text-rose-700">− {money(project.approvedOmissions)}</div></div>
        <div><div className="text-xs uppercase tracking-[0.18em] text-slate-500">Revised value</div><div className="mt-1 font-semibold text-slate-900">{money(project.summary.revisedValue)}</div></div>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-slate-600">Only approved variations change the project value. Approvals follow the approval matrix for project variations when one is set up.</p>
        {canEdit ? <button type="button" className={primaryButtonClass} onClick={() => setDraft(emptyDraft)}><Plus className="h-4 w-4" />New variation</button> : null}
      </div>
      <DataTable columns={columns} data={variations ?? []} loading={isLoading} emptyTitle="No variations" emptyDescription="Additions and omissions instructed by the client or consultant are recorded here." />

      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit variation' : 'New variation'}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button>
            <button type="button" className={secondaryButtonClass} disabled={!draft?.title.trim() || !(Number(draft?.amount) > 0)} onClick={() => void save(false)}>Save draft</button>
            <button type="button" className={primaryButtonClass} disabled={!draft?.title.trim() || !(Number(draft?.amount) > 0)} onClick={() => void save(true)}>Save and submit</button>
          </div>
        }
      >
        {draft ? (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="variation-kind">Type</label>
                <select id="variation-kind" value={draft.kind} onChange={(event) => setDraft({ ...draft, kind: event.target.value as VariationKind })} className={fieldClass}>
                  <option value="Addition">Addition</option>
                  <option value="Omission">Omission</option>
                </select>
              </div>
              <div>
                <label className={labelClass} htmlFor="variation-amount">Amount ({project.summary.currency})</label>
                <input id="variation-amount" type="number" min="0" step="0.01" inputMode="decimal" value={draft.amount} onChange={(event) => setDraft({ ...draft, amount: event.target.value })} className={fieldClass} />
              </div>
            </div>
            <div>
              <label className={labelClass} htmlFor="variation-title">Title</label>
              <input id="variation-title" value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} placeholder="e.g. Additional AHU for level 3" className={fieldClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="variation-ref">Client / consultant instruction reference</label>
              <input id="variation-ref" value={draft.clientReference} onChange={(event) => setDraft({ ...draft, clientReference: event.target.value })} className={fieldClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="variation-description">Description</label>
              <textarea id="variation-description" rows={3} value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} className={textAreaClass} />
            </div>
          </div>
        ) : null}
      </FormDrawer>

      <ConfirmDialog
        open={pending !== null}
        title={pending ? `${pending.action === 'approve' ? 'Approve' : pending.action === 'reject' ? 'Reject' : 'Void'} ${pending.variation.variationNumber}` : ''}
        message={pending?.action === 'approve'
          ? `This ${pending.variation.kind.toLowerCase()} of ${money(pending.variation.amount)} will change the revised project value.`
          : voidingApproved ? 'Voiding reverses this variation\'s effect on the project value. Raise a new variation if it needs correcting.' : 'This can\'t be undone.'}
        confirmLabel={pending?.action === 'approve' ? 'Approve' : pending?.action === 'reject' ? 'Reject' : 'Void'}
        danger={pending?.action !== 'approve'}
        confirmDisabled={(voidingApproved && !note.trim()) || decideVariation.isPending || voidVariation.isPending}
        onCancel={() => { setPending(null); setNote('') }}
        onConfirm={() => void runPending()}
      >
        <div className="text-left">
          <label className={labelClass} htmlFor="variation-note">{voidingApproved ? 'Reason (required)' : 'Note (optional)'}</label>
          <textarea id="variation-note" rows={2} value={note} onChange={(event) => setNote(event.target.value)} className={textAreaClass} />
        </div>
      </ConfirmDialog>
    </div>
  )
}
