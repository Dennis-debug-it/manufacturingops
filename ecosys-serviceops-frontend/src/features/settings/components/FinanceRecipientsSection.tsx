import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { api } from '../../../shared/api/apiClient'
import { usePermission } from '../../../shared/hooks/usePermission'

type AppliesTo = 'All' | 'Invoice' | 'Receipt' | 'Statement'

interface FinanceRecipient {
  email: string
  appliesTo: AppliesTo
}

const appliesToOptions: Array<{ value: AppliesTo; label: string }> = [
  { value: 'All', label: 'All finance emails' },
  { value: 'Invoice', label: 'Invoices only' },
  { value: 'Receipt', label: 'Receipts only' },
  { value: 'Statement', label: 'Statements only' },
]

const url = '/api/settings/finance-recipients'
const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50'

/**
 * Your own finance addresses, copied on invoices, receipts and statements sent to clients. Documents
 * always go To the client; these are CC only.
 */
export function FinanceRecipientsSection() {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: ['settings', 'finance-recipients'], queryFn: () => api.get<FinanceRecipient[]>(url) })
  const [rows, setRows] = useState<FinanceRecipient[]>([])

  useEffect(() => {
    if (data) setRows(data)
  }, [data])

  const save = useMutation({
    mutationFn: (value: FinanceRecipient[]) => api.put<FinanceRecipient[]>(url, value.filter((row) => row.email.trim())),
    onSuccess: (saved) => {
      queryClient.setQueryData(['settings', 'finance-recipients'], saved)
      toast.success('Finance email copies saved')
    },
  })

  const update = (index: number, patch: Partial<FinanceRecipient>) =>
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...patch } : row)))

  return (
    <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div>
        <h2 className="text-sm font-semibold text-slate-900">Copy finance emails to</h2>
        <p className="mt-1 text-sm text-slate-500">
          Invoices, receipts and statements always go to the client's own email. Add your finance team here to receive a copy (CC).
        </p>
      </div>

      {isLoading ? <p className="text-sm text-slate-500">Loading…</p> : (
        <div className="space-y-2">
          {rows.length === 0 ? <p className="text-sm text-slate-500">Nobody is copied yet.</p> : null}
          {rows.map((row, index) => (
            <div key={index} className="flex gap-2">
              <input type="email" value={row.email} disabled={!canManage} placeholder="accounts@yourcompany.com" onChange={(event) => update(index, { email: event.target.value })} className={fieldClass} />
              <select value={row.appliesTo} disabled={!canManage} onChange={(event) => update(index, { appliesTo: event.target.value as AppliesTo })} className={`${fieldClass} max-w-[12rem]`}>
                {appliesToOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
              {canManage ? (
                <button type="button" onClick={() => setRows((current) => current.filter((_, i) => i !== index))} className="rounded p-2 text-slate-400 hover:bg-slate-100 hover:text-rose-600" aria-label="Remove address">
                  <Trash2 className="h-4 w-4" />
                </button>
              ) : null}
            </div>
          ))}
        </div>
      )}

      {canManage ? (
        <div className="flex flex-wrap gap-2">
          <button type="button" onClick={() => setRows((current) => [...current, { email: '', appliesTo: 'All' }])} className="inline-flex h-10 items-center gap-1.5 rounded-lg border border-dashed border-slate-300 px-3 text-xs font-medium text-slate-600 hover:bg-slate-50">
            <Plus className="h-3.5 w-3.5" />Add address
          </button>
          <button type="button" onClick={() => save.mutate(rows)} disabled={save.isPending} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {save.isPending ? 'Saving…' : 'Save copies'}
          </button>
        </div>
      ) : null}
    </section>
  )
}
