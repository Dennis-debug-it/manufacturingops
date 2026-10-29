import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useSalesTargets, useSaveSalesTargets } from '../api'

const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const TEAM = 'team'
const monthKey = (year: number, index: number) => `${year}-${String(index + 1).padStart(2, '0')}-01`

/** Monthly targets for the team and each person, a year at a time; blank means no target. */
export function TargetsDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [year, setYear] = useState(() => new Date().getFullYear())
  const { data } = useSalesTargets(year, open)
  const save = useSaveSalesTargets()
  const [cells, setCells] = useState<Record<string, string>>({})

  useEffect(() => {
    if (!data) return
    const next: Record<string, string> = {}
    data.targets.forEach((row) => { next[`${row.ownerUserId ?? TEAM}|${row.month.slice(0, 10)}`] = String(row.amount) })
    setCells(next)
  }, [data])

  const rows = [{ id: TEAM, fullName: 'Whole team' }, ...(data?.owners ?? [])]
  const submit = async () => {
    const targets = Object.entries(cells)
      .filter(([, value]) => Number(value) > 0)
      .map(([key, value]) => {
        const [owner = TEAM, month = ''] = key.split('|')
        return { ownerUserId: owner === TEAM ? null : owner, month: `${month}T00:00:00Z`, amount: Number(value) }
      })
    try {
      await save.mutateAsync({ year, targets })
      toast.success(`Targets for ${year} saved`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Sales targets"
      onClose={onClose}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-700">Close</button>
          {data?.canEdit ? <button type="button" disabled={save.isPending} onClick={() => void submit()} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">Save targets</button> : null}
        </div>
      }
    >
      <div className="space-y-3">
        <div className="flex items-center justify-between gap-3">
          <p className="text-sm text-slate-600">
            Won value to aim for each month, in {data?.reportingCurrency ?? 'the reporting currency'}. The team target is used as is; without one, the team's is what its people's add up to.
          </p>
          <select aria-label="Year" value={year} onChange={(event) => setYear(Number(event.target.value))} className="h-10 rounded-lg border border-slate-300 px-3 text-sm">
            {[year - 1, year, year + 1].map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
        </div>
        <div className="max-h-[60vh] overflow-auto rounded-lg border border-slate-200">
          <table className="min-w-full text-sm">
            <thead className="sticky top-0 bg-slate-50 text-xs text-slate-500">
              <tr>
                <th className="sticky left-0 bg-slate-50 px-3 py-2 text-left font-medium">Who</th>
                {months.map((month) => <th key={month} className="px-2 py-2 text-right font-medium">{month}</th>)}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((row) => (
                <tr key={row.id}>
                  <td className="sticky left-0 whitespace-nowrap bg-white px-3 py-1.5 font-medium text-slate-800">{row.fullName}</td>
                  {months.map((month, index) => {
                    const key = `${row.id}|${monthKey(year, index)}`
                    return (
                      <td key={month} className="px-1 py-1">
                        <input
                          aria-label={`${row.fullName} ${month} ${year}`}
                          type="number"
                          min={0}
                          disabled={!data?.canEdit}
                          value={cells[key] ?? ''}
                          onChange={(event) => setCells((current) => ({ ...current, [key]: event.target.value }))}
                          className="h-8 w-24 rounded border border-slate-200 px-2 text-right text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
                        />
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </FormDrawer>
  )
}
