import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { formatDate } from '../../../shared/utils/dates'
import { useSalesCurrency, useSetSalesExchangeRate } from '../api'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'

function RateRow({ currency, reporting, current, canManage }: { currency: string; reporting: string; current?: number; canManage: boolean }) {
  const setRate = useSetSalesExchangeRate()
  const [value, setValue] = useState(current ? String(current) : '')
  useEffect(() => setValue(current ? String(current) : ''), [current])

  const save = async () => {
    const rate = Number(value)
    if (!(rate > 0)) {
      toast.error('Enter a rate greater than zero')
      return
    }
    try {
      await setRate.mutateAsync({ currency, rate })
      toast.success(`1 ${currency} = ${rate} ${reporting} saved`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="flex items-center gap-2">
      <span className="w-14 shrink-0 text-sm font-semibold text-slate-900">1 {currency}</span>
      <span className="text-sm text-slate-500">=</span>
      <input
        aria-label={`${reporting} per ${currency}`}
        type="number"
        min="0"
        step="any"
        inputMode="decimal"
        value={value}
        disabled={!canManage}
        onChange={(event) => setValue(event.target.value)}
        className={fieldClass}
      />
      <span className="w-12 shrink-0 text-sm text-slate-500">{reporting}</span>
      {canManage ? (
        <button type="button" disabled={setRate.isPending || value === (current ? String(current) : '')} onClick={() => void save()} className="inline-flex h-10 shrink-0 items-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-40">
          Save
        </button>
      ) : null}
    </div>
  )
}

/**
 * Today's rates for converting deal and RFP/RFQ values into the reporting currency. Saving adds a
 * new row to the workspace's exchange-rate history (the same one Finance › Exchange Rates shows);
 * the newest rate is used from then on.
 */
export function ExchangeRatesDrawer({ open, onClose, canManage }: { open: boolean; onClose: () => void; canManage: boolean }) {
  const { data } = useSalesCurrency()
  const setRate = useSetSalesExchangeRate()
  const [newCurrency, setNewCurrency] = useState('')
  const [newRate, setNewRate] = useState('')
  const reporting = data?.reportingCurrency ?? '—'

  const addRate = async () => {
    const currency = newCurrency.trim().toUpperCase()
    const rate = Number(newRate)
    if (!/^[A-Z]{3}$/.test(currency)) {
      toast.error('Use a 3-letter currency code, e.g. USD')
      return
    }
    if (!(rate > 0)) {
      toast.error('Enter a rate greater than zero')
      return
    }
    try {
      await setRate.mutateAsync({ currency, rate })
      toast.success(`1 ${currency} = ${rate} ${reporting} saved`)
      setNewCurrency('')
      setNewRate('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer open={open} title="Exchange Rates" onClose={onClose}>
      <div className="space-y-5">
        <p className="text-sm text-slate-600">
          Pipeline and RFP/RFQ totals are shown in <span className="font-semibold text-slate-900">{reporting}</span>. Enter how many {reporting} one unit of each other currency is worth today.
          Earlier rates are kept in Finance › Exchange Rates; the newest one is used.
        </p>

        {data && data.missingCurrencies.length > 0 ? (
          <section className="space-y-3 rounded-xl border border-amber-200 bg-amber-50 p-4">
            <div className="text-sm font-medium text-amber-900">No rate yet — these deals are left out of the totals</div>
            {data.missingCurrencies.map((currency) => (
              <RateRow key={currency} currency={currency} reporting={reporting} canManage={canManage} />
            ))}
          </section>
        ) : null}

        <section className="space-y-3">
          <div className="text-sm font-medium text-slate-900">Rates in use</div>
          {data && data.rates.length === 0 ? <p className="text-sm text-slate-500">No exchange rates yet.</p> : null}
          {(data?.rates ?? []).map((rate) => (
            <div key={rate.currency} className="space-y-1">
              <RateRow currency={rate.currency} reporting={reporting} current={Number(rate.rateToReporting.toFixed(6))} canManage={canManage} />
              <div className="pl-16 text-xs text-slate-500">Since {formatDate(rate.effectiveFrom)}{rate.updatedByName ? ` · ${rate.updatedByName}` : ''}</div>
            </div>
          ))}
        </section>

        {canManage ? (
          <section className="space-y-2 border-t border-slate-200 pt-4">
            <div className="text-sm font-medium text-slate-900">Add a currency</div>
            <div className="flex items-center gap-2">
              <input aria-label="Currency code" value={newCurrency} onChange={(event) => setNewCurrency(event.target.value)} maxLength={3} placeholder="USD" className={`${fieldClass} w-20 uppercase`} />
              <span className="text-sm text-slate-500">=</span>
              <input aria-label={`${reporting} per unit`} type="number" min="0" step="any" inputMode="decimal" value={newRate} onChange={(event) => setNewRate(event.target.value)} placeholder="Rate" className={fieldClass} />
              <span className="w-12 shrink-0 text-sm text-slate-500">{reporting}</span>
              <button type="button" disabled={setRate.isPending} onClick={() => void addRate()} className="inline-flex h-10 shrink-0 items-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
                Add
              </button>
            </div>
          </section>
        ) : null}
      </div>
    </FormDrawer>
  )
}
