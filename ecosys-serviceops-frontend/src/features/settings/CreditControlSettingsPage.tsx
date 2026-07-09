import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { useCreditTerms, useCreateCreditTerm, type UpsertCreditTermPayload } from '../finance/api'
import type { CreditTerm } from '../finance/types'

function NewCreditTermDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const createTerm = useCreateCreditTerm()
  const [form, setForm] = useState<UpsertCreditTermPayload>({
    name: '', code: '', netDays: 30, isDepositRequired: false, depositPercent: 0, creditLimit: null, description: null, isDefault: false,
  })

  const submit = async () => {
    try {
      await createTerm.mutateAsync(form)
      toast.success('Credit term created')
      onClose()
      setForm({ name: '', code: '', netDays: 30, isDepositRequired: false, depositPercent: 0, creditLimit: null, description: null, isDefault: false })
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer open={open} title="New Credit Term" onClose={onClose}>
      <div className="space-y-4">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Name</label>
          <input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Code</label>
          <input value={form.code} onChange={(event) => setForm((current) => ({ ...current, code: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Net Days</label>
          <input type="number" value={form.netDays} onChange={(event) => setForm((current) => ({ ...current, netDays: Number(event.target.value) }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Credit Limit (optional)</label>
          <input type="number" value={form.creditLimit ?? ''} onChange={(event) => setForm((current) => ({ ...current, creditLimit: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={form.isDepositRequired} onChange={(event) => setForm((current) => ({ ...current, isDepositRequired: event.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
          Deposit Required
        </label>
        {form.isDepositRequired ? (
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Deposit Percent</label>
            <input type="number" value={form.depositPercent} onChange={(event) => setForm((current) => ({ ...current, depositPercent: Number(event.target.value) }))} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
        ) : null}
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={form.isDefault} onChange={(event) => setForm((current) => ({ ...current, isDefault: event.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
          Set as Default
        </label>
        <button type="button" disabled={!form.name.trim() || !form.code.trim() || createTerm.isPending} onClick={submit} className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60">
          {createTerm.isPending ? 'Creating…' : 'Create Credit Term'}
        </button>
      </div>
    </FormDrawer>
  )
}

export function CreditControlSettingsPage() {
  const canManage = usePermission('settings.manage')
  const { data, isLoading, isError } = useCreditTerms()
  const [showNew, setShowNew] = useState(false)

  const columns: DataTableColumn<CreditTerm>[] = [
    { key: 'name', header: 'Name', cell: (row) => <span className="font-medium text-slate-900">{row.name}{row.isDefault ? <span className="ml-2 rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-medium text-slate-500">Default</span> : null}</span> },
    { key: 'code', header: 'Code', cell: (row) => row.code },
    { key: 'netDays', header: 'Net Days', cell: (row) => `${row.netDays} days` },
    { key: 'creditLimit', header: 'Credit Limit', cell: (row) => row.creditLimit != null ? row.creditLimit.toLocaleString() : 'Unlimited' },
    { key: 'isDepositRequired', header: 'Deposit', cell: (row) => (row.isDepositRequired ? <StatusBadge status={`${row.depositPercent}% required`} /> : '—') },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Credit Control"
        subtitle="Payment terms and credit limits applied to clients. Per-client escalation/hold stages are computed live on the Finance → Credit Control page from these terms and outstanding balances."
        primaryAction={canManage ? <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800"><Plus className="h-4 w-4" />New Credit Term</button> : undefined}
      />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Credit terms unavailable' : 'No credit terms configured'}
        emptyDescription={isError ? 'Credit terms could not be loaded.' : 'Create a credit term to define payment windows and limits for clients.'}
      />
      <NewCreditTermDrawer open={showNew} onClose={() => setShowNew(false)} />
    </div>
  )
}
