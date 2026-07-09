import { useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'

// TODO(backend): No exchange-rate controller exists in the current backend yet
// (confirmed against FRONTEND_BUILD_PLAN.md §12.1). Typed stub hooks return empty data
// until a real endpoint is available.
interface ExchangeRateItem {
  id: string
  baseCurrency: string
  quoteCurrency: string
  rate: number
  effectiveFrom: string
  updatedByName: string
}

function useExchangeRates() {
  return useQuery({ queryKey: ['finance', 'exchange-rates'], queryFn: async (): Promise<ExchangeRateItem[]> => [], staleTime: Infinity })
}

function useCreateExchangeRate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_payload: { baseCurrency: string; quoteCurrency: string; rate: number }): Promise<void> => {
      throw new Error('Exchange rates cannot be saved yet — no backend exchange-rate controller exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'exchange-rates'] }),
  })
}

export function ExchangeRatesPage() {
  const canEdit = usePermission('finance.manage')
  const { data, isLoading } = useExchangeRates()
  const createRate = useCreateExchangeRate()
  const [showNewRate, setShowNewRate] = useState(false)
  const [baseCurrency, setBaseCurrency] = useState('USD')
  const [quoteCurrency, setQuoteCurrency] = useState('KES')
  const [rate, setRate] = useState('')

  const submit = async () => {
    try {
      await createRate.mutateAsync({ baseCurrency, quoteCurrency, rate: Number(rate) })
      toast.success('Exchange rate saved')
      setShowNewRate(false)
    } catch {
      toast.error('Exchange rates are not yet backed by a live API — this is a UI stub.')
    }
  }

  const columns: DataTableColumn<ExchangeRateItem>[] = [
    { key: 'pair', header: 'Currency Pair', cell: (row) => <span className="font-medium text-slate-900">{row.baseCurrency} / {row.quoteCurrency}</span> },
    { key: 'rate', header: 'Rate', cell: (row) => row.rate.toFixed(4) },
    { key: 'effectiveFrom', header: 'Effective From', cell: (row) => formatDate(row.effectiveFrom) },
    { key: 'updatedByName', header: 'Updated By', cell: (row) => row.updatedByName },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Exchange Rates"
        subtitle="Currency pairs and manual rates used for multi-currency invoicing."
        primaryAction={
          canEdit ? (
            <button type="button" onClick={() => setShowNewRate(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />Add Rate
            </button>
          ) : undefined
        }
      />
      <FinanceWorkspaceNav />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        No exchange-rate controller exists in the current backend yet — this is the full intended UI wired against typed stub data until a real endpoint is connected.
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No exchange rates configured"
        emptyDescription="Manual currency pair rates will appear here once the backend endpoint is connected."
      />

      <FormDrawer open={showNewRate} title="Add Exchange Rate" onClose={() => setShowNewRate(false)}>
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">Base Currency</label>
              <input value={baseCurrency} onChange={(event) => setBaseCurrency(event.target.value.toUpperCase())} maxLength={3} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">Quote Currency</label>
              <input value={quoteCurrency} onChange={(event) => setQuoteCurrency(event.target.value.toUpperCase())} maxLength={3} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
            </div>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Rate</label>
            <input type="number" step="0.0001" min={0} value={rate} onChange={(event) => setRate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <button
            type="button"
            disabled={!rate || createRate.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            {createRate.isPending ? 'Saving…' : 'Save Rate'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
