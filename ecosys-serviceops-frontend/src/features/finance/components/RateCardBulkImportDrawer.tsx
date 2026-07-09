import { useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Download, Upload } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { downloadCsvTemplate, readCsvFile } from './financeImportUtils'
import type { UpsertRateCardPayload } from '../api'

const categories = ['Labour', 'Callout', 'Service', 'Transport', 'Material', 'Other'] as const
const timeTypes = ['Standard', 'Overtime', 'Emergency', 'Weekend', 'Holiday'] as const
const units = ['hour', 'visit', 'km', 'unit', 'day', 'job'] as const
const skillLevels = ['Junior', 'Mid', 'Senior', 'Specialist'] as const
const csvHeaders = ['name*', 'category*', 'time_type*', 'skill_level', 'unit_rate*', 'unit*', 'tax_rate', 'is_default'] as const

interface ValidationRow {
  rowIndex: number
  values: Record<string, string>
  errors: string[]
  parsed: UpsertRateCardPayload | undefined
}

function normalizeFromList<T extends readonly string[]>(value: string, list: T) {
  return list.find((item) => item.toLowerCase() === value.trim().toLowerCase()) ?? null
}

function validateRows(rows: Array<Record<string, string>>) {
  return rows.map((row, rowIndex): ValidationRow => {
    const errors: string[] = []
    const name = row['name*']?.trim()
    const category = normalizeFromList(row['category*'] ?? '', categories)
    const timeType = normalizeFromList(row['time_type*'] ?? '', timeTypes)
    const unit = normalizeFromList(row['unit*'] ?? '', units)
    const skillLevelRaw = row.skill_level?.trim() ?? ''
    const skillLevel = skillLevelRaw ? normalizeFromList(skillLevelRaw, skillLevels) : null
    const unitRate = Number(row['unit_rate*']?.trim())
    const taxRateValue = row.tax_rate?.trim()
    const taxRate = taxRateValue ? Number(taxRateValue) : null
    const isDefaultRaw = row.is_default?.trim().toUpperCase()

    if (!name) errors.push('Name is required.')
    if (!category) errors.push(`Category must be one of: ${categories.join(', ')}.`)
    if (!timeType) errors.push(`Time type must be one of: ${timeTypes.join(', ')}.`)
    if (!unit || !unit.trim()) errors.push(`Unit must be one of: ${units.join(', ')}.`)
    if (Number.isNaN(unitRate) || unitRate <= 0) errors.push('Unit rate must be a positive number.')
    if (skillLevelRaw && !skillLevel) errors.push(`Skill level must be one of: ${skillLevels.join(', ')}.`)
    if (taxRateValue && (Number.isNaN(Number(taxRateValue)) || (taxRate ?? 0) < 0 || (taxRate ?? 0) > 100)) errors.push('Tax rate must be between 0 and 100.')
    if (isDefaultRaw && isDefaultRaw !== 'TRUE' && isDefaultRaw !== 'FALSE') errors.push('is_default must be TRUE or FALSE.')

    const parsed: UpsertRateCardPayload | undefined = errors.length === 0 ? {
      name: name!,
      category: category!,
      timeType: timeType!,
      unitRate,
      unit: unit!,
      skillLevel,
      taxRate,
      isDefault: isDefaultRaw === 'TRUE',
      description: null,
    } : undefined

    return {
      rowIndex,
      values: row,
      errors,
      parsed,
    }
  })
}

interface RateCardBulkImportDrawerProps {
  open: boolean
  onClose: () => void
  onImport: (payload: UpsertRateCardPayload) => Promise<unknown>
}

export function RateCardBulkImportDrawer({ open, onClose, onImport }: RateCardBulkImportDrawerProps) {
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
      setError('Only CSV files are supported for rate card import.')
      return
    }

    try {
      setParsing(true)
      setError('')
      setResult(null)
      const parsed = await readCsvFile(file)
      const nextRows = parsed.rows
      setRows(nextRows)
      setValidations(validateRows(nextRows))
      if (nextRows.length === 0) {
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
    const toImport = candidates.filter((row): row is ValidationRow & { parsed: UpsertRateCardPayload } => Boolean(row.parsed))
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
      await queryClient.invalidateQueries({ queryKey: ['finance', 'rate-cards'] })
      if (created > 0) {
        toast.success(`Created ${created} rate card${created === 1 ? '' : 's'}`)
      }
    } finally {
      setImporting(false)
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Bulk Import Rate Cards"
      onClose={handleClose}
      footer={(
        <div className="flex flex-wrap items-center justify-between gap-2">
          <button
            type="button"
            onClick={() => downloadCsvTemplate(
              'rate_cards_import_template.csv',
              [
                '# category: Labour|Callout|Service|Transport|Material|Other',
                '# time_type: Standard|Overtime|Emergency|Weekend|Holiday',
                '# skill_level: Junior|Mid|Senior|Specialist',
                '# unit: hour|visit|km|unit|day|job',
                '# is_default: TRUE|FALSE',
              ],
              csvHeaders,
              [
                { 'name*': 'Standard Labour - Senior', 'category*': 'Labour', 'time_type*': 'Standard', skill_level: 'Senior', 'unit_rate*': '2500', 'unit*': 'hour', tax_rate: '16', is_default: 'FALSE' },
                { 'name*': 'Emergency Callout', 'category*': 'Callout', 'time_type*': 'Emergency', skill_level: '', 'unit_rate*': '5000', 'unit*': 'visit', tax_rate: '16', is_default: 'FALSE' },
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
              {importing ? 'Importing...' : 'Import Rate Cards'}
            </button>
          </div>
        </div>
      )}
    >
      <div className="space-y-4">
        <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
          Upload a CSV to create several rate cards at once. Use the template so category, unit, and time type values match the live finance setup.
        </div>

        <label className="flex min-h-32 cursor-pointer flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-8 text-center transition hover:border-slate-400 hover:bg-slate-50">
          <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-slate-100 text-slate-700">
            <Upload className="h-5 w-5" />
          </span>
          <span className="space-y-1">
            <span className="block text-sm font-medium text-slate-900">Choose a rate cards CSV file</span>
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
            <p className="text-xs text-slate-500">Import every valid row and keep the rows that need correction in the report.</p>
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
              Created {result.created} rate cards. Failed {result.failed}.
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
