import { useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Download, Upload } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import type { Client } from '../../clients/types'
import { downloadCsvTemplate, readCsvFile } from './financeImportUtils'
import type { UpsertPreferentialRatePayload } from '../api'

const statuses = ['Active', 'Inactive', 'Pending'] as const
const itemTypes = ['labour', 'material', 'callout', 'transport', 'service', 'other'] as const
const csvHeaders = ['client_name*', 'rate_name*', 'rate_type', 'item_type*', 'unit*', 'default_rate*', 'preferential_rate*', 'currency', 'effective_from*', 'effective_to', 'status', 'requires_approval'] as const

interface ValidationRow {
  rowIndex: number
  values: Record<string, string>
  errors: string[]
  parsed: UpsertPreferentialRatePayload | undefined
}

function normalizeFromList<T extends readonly string[]>(value: string, list: T) {
  return list.find((item) => item.toLowerCase() === value.trim().toLowerCase()) ?? null
}

function normalizeDate(value: string) {
  const trimmed = value.trim()
  if (!trimmed) return null
  if (!/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return null
  return new Date(`${trimmed}T00:00:00.000Z`).toISOString()
}

function normalizeBoolean(value: string) {
  const normalized = value.trim().toUpperCase()
  if (!normalized) return false
  if (normalized === 'TRUE') return true
  if (normalized === 'FALSE') return false
  return null
}

function validateRows(rows: Array<Record<string, string>>, clients: Client[]) {
  const clientLookup = new Map(clients.map((client) => [client.clientName.trim().toLowerCase(), client.id] as const))

  return rows.map((row, rowIndex): ValidationRow => {
    const errors: string[] = []
    const clientName = row['client_name*']?.trim()
    const clientId = clientName ? clientLookup.get(clientName.toLowerCase()) : null
    const rateName = row['rate_name*']?.trim()
    const rateType = row.rate_type?.trim() || 'Preferential'
    const itemType = normalizeFromList(row['item_type*'] ?? '', itemTypes)
    const unit = row['unit*']?.trim()
    const defaultRate = Number(row['default_rate*']?.trim())
    const preferentialRate = Number(row['preferential_rate*']?.trim())
    const currency = row.currency?.trim().toUpperCase() || 'KES'
    const effectiveFrom = normalizeDate(row['effective_from*'] ?? '')
    const effectiveToRaw = row.effective_to?.trim() ?? ''
    const effectiveTo = effectiveToRaw ? normalizeDate(effectiveToRaw) : null
    const status = normalizeFromList(row.status?.trim() || 'Pending', statuses)
    const requiresApproval = normalizeBoolean(row.requires_approval ?? '')

    if (!clientName) errors.push('Client name is required.')
    if (clientName && !clientId) errors.push(`Client "${clientName}" was not found.`)
    if (!rateName) errors.push('Rate name is required.')
    if (!itemType) errors.push(`Item type must be one of: ${itemTypes.join(', ')}.`)
    if (!unit) errors.push('Unit is required.')
    if (Number.isNaN(defaultRate) || defaultRate < 0) errors.push('Default rate must be 0 or more.')
    if (Number.isNaN(preferentialRate) || preferentialRate < 0) errors.push('Preferential rate must be 0 or more.')
    if (!effectiveFrom) errors.push('effective_from must use YYYY-MM-DD.')
    if (effectiveToRaw && !effectiveTo) errors.push('effective_to must use YYYY-MM-DD.')
    if (!status) errors.push(`Status must be one of: ${statuses.join(', ')}.`)
    if (requiresApproval === null) errors.push('requires_approval must be TRUE or FALSE.')

    const parsed: UpsertPreferentialRatePayload | undefined = errors.length === 0 && clientId && itemType && unit && effectiveFrom && status && requiresApproval !== null ? {
      clientId,
      rateName: rateName!,
      rateType,
      itemType,
      unit,
      defaultRate,
      preferentialRate,
      currency,
      effectiveFrom,
      effectiveTo,
      status,
      requiresApproval,
    } : undefined

    return {
      rowIndex,
      values: row,
      errors,
      parsed,
    }
  })
}

interface PreferentialRateBulkImportDrawerProps {
  open: boolean
  onClose: () => void
  clients: Client[]
  onImport: (payload: UpsertPreferentialRatePayload) => Promise<unknown>
}

export function PreferentialRateBulkImportDrawer({ open, onClose, clients, onImport }: PreferentialRateBulkImportDrawerProps) {
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [rows, setRows] = useState<Array<Record<string, string>>>([])
  const [validations, setValidations] = useState<ValidationRow[]>([])
  const [skipErrors, setSkipErrors] = useState(true)
  const [parsing, setParsing] = useState(false)
  const [importing, setImporting] = useState(false)
  const [progressDone, setProgressDone] = useState(0)
  const [error, setError] = useState('')
  const [result, setResult] = useState<{ created: number; failed: number; failures: string[] } | null>(null)

  const validRows = useMemo(() => validations.filter((row) => row.errors.length === 0), [validations])
  const invalidRows = validations.length - validRows.length

  function reset() {
    setFile(null)
    setRows([])
    setValidations([])
    setSkipErrors(true)
    setParsing(false)
    setImporting(false)
    setProgressDone(0)
    setError('')
    setResult(null)
  }

  function handleClose() {
    if (parsing || importing) {
      return
    }
    reset()
    onClose()
  }

  async function handleParse() {
    if (!file) {
      toast.error('Choose a CSV file first')
      return
    }
    if (!file.name.toLowerCase().endsWith('.csv')) {
      setError('Only CSV files are supported for preferential rate import.')
      return
    }

    try {
      setParsing(true)
      setError('')
      setResult(null)
      const parsed = await readCsvFile(file)
      setRows(parsed.rows)
      setValidations(validateRows(parsed.rows, clients))
      if (parsed.rows.length === 0) {
        toast.error('The selected file has no rows to import')
      }
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : 'Could not read this CSV file.')
    } finally {
      setParsing(false)
    }
  }

  async function handleImport() {
    const candidates = skipErrors ? validRows : validations
    const toImport = candidates.filter((row): row is ValidationRow & { parsed: UpsertPreferentialRatePayload } => Boolean(row.parsed))
    if (toImport.length === 0) {
      return
    }

    const failures: string[] = []
    let created = 0

    try {
      setImporting(true)
      setProgressDone(0)
      setError('')
      for (let index = 0; index < toImport.length; index += 1) {
        const row = toImport[index]!
        try {
          await onImport(row.parsed)
          created += 1
        } catch (nextError) {
          failures.push(`Row ${row.rowIndex + 2}: ${nextError instanceof Error ? nextError.message : 'Import failed.'}`)
        }
        setProgressDone(index + 1)
      }
      setResult({ created, failed: failures.length, failures })
      await queryClient.invalidateQueries({ queryKey: ['finance', 'preferential-rates'] })
      if (created > 0) {
        toast.success(`Created ${created} preferential rate${created === 1 ? '' : 's'}`)
      }
    } finally {
      setImporting(false)
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Bulk Import Preferential Rates"
      onClose={handleClose}
      footer={(
        <div className="flex flex-wrap items-center justify-between gap-2">
          <button
            type="button"
            onClick={() => downloadCsvTemplate(
              'preferential_rates_import_template.csv',
              [
                '# item_type: labour|material|callout|transport|service|other',
                '# status: Active|Inactive|Pending',
                '# requires_approval: TRUE|FALSE',
                '# effective_from/effective_to: YYYY-MM-DD',
              ],
              csvHeaders,
              [
                { 'client_name*': 'Acme Facilities', 'rate_name*': 'Preferred Generator Labour', rate_type: 'Preferential', 'item_type*': 'labour', 'unit*': 'hour', 'default_rate*': '3500', 'preferential_rate*': '3000', currency: 'KES', 'effective_from*': '2026-06-01', effective_to: '2026-12-31', status: 'Pending', requires_approval: 'TRUE' },
              ],
            )}
            disabled={parsing || importing}
            className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60"
          >
            <Download className="h-4 w-4" />
            Download Template
          </button>
          <div className="flex flex-wrap items-center gap-2">
            <button type="button" onClick={handleClose} disabled={parsing || importing} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              Close
            </button>
            <button type="button" onClick={() => void handleParse()} disabled={!file || parsing || importing} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              {parsing ? 'Validating...' : 'Validate CSV'}
            </button>
            <button type="button" onClick={() => void handleImport()} disabled={importing || validRows.length === 0} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
              {importing ? 'Importing...' : 'Import Rates'}
            </button>
          </div>
        </div>
      )}
    >
      <div className="space-y-4">
        <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
          Upload a CSV of client-specific pricing overrides. Client names must match existing clients exactly so the rates attach to the correct account.
        </div>

        <label className="flex min-h-32 cursor-pointer flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-8 text-center transition hover:border-slate-400 hover:bg-slate-50">
          <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-slate-100 text-slate-700">
            <Upload className="h-5 w-5" />
          </span>
          <span className="space-y-1">
            <span className="block text-sm font-medium text-slate-900">Choose a preferential rates CSV file</span>
            <span className="block text-sm text-slate-500">{file ? file.name : 'Only CSV files are supported.'}</span>
          </span>
          <input
            type="file"
            accept=".csv"
            className="hidden"
            onChange={(event) => {
              setFile(event.target.files?.[0] ?? null)
              setRows([])
              setValidations([])
              setResult(null)
              setError('')
            }}
          />
        </label>

        <label className="flex items-center justify-between rounded-2xl border border-slate-200 bg-white px-4 py-3 shadow-sm">
          <div>
            <p className="text-sm font-medium text-slate-900">Skip invalid rows</p>
            <p className="text-xs text-slate-500">Import every valid row and leave anything that needs correction in the results table.</p>
          </div>
          <input type="checkbox" checked={skipErrors} onChange={(event) => setSkipErrors(event.target.checked)} />
        </label>

        {validations.length > 0 ? (
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Rows</p>
              <p className="mt-2 text-2xl font-semibold text-slate-900">{validations.length}</p>
            </div>
            <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Valid</p>
              <p className="mt-2 text-2xl font-semibold text-emerald-600">{validRows.length}</p>
            </div>
            <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Needs Review</p>
              <p className="mt-2 text-2xl font-semibold text-amber-600">{invalidRows}</p>
            </div>
          </div>
        ) : null}

        {error ? <div className="rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</div> : null}

        {rows.length > 0 ? (
          <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
            <div className="max-h-[22rem] overflow-auto">
              <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                <thead className="sticky top-0 bg-slate-50">
                  <tr>
                    <th className="px-4 py-3 font-medium text-slate-600">Row</th>
                    {csvHeaders.map((header) => (
                      <th key={header} className="px-4 py-3 font-medium text-slate-600">{header}</th>
                    ))}
                    <th className="px-4 py-3 font-medium text-slate-600">Errors</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {validations.map((row) => (
                    <tr key={row.rowIndex}>
                      <td className="px-4 py-3 text-slate-700">{row.rowIndex + 2}</td>
                      {csvHeaders.map((header) => (
                        <td key={header} className="px-4 py-3 text-slate-700">{row.values[header] || '-'}</td>
                      ))}
                      <td className="px-4 py-3 text-rose-700">{row.errors.join(' ') || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}

        {importing ? (
          <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
            Import progress: {progressDone} of {skipErrors ? validRows.length : validations.filter((row) => row.parsed).length}
          </div>
        ) : null}

        {result ? (
          <div className="space-y-3">
            <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
              Created {result.created} preferential rates. Failed {result.failed}.
            </div>
            {result.failures.length > 0 ? (
              <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                <div className="max-h-40 overflow-auto px-4 py-3 text-sm text-rose-700">
                  {result.failures.map((failure) => <p key={failure}>{failure}</p>)}
                </div>
              </div>
            ) : null}
          </div>
        ) : null}
      </div>
    </FormDrawer>
  )
}
