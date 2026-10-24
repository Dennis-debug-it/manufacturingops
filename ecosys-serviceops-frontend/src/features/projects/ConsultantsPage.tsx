import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useConsultants, useCreateConsultant, useDeleteConsultant, useUpdateConsultant, type ConsultantPayload } from './api'
import type { Consultant } from './types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textAreaClass, textOrNull } from './components/formStyles'

interface Draft {
  id?: string
  firmName: string
  discipline: string
  contactPerson: string
  phone: string
  email: string
  physicalAddress: string
  notes: string
}

const emptyDraft: Draft = { firmName: '', discipline: '', contactPerson: '', phone: '', email: '', physicalAddress: '', notes: '' }

/** The consultant directory: firms and their contact details, shared by every project. */
export function ConsultantsPage() {
  const canManage = usePermission('projects.manage')
  const [search, setSearch] = useState('')
  const [showInactive, setShowInactive] = useState(false)
  const debounced = useDebounce(search, 300)
  const { data, isLoading, isError } = useConsultants(debounced.trim() || undefined, showInactive)
  const createConsultant = useCreateConsultant()
  const updateConsultant = useUpdateConsultant()
  const deleteConsultant = useDeleteConsultant()
  const [draft, setDraft] = useState<Draft | null>(null)
  const [removing, setRemoving] = useState<Consultant | null>(null)

  const save = async () => {
    if (!draft?.firmName.trim()) return
    const payload: ConsultantPayload = {
      firmName: draft.firmName.trim(),
      discipline: textOrNull(draft.discipline),
      contactPerson: textOrNull(draft.contactPerson),
      phone: textOrNull(draft.phone),
      email: textOrNull(draft.email),
      physicalAddress: textOrNull(draft.physicalAddress),
      notes: textOrNull(draft.notes),
    }
    try {
      if (draft.id) await updateConsultant.mutateAsync({ id: draft.id, ...payload })
      else await createConsultant.mutateAsync(payload)
      toast.success(draft.id ? 'Consultant updated' : 'Consultant added')
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const reactivate = (consultant: Consultant) =>
    updateConsultant.mutate(
      { id: consultant.id, firmName: consultant.firmName, discipline: consultant.discipline, contactPerson: consultant.contactPerson, phone: consultant.phone, email: consultant.email, physicalAddress: consultant.physicalAddress, notes: consultant.notes, isActive: true },
      { onSuccess: () => toast.success(`${consultant.firmName} reactivated`) },
    )

  const columns: DataTableColumn<Consultant>[] = [
    { key: 'firmName', header: 'Firm', cell: (row) => (<div><div className="font-medium text-slate-900">{row.firmName}{row.isActive ? '' : ' (inactive)'}</div>{row.discipline ? <div className="text-xs text-slate-500">{row.discipline}</div> : null}</div>) },
    { key: 'contactPerson', header: 'Contact', cell: (row) => row.contactPerson ?? '—' },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? '—' },
    { key: 'email', header: 'Email', cell: (row) => (row.email ? <a className="underline decoration-slate-300 underline-offset-4" href={`mailto:${row.email}`}>{row.email}</a> : '—') },
    { key: 'projectCount', header: 'Projects', cell: (row) => row.projectCount },
    ...(canManage ? [{
      key: 'actions',
      header: '',
      cell: (row: Consultant) => (
        <div className="flex justify-end gap-2">
          <button type="button" className={smallButtonClass} onClick={() => setDraft({ id: row.id, firmName: row.firmName, discipline: row.discipline ?? '', contactPerson: row.contactPerson ?? '', phone: row.phone ?? '', email: row.email ?? '', physicalAddress: row.physicalAddress ?? '', notes: row.notes ?? '' })}>Edit</button>
          {row.isActive
            ? <button type="button" className={smallButtonClass} onClick={() => setRemoving(row)}>{row.projectCount > 0 ? 'Deactivate' : 'Delete'}</button>
            : <button type="button" className={smallButtonClass} onClick={() => reactivate(row)}>Reactivate</button>}
        </div>
      ),
    }] : []),
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Consultants"
        subtitle="Engineers, quantity surveyors, architects and other consultants you work with on projects."
        breadcrumbs={[{ label: 'Projects', to: '/projects' }, { label: 'Consultants' }]}
        primaryAction={canManage ? <button type="button" className={primaryButtonClass} onClick={() => setDraft(emptyDraft)}><Plus className="h-4 w-4" />Add consultant</button> : undefined}
      />

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)] sm:flex-row sm:items-center">
        <label className="flex flex-1 items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by firm, contact or discipline" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={showInactive} onChange={(event) => setShowInactive(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Show inactive
        </label>
      </div>

      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Directory unavailable' : 'No consultants yet'} emptyDescription={isError ? 'The directory could not be loaded.' : 'Add the consultant firms you work with.'} />

      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit consultant' : 'Add consultant'}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!draft?.firmName.trim() || createConsultant.isPending || updateConsultant.isPending} onClick={() => void save()}>Save</button>
          </div>
        }
      >
        {draft ? (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="consultant-name">Firm name</label>
                <input id="consultant-name" value={draft.firmName} onChange={(event) => setDraft({ ...draft, firmName: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="consultant-discipline">Discipline</label>
                <input id="consultant-discipline" value={draft.discipline} onChange={(event) => setDraft({ ...draft, discipline: event.target.value })} placeholder="e.g. M&E, QS, Architect" className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="consultant-contact">Contact person</label>
                <input id="consultant-contact" value={draft.contactPerson} onChange={(event) => setDraft({ ...draft, contactPerson: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="consultant-phone">Phone</label>
                <input id="consultant-phone" type="tel" value={draft.phone} onChange={(event) => setDraft({ ...draft, phone: event.target.value })} className={fieldClass} />
              </div>
              <div className="sm:col-span-2">
                <label className={labelClass} htmlFor="consultant-email">Email</label>
                <input id="consultant-email" type="email" value={draft.email} onChange={(event) => setDraft({ ...draft, email: event.target.value })} className={fieldClass} />
              </div>
            </div>
            <div>
              <label className={labelClass} htmlFor="consultant-address">Physical address</label>
              <input id="consultant-address" value={draft.physicalAddress} onChange={(event) => setDraft({ ...draft, physicalAddress: event.target.value })} className={fieldClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="consultant-notes">Notes</label>
              <textarea id="consultant-notes" rows={3} value={draft.notes} onChange={(event) => setDraft({ ...draft, notes: event.target.value })} className={textAreaClass} />
            </div>
          </div>
        ) : null}
      </FormDrawer>

      <ConfirmDialog
        open={removing !== null}
        title={removing?.projectCount ? `Deactivate ${removing.firmName}` : `Delete ${removing?.firmName ?? ''}`}
        message={removing?.projectCount
          ? 'This consultant is on existing projects, so it is deactivated rather than deleted. Those projects keep it.'
          : 'This consultant isn\'t on any project and will be deleted.'}
        confirmLabel={removing?.projectCount ? 'Deactivate' : 'Delete'}
        danger
        onCancel={() => setRemoving(null)}
        onConfirm={() => {
          if (removing) deleteConsultant.mutate(removing.id, { onSuccess: () => toast.success('Done') })
          setRemoving(null)
        }}
      />
    </div>
  )
}
