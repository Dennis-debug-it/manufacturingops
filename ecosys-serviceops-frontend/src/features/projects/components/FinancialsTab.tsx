import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { StatCard } from '../../../shared/components/StatCard'
import { formatCurrency } from '../../../shared/utils/format'
import { formatDate } from '../../../shared/utils/dates'
import { useBudgetLines, useCostEntries, useDeleteBudgetLine, useDeleteCostEntry, useProjectFinancials, useSaveBudgetLine, useSaveCostEntry } from '../siteApi'
import { costCategories, type BudgetLine, type CostCategory, type CostEntry, type FinancialsCategory, type ProjectDetail } from '../types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textOrNull, todayInput } from './formStyles'

type Draft =
  | { kind: 'budget'; id?: string; category: CostCategory; description: string; amount: string }
  | { kind: 'cost'; id?: string; category: CostCategory; description: string; amount: string; incurredOn: string }

type PendingDelete = { kind: 'budget'; line: BudgetLine } | { kind: 'cost'; entry: CostEntry }

function Figure({ label, value, hint, tone }: { label: string; value: string; hint?: string | undefined; tone?: string | undefined }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className={`mt-1 text-lg font-semibold ${tone ?? 'text-slate-900'}`}>{value}</div>
      {hint ? <div className="mt-0.5 text-xs text-slate-500">{hint}</div> : null}
    </div>
  )
}

/**
 * Budget against actual cost and the billing position (design section 7). Every figure is derived
 * from the records that hold it; only budget lines and one-off cost entries are typed in here.
 */
