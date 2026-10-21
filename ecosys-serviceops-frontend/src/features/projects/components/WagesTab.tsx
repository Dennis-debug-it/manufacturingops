import { useState } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, Download } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { formatCurrency } from '../../../shared/utils/format'
import { formatDate } from '../../../shared/utils/dates'
import { downloadWageSheet, useSetWorkerRate, useWageSheet, useWorkerRates } from '../siteApi'
import type { ProjectDetail, WageSheetRow, WorkerRates } from '../types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, shiftDateInput, smallButtonClass, textOrNull, todayInput } from './formStyles'

interface RateDraft {
  worker: WorkerRates
  effectiveFrom: string
  dailyRate: string
  note: string
}

/**
 * Office-only labour: each worker's daily rate (dated, so a new rate never rewrites earlier days)
 * and the wage sheet for paying casuals. Ecosys records what's owed; it doesn't pay anyone.
 */
export function WagesTab({ project }: { project: ProjectDetail }) {
  const projectId = project.summary.id
  const canManage = project.abilities.canManageFinancials && project.summary.stageCategory !== 'Cancelled'
  const { data: workers, isLoading } = useWorkerRates(projectId)
  const setRate = useSetWorkerRate(projectId)
  const today = todayInput()
  const [from, setFrom] = useState(shiftDateInput(today, -6))
  const [to, setTo] = useState(today)
  const { data: sheet, isLoading: sheetLoading } = useWageSheet(projectId, from, to)
  const [draft, setDraft] = useState<RateDraft | null>(null)
  const money = (value: number) => formatCurrency(value, project.summary.currency)
  const uncosted = (workers ?? []).filter((worker) => worker.uncostedDays > 0)

  const saveRate = async () => {
    if (!draft) return
    try {
      await setRate.mutateAsync({ workerId: draft.worker.workerId, effectiveFrom: draft.effectiveFrom, dailyRate: Number(draft.dailyRate), note: textOrNull(draft.note) })
      toast.success(`Rate set for ${draft.worker.fullName}`)
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const download = async () => {
    try {
      await downloadWageSheet(projectId, from, to, project.summary.projectNumber)
    } catch {
      toast.error('The wage sheet could not be downloaded.')
    }
  }

  const workerColumns: DataTableColumn<WorkerRates & { id: string }>[] = [
    { key: 'name', header: 'Worker', cell: (row) => <div><div className="font-medium text-slate-900">{row.fullName}{row.isActive ? '' : ' (inactive)'}</div><div className="text-xs text-slate-500">{[row.trade, row.engagementType, row.phone].filter(Boolean).join(' · ')}</div></div> },
    { key: 'rate', header: 'Daily rate', cell: (row) => (row.currentRate === null ? <span className="text-amber-700">Not set</span> : money(row.currentRate)) },
    { key: 'days', header: 'Man-days', cell: (row) => <span>{row.manDays}{row.uncostedDays > 0 ? <span className="text-amber-700"> ({row.uncostedDays} uncosted)</span> : null}</span> },
    { key: 'cost', header: 'Labour cost', cell: (row) => money(row.labourCost) },
    {
      key: 'actions',
      header: '',
      cell: (row) => canManage ? (
        <div className="flex justify-end">
          {/* A worker with no rate yet is usually owed from their first day, so default to it. */}
          <button type="button" className={smallButtonClass} onClick={() => setDraft({ worker: row, effectiveFrom: row.rates.length === 0 ? shiftDateInput(today, -30) : today, dailyRate: row.currentRate ? String(row.currentRate) : '', note: '' })}>
            {row.currentRate === null ? 'Set rate' : 'Change rate'}
          </button>
        </div>
      ) : null,
    },
  ]

  const sheetColumns: DataTableColumn<WageSheetRow & { id: string }>[] = [
    { key: 'name', header: 'Worker', cell: (row) => <div><div className="font-medium text-slate-900">{row.fullName}</div><div className="text-xs text-slate-500">{row.phone ?? 'No phone'}</div></div> },
    { key: 'days', header: 'Days', cell: (row) => `${row.fullDays} full, ${row.halfDays} half` },
    { key: 'manDays', header: 'Man-days', cell: (row) => row.manDays },
    { key: 'rate', header: 'Rate', cell: (row) => (row.currentRate === null ? '—' : money(row.currentRate)) },
    { key: 'amount', header: 'Owed', cell: (row) => <span>{money(row.amount)}{row.uncostedDays > 0 ? <span className="block text-xs text-amber-700">+ {row.uncostedDays} day(s) with no rate</span> : null}</span> },
  ]

  return (
    <div className="space-y-5">
      {uncosted.length > 0 ? (
        <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
          <AlertTriangle className="mt-0.5 h-4 w-4 flex-none" />
          <span>{uncosted.length} worker{uncosted.length === 1 ? ' has' : 's have'} days with no rate yet ({uncosted.map((worker) => worker.fullName).join(', ')}). Those days aren't counted in labour cost until a rate covers them.</span>
        </div>
      ) : null}

      <section className="space-y-2">
        <h3 className="text-sm font-semibold text-slate-900">Workers and daily rates</h3>
        <DataTable columns={workerColumns} data={(workers ?? []).map((row) => ({ ...row, id: row.workerId }))} loading={isLoading} emptyTitle="No workers" emptyDescription="Workers are added to the roster from the Site tab or the technician app." />
      </section>

      <section className="space-y-2">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">Wage sheet</h3>
            <p className="text-xs text-slate-500">Man-days at the rate in effect on each day. Export it for cash or M-Pesa payment.</p>
          </div>
          <div className="flex flex-wrap items-end gap-2">
            <div>
              <label className={labelClass} htmlFor="wages-from">From</label>
              <input id="wages-from" type="date" value={from} max={to} onChange={(event) => event.target.value && setFrom(event.target.value)} className={fieldClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="wages-to">To</label>
              <input id="wages-to" type="date" value={to} min={from} onChange={(event) => event.target.value && setTo(event.target.value)} className={fieldClass} />
            </div>
            <button type="button" className={secondaryButtonClass} disabled={!sheet || sheet.rows.length === 0} onClick={() => void download()}><Download className="h-4 w-4" />CSV</button>
          </div>
        </div>
        <DataTable columns={sheetColumns} data={(sheet?.rows ?? []).map((row) => ({ ...row, id: row.workerId }))} loading={sheetLoading} emptyTitle="No days marked" emptyDescription={`No muster marks between ${formatDate(from)} and ${formatDate(to)}.`} />
        {sheet && sheet.rows.length > 0 ? (
          <div className="flex flex-wrap justify-end gap-4 text-sm">
            <span>{sheet.totalManDays} man-days</span>
            <span className="font-semibold">Total owed {money(sheet.totalAmount)}</span>
          </div>
        ) : null}
      </section>

      <FormDrawer
        open={draft !== null}
        title={draft ? `Daily rate: ${draft.worker.fullName}` : ''}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!draft?.effectiveFrom || !(Number(draft?.dailyRate) > 0) || setRate.isPending} onClick={() => void saveRate()}>Save rate</button>
          </div>
        }
      >
        {draft ? (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="rate-amount">Rate per full day ({project.summary.currency})</label>
                <input id="rate-amount" type="number" min="0" step="1" inputMode="decimal" value={draft.dailyRate} onChange={(event) => setDraft({ ...draft, dailyRate: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="rate-from">Applies from</label>
                <input id="rate-from" type="date" value={draft.effectiveFrom} onChange={(event) => setDraft({ ...draft, effectiveFrom: event.target.value })} className={fieldClass} />
              </div>
            </div>
            <div>
              <label className={labelClass} htmlFor="rate-note">Note</label>
              <input id="rate-note" value={draft.note} onChange={(event) => setDraft({ ...draft, note: event.target.value })} placeholder="e.g. Promoted to foreman" className={fieldClass} />
            </div>
            <p className="text-xs text-slate-500">Days from this date are costed at the new rate until the next one. Earlier days keep their rate. A half day is half the rate.</p>
            {draft.worker.rates.length > 0 ? (
              <div className="rounded-lg border border-slate-200 p-3 text-sm">
                <div className="mb-1 text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Rate history</div>
                {draft.worker.rates.map((rate) => <div key={rate.id}>{formatDate(rate.effectiveFrom)}: {money(rate.dailyRate)}{rate.note ? ` · ${rate.note}` : ''}</div>)}
              </div>
            ) : null}
          </div>
        ) : null}
      </FormDrawer>
    </div>
  )
}
