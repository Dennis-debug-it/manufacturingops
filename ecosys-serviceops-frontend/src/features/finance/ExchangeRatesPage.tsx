import { useState } from 'react'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'

interface ExchangeRateItem {
  id: string
  baseCurrency: string
  quoteCurrency: string
  rate: number
  effectiveFrom: string
  updatedByName: string
}

function useExchangeRates() {
  return useQuery({
    queryKey: ['finance', 'exchange-rates'],
    queryFn: () => api.get<ExchangeRateItem[]>('/api/exchange-rates'),
    staleTime: 30_000,
  })
}

function useCreateExchangeRate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { baseCurrency: string; quoteCurrency: string; rate: number }) =>
      api.post<ExchangeRateItem>('/api/exchange-rates', payload),
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
    await createRate.mutateAsync({ baseCurrency, quoteCurrency, rate: Number(rate) })
    toast.success('Exchange rate saved')
    setShowNewRate(false)
    setRate('')
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
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No exchange rates configured"
        emptyDescription="Add a currency pair rate for multi-currency invoicing."
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