export function FinancialsTab({ project }: { project: ProjectDetail }) {
  const projectId = project.summary.id
  const isFinal = project.summary.stageCategory === 'Closed' || project.summary.stageCategory === 'Cancelled'
  const canManage = project.abilities.canManageFinancials && !isFinal
  const { data: financials, isLoading } = useProjectFinancials(projectId)
  const { data: budgetLines } = useBudgetLines(projectId)
  const { data: costEntries, isLoading: costsLoading } = useCostEntries(projectId)
  const saveBudget = useSaveBudgetLine(projectId)
  const deleteBudget = useDeleteBudgetLine(projectId)
  const saveCost = useSaveCostEntry(projectId)
  const deleteCost = useDeleteCostEntry(projectId)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [pendingDelete, setPendingDelete] = useState<PendingDelete | null>(null)
  const money = (value: number) => formatCurrency(value, project.summary.currency)
  const percent = (value: number | null) => (value === null ? 'n/a' : `${value.toFixed(1)}%`)

  const save = async () => {
    if (!draft) return
    const base = { category: draft.category, amount: Number(draft.amount) }
    try {
      if (draft.kind === 'budget') await saveBudget.mutateAsync({ ...base, ...(draft.id ? { lineId: draft.id } : {}), description: textOrNull(draft.description) })
      else await saveCost.mutateAsync({ ...base, ...(draft.id ? { entryId: draft.id } : {}), description: draft.description.trim(), incurredOn: draft.incurredOn })
      toast.success(draft.kind === 'budget' ? 'Budget saved' : 'Cost recorded')
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const confirmDelete = async () => {
    if (!pendingDelete) return
    try {
      if (pendingDelete.kind === 'budget') await deleteBudget.mutateAsync(pendingDelete.line.id)
      else await deleteCost.mutateAsync(pendingDelete.entry.id)
    } catch {
      // toast from the API client
    } finally {
      setPendingDelete(null)
    }
  }

  const categoryColumns: DataTableColumn<FinancialsCategory & { id: string }>[] = [
    { key: 'category', header: 'Category', cell: (row) => <div><div className="font-medium text-slate-900">{row.category}</div><div className="text-xs text-slate-500">{row.source}</div></div> },
    { key: 'budget', header: 'Budget', cell: (row) => money(row.budget) },
    { key: 'actual', header: 'Actual', cell: (row) => <span className={row.budget > 0 && row.actual > row.budget ? 'font-medium text-rose-700' : ''}>{money(row.actual)}</span> },
    { key: 'variance', header: 'Remaining', cell: (row) => (row.budget > 0 ? money(row.budget - row.actual) : '—') },
  ]

  const costColumns: DataTableColumn<CostEntry>[] = [
    { key: 'date', header: 'Date', cell: (row) => formatDate(row.incurredOn) },
    { key: 'category', header: 'Category', cell: (row) => row.category },
    { key: 'description', header: 'Description', cell: (row) => row.description },
    { key: 'amount', header: 'Amount', cell: (row) => money(row.amount) },
    {
      key: 'actions',
      header: '',
      cell: (row) => canManage ? (
        <div className="flex justify-end gap-2">
          <button type="button" className={smallButtonClass} onClick={() => setDraft({ kind: 'cost', id: row.id, category: row.category, description: row.description, amount: String(row.amount), incurredOn: row.incurredOn })}>Edit</button>
          <button type="button" className={smallButtonClass} onClick={() => setPendingDelete({ kind: 'cost', entry: row })}>Remove</button>
        </div>
      ) : null,
    },
  ]

  if (isLoading || !financials) return <p className="text-sm text-slate-500">Loading financials…</p>

  return (
    <div className="space-y-5">
      {financials.unconvertedCurrencies?.length ? (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          Some costs are in {financials.unconvertedCurrencies.join(', ')} and are left out of these figures. Add an exchange rate to {financials.currency} in Finance → Exchange Rates to include them.
        </div>
      ) : null}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Revised value" value={money(financials.revisedValue)} />
        <StatCard label="Actual cost" value={money(financials.actualCost)} />
        <StatCard label="Indicative margin" value={percent(financials.indicativeMarginPercent)} tone={(financials.indicativeMarginPercent ?? 0) < 0 ? 'danger' : 'default'} />
        <StatCard label="Outstanding" value={money(financials.outstanding)} tone={financials.overdue > 0 ? 'warning' : 'default'} />
      </div>

      <section className="space-y-2">
        <h3 className="text-sm font-semibold text-slate-900">Cost against budget</h3>
        <DataTable columns={categoryColumns} data={financials.categories.map((row) => ({ ...row, id: row.category }))} />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <Figure label="Budget" value={money(financials.budgetTotal)} />
          <Figure label="Committed" value={money(financials.committedCost)} hint="Open purchase orders" />
          <Figure label="Forecast margin" value={percent(financials.forecastMarginPercent)} hint="After committed cost" />
          <Figure label="Man-days" value={String(financials.manDays)} hint={financials.uncostedDays > 0 ? `${financials.uncostedDays} not yet costed: set the rates on Labour & wages` : 'From the muster roll'} tone={financials.uncostedDays > 0 ? 'text-amber-700' : undefined} />
        </div>
      </section>

      <section className="space-y-2">
        <h3 className="text-sm font-semibold text-slate-900">Billing</h3>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <Figure label="Certified to date" value={money(financials.certifiedToDate)} />
          <Figure label="Invoiced" value={money(financials.invoiced)} hint="Before tax" />
          <Figure label="Cash received" value={money(financials.cashReceived)} />
          <Figure label="Tax withheld" value={money(financials.taxWithheld)} hint="Withholding certificates" />
          <Figure label="Overdue" value={money(financials.overdue)} tone={financials.overdue > 0 ? 'text-rose-700' : undefined} />
          <Figure label="Retention held" value={money(financials.retentionHeld)} hint={financials.retentionDueNow > 0 ? `${money(financials.retentionDueNow)} can be released now` : undefined} />
          <Figure label="Advance outstanding" value={money(financials.advanceOutstanding)} />
          <Figure label="Unbilled value" value={money(financials.unbilledValue)} hint={financials.pendingVariations !== 0 ? `Plus ${money(financials.pendingVariations)} in pending variations` : undefined} />
        </div>
        <p className="text-xs text-slate-500">Margins are indicative: amounts exclude tax, and costs not yet recorded in Ecosys (accruals, unbilled supplier work) aren't included.</p>
      </section>

      <section className="space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-sm font-semibold text-slate-900">Budget lines</h3>
          {canManage ? <button type="button" className={secondaryButtonClass} onClick={() => setDraft({ kind: 'budget', category: 'Materials', description: '', amount: '' })}><Plus className="h-4 w-4" />Add budget line</button> : null}
        </div>
        {(budgetLines ?? []).length === 0 ? <p className="text-sm text-slate-500">No budget set.</p> : (
          <div className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
            {(budgetLines ?? []).map((line) => (
              <div key={line.id} className="flex flex-wrap items-center justify-between gap-2 px-4 py-2 text-sm">
                <div><span className="font-medium text-slate-900">{line.category}</span>{line.description ? <span className="text-slate-500"> · {line.description}</span> : null}</div>
                <div className="flex items-center gap-2">
                  <span className="font-medium">{money(line.amount)}</span>
                  {canManage ? (
                    <>
                      <button type="button" className={smallButtonClass} onClick={() => setDraft({ kind: 'budget', id: line.id, category: line.category, description: line.description ?? '', amount: String(line.amount) })}>Edit</button>
                      <button type="button" className={smallButtonClass} onClick={() => setPendingDelete({ kind: 'budget', line })}>Remove</button>
                    </>
                  ) : null}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      <section className="space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">Other costs</h3>
            <p className="text-xs text-slate-500">Equipment hire and one-off costs with no bill, expense claim or material issue behind them.</p>
          </div>
          {canManage ? <button type="button" className={secondaryButtonClass} onClick={() => setDraft({ kind: 'cost', category: 'Equipment', description: '', amount: '', incurredOn: todayInput() })}><Plus className="h-4 w-4" />Record cost</button> : null}
        </div>
        <DataTable columns={costColumns} data={costEntries ?? []} loading={costsLoading} emptyTitle="No other costs" emptyDescription="Materials, labour, bills and expense claims are counted automatically." />
      </section>

      <FormDrawer
        open={draft !== null}
        title={draft?.kind === 'budget' ? (draft.id ? 'Edit budget line' : 'Add budget line') : draft?.id ? 'Edit cost' : 'Record cost'}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button>
            <button type="button" className={primaryButtonClass}
              disabled={!draft || !(Number(draft.amount) >= (draft.kind === 'budget' ? 0 : 0.01)) || draft.amount === '' || (draft.kind === 'cost' && !draft.description.trim())}
              onClick={() => void save()}>Save</button>
          </div>
        }
      >
        {draft ? (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="cost-category">Category</label>
                <select id="cost-category" value={draft.category} onChange={(event) => setDraft({ ...draft, category: event.target.value as CostCategory })} className={fieldClass}>
                  {costCategories.map((category) => <option key={category} value={category}>{category}</option>)}
                </select>
              </div>
              <div>
                <label className={labelClass} htmlFor="cost-amount">Amount ({project.summary.currency}, before tax)</label>
                <input id="cost-amount" type="number" min="0" step="0.01" inputMode="decimal" value={draft.amount} onChange={(event) => setDraft({ ...draft, amount: event.target.value })} className={fieldClass} />
              </div>
            </div>
            <div>
              <label className={labelClass} htmlFor="cost-description">Description{draft.kind === 'budget' ? ' (optional)' : ''}</label>
              <input id="cost-description" value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} className={fieldClass} />
            </div>
            {draft.kind === 'cost' ? (
              <div>
                <label className={labelClass} htmlFor="cost-date">Date</label>
                <input id="cost-date" type="date" value={draft.incurredOn} onChange={(event) => setDraft({ ...draft, incurredOn: event.target.value })} className={fieldClass} />
              </div>
            ) : null}
          </div>
        ) : null}
      </FormDrawer>

      <ConfirmDialog
        open={pendingDelete !== null}
        title={pendingDelete?.kind === 'budget' ? 'Remove budget line' : 'Remove cost'}
        message={pendingDelete?.kind === 'budget' ? `Remove the ${pendingDelete.line.category} budget of ${money(pendingDelete.line.amount)}?` : pendingDelete ? `Remove "${pendingDelete.entry.description}" (${money(pendingDelete.entry.amount)})? This is recorded in the audit log.` : ''}
        confirmLabel="Remove"
        danger
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => void confirmDelete()}
      />
    </div>
  )
}
