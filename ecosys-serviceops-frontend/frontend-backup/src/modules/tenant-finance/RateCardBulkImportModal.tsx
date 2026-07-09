import { useCallback, useRef, useState } from 'react'
import Papa from 'papaparse'
import { CheckCircle, Download, Upload, XCircle } from 'lucide-react'
import { Modal } from '../../components/ui/Modal'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import type { UpsertRateCardInput } from '../../types/api'

// ─── Constants ────────────────────────────────────────────────────────────────

const VALID_CATEGORIES = ['Labour', 'Callout', 'Service', 'Transport', 'Material', 'Other'] as const
const VALID_TIME_TYPES = ['Standard', 'Overtime', 'Emergency', 'Weekend', 'Holiday'] as const
const VALID_UNITS = ['hour', 'visit', 'km', 'unit', 'day', 'job'] as const
const VALID_SKILL_LEVELS = ['Junior', 'Mid', 'Senior', 'Specialist'] as const

type ValidCategory = (typeof VALID_CATEGORIES)[number]
type ValidTimeType = (typeof VALID_TIME_TYPES)[number]
type ValidUnit = (typeof VALID_UNITS)[number]

const CSV_HEADERS = ['name*', 'category*', 'time_type*', 'skill_level', 'unit_rate*', 'unit*', 'tax_rate', 'is_default']

const CSV_COMMENT = [
  '# Valid values:',
  '# category: Labour|Callout|Service|Transport|Material|Other',
  '# time_type: Standard|Overtime|Emergency|Weekend|Holiday',
  '# skill_level: Junior|Mid|Senior|Specialist  (leave blank for Any)',
  '# unit: hour|visit|km|unit|day|job',
  '# is_default: TRUE|FALSE',
]

type CsvRow = Record<string, string>

type ValidationResult = {
  row: CsvRow
  rowIndex: number
  errors: string[]
  parsed?: UpsertRateCardInput
}

type ImportResult = { created: number; failed: number; failures: string[] }

// ─── Template ─────────────────────────────────────────────────────────────────

const EXAMPLE_ROWS: CsvRow[] = [
  { 'name*': 'Standard Labour - Senior', 'category*': 'Labour', 'time_type*': 'Standard', skill_level: 'Senior', 'unit_rate*': '2500', 'unit*': 'hour', tax_rate: '16', is_default: 'FALSE' },
  { 'name*': 'Overtime Labour', 'category*': 'Labour', 'time_type*': 'Overtime', skill_level: '', 'unit_rate*': '3750', 'unit*': 'hour', tax_rate: '16', is_default: 'FALSE' },
  { 'name*': 'Emergency Callout', 'category*': 'Callout', 'time_type*': 'Emergency', skill_level: '', 'unit_rate*': '5000', 'unit*': 'visit', tax_rate: '16', is_default: 'FALSE' },
  { 'name*': 'Standard Transport', 'category*': 'Transport', 'time_type*': 'Standard', skill_level: '', 'unit_rate*': '85', 'unit*': 'km', tax_rate: '16', is_default: 'FALSE' },
  { 'name*': 'Generator Service', 'category*': 'Service', 'time_type*': 'Standard', skill_level: 'Specialist', 'unit_rate*': '15000', 'unit*': 'job', tax_rate: '16', is_default: 'FALSE' },
]

function downloadTemplate() {
  const header = CSV_HEADERS.join(',')
  const rows = EXAMPLE_ROWS.map((r) =>
    CSV_HEADERS.map((h) => `"${r[h] ?? ''}"`).join(','),
  )
  const csv = [...CSV_COMMENT, header, ...rows].join('\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
  const a = document.createElement('a')
  a.href = url
  a.download = 'rate_cards_import_template.csv'
  a.click()
  URL.revokeObjectURL(url)
}

// ─── Normalise ────────────────────────────────────────────────────────────────

function normaliseCategory(v: string): ValidCategory | null {
  const found = VALID_CATEGORIES.find((c) => c.toLowerCase() === v.trim().toLowerCase())
  return found ?? null
}

function normaliseTimeType(v: string): ValidTimeType | null {
  const found = VALID_TIME_TYPES.find((t) => t.toLowerCase() === v.trim().toLowerCase())
  return found ?? null
}

function normaliseUnit(v: string): ValidUnit | null {
  const found = VALID_UNITS.find((u) => u.toLowerCase() === v.trim().toLowerCase())
  return found ?? null
}

function normaliseSkillLevel(v: string): string | null {
  if (!v?.trim()) return null
  const found = VALID_SKILL_LEVELS.find((s) => s.toLowerCase() === v.trim().toLowerCase())
  return found ?? null
}

// ─── Validation ───────────────────────────────────────────────────────────────

function validateRows(rows: CsvRow[]): ValidationResult[] {
  return rows.map((row, i) => {
    const errors: string[] = []

    const name = row['name*']?.trim()
    if (!name) errors.push('name is required.')

    const cat = normaliseCategory(row['category*'] ?? '')
    if (!cat) errors.push(`Invalid category "${row['category*']}". Use: ${VALID_CATEGORIES.join('|')}.`)

    const tt = normaliseTimeType(row['time_type*'] ?? '')
    if (!tt) errors.push(`Invalid time_type "${row['time_type*']}". Use: ${VALID_TIME_TYPES.join('|')}.`)

    const unitRateRaw = row['unit_rate*']?.trim()
    const unitRate = parseFloat(unitRateRaw ?? '')
    if (!unitRateRaw || Number.isNaN(unitRate) || unitRate <= 0)
      errors.push('unit_rate must be a positive number.')

    const unitVal = normaliseUnit(row['unit*'] ?? '')
    if (!unitVal) errors.push(`Invalid unit "${row['unit*']}". Use: ${VALID_UNITS.join('|')}.`)

    const taxRateRaw = row['tax_rate']?.trim()
    let taxRate: number | null = null
    if (taxRateRaw) {
      taxRate = parseFloat(taxRateRaw)
      if (Number.isNaN(taxRate) || taxRate < 0 || taxRate > 100)
        errors.push('tax_rate must be a number between 0 and 100.')
    }

    const skillRaw = row['skill_level']?.trim()
    if (skillRaw && !normaliseSkillLevel(skillRaw))
      errors.push(`Invalid skill_level "${skillRaw}". Use: ${VALID_SKILL_LEVELS.join('|')} or leave blank.`)

    const isDefaultRaw = row['is_default']?.trim().toUpperCase()
    if (isDefaultRaw && isDefaultRaw !== 'TRUE' && isDefaultRaw !== 'FALSE')
      errors.push('is_default must be TRUE or FALSE.')

    const parsed: UpsertRateCardInput | undefined =
      errors.length === 0
        ? {
            name: name!,
            category: cat!,
            timeType: tt!,
            skillLevel: skillRaw ? normaliseSkillLevel(skillRaw) : null,
            unitRate,
            unit: unitVal!,
            taxRate: taxRateRaw ? taxRate : null,
            isDefault: isDefaultRaw === 'TRUE',
            description: null,
          }
        : undefined

    return { row, rowIndex: i, errors, parsed }
  })
}

// ─── Batching ─────────────────────────────────────────────────────────────────

async function batchRun<T, R>(
  items: T[],
  batchSize: number,
  fn: (item: T) => Promise<R>,
  onProgress: (done: number, total: number) => void,
): Promise<PromiseSettledResult<R>[]> {
  const results: PromiseSettledResult<R>[] = []
  for (let i = 0; i < items.length; i += batchSize) {
    const batch = items.slice(i, i + batchSize)
    const batchResults = await Promise.allSettled(batch.map(fn))
    results.push(...batchResults)
    onProgress(Math.min(i + batchSize, items.length), items.length)
  }
  return results
}

// ─── Step indicator ───────────────────────────────────────────────────────────

function StepIndicator({ current }: { current: 1 | 2 }) {
  const steps = [
    { n: 1 as const, label: 'Upload & validate' },
    { n: 2 as const, label: 'Import & results' },
  ]
  return (
    <div className="mb-6 flex items-center gap-0">
      {steps.map((step, idx) => (
        <div key={step.n} className="flex flex-1 items-center last:flex-none">
          <div className="flex items-center gap-2">
            <div
              className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold transition-colors ${
                step.n < current
                  ? 'bg-emerald-500 text-white'
                  : step.n === current
                  ? 'bg-teal-600 text-white ring-4 ring-teal-600/20'
                  : 'bg-white/10 text-muted'
              }`}
            >
              {step.n < current ? <CheckCircle className="h-4 w-4" /> : step.n}
            </div>
            <span className={`hidden text-sm sm:inline ${step.n === current ? 'font-semibold text-app' : 'text-muted'}`}>
              {step.label}
            </span>
          </div>
          {idx < steps.length - 1 && (
            <div className={`mx-3 h-px flex-1 ${step.n < current ? 'bg-emerald-500/60' : 'bg-white/10'}`} />
          )}
        </div>
      ))}
    </div>
  )
}

// ─── Preview table ────────────────────────────────────────────────────────────

function PreviewTable({ rows, validations }: { rows: CsvRow[]; validations: ValidationResult[] }) {
  const display = rows.slice(0, 20)
  return (
    <div className="space-y-1.5">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Preview</p>
      <div className="overflow-hidden rounded-[20px] border border-app">
        <div className="max-h-52 overflow-x-auto">
          <table className="min-w-full text-xs">
            <thead className="sticky top-0 z-10 bg-overlay/80">
              <tr>
                {CSV_HEADERS.map((h) => (
                  <th key={h} className="whitespace-nowrap px-3 py-2 text-left font-semibold uppercase tracking-wider text-muted">
                    {h}
                  </th>
                ))}
                <th className="px-3 py-2 text-left font-semibold uppercase tracking-wider text-muted">Status</th>
              </tr>
            </thead>
            <tbody>
              {display.map((row, i) => {
                const isInvalid = (validations[i]?.errors.length ?? 0) > 0
                return (
                  <tr
                    key={i}
                    className={`border-t border-app/50 ${isInvalid ? 'border-l-2 border-l-rose-400 bg-rose-500/5' : ''}`}
                  >
                    {CSV_HEADERS.map((h) => (
                      <td key={h} className="whitespace-nowrap px-3 py-2 text-app">
                        {row[h] || '—'}
                      </td>
                    ))}
                    <td className="px-3 py-2">
                      {isInvalid
                        ? <XCircle className="h-4 w-4 text-rose-400" />
                        : <CheckCircle className="h-4 w-4 text-emerald-400" />
                      }
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        {rows.length > 20 && (
          <p className="border-t border-app px-3 py-2 text-xs text-muted">
            Showing 20 of {rows.length} rows
          </p>
        )}
      </div>
    </div>
  )
}

// ─── Main component ───────────────────────────────────────────────────────────

export function RateCardBulkImportModal({
  open,
  onClose,
  onSuccess,
}: {
  open: boolean
  onClose: () => void
  onSuccess: () => void
}) {
  const [step, setStep] = useState<1 | 2>(1)
  const [isDragging, setIsDragging] = useState(false)
  const [rows, setRows] = useState<CsvRow[]>([])
  const [validations, setValidations] = useState<ValidationResult[]>([])
  const [parseError, setParseError] = useState('')
  const [skipErrors, setSkipErrors] = useState(false)
  const [importing, setImporting] = useState(false)
  const [progressDone, setProgressDone] = useState(0)
  const [progressTotal, setProgressTotal] = useState(0)
  const [result, setResult] = useState<ImportResult | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  function reset() {
    setStep(1)
    setIsDragging(false)
    setRows([])
    setValidations([])
    setParseError('')
    setSkipErrors(false)
    setImporting(false)
    setProgressDone(0)
    setProgressTotal(0)
    setResult(null)
  }

  function handleClose() {
    if (importing) return
    reset()
    onClose()
  }

  // ── File handling ─────────────────────────────────────────────────────────

  const processFile = useCallback((file: File | null | undefined) => {
    if (!file) return
    if (!file.name.toLowerCase().endsWith('.csv')) {
      setParseError('Only .csv files are accepted for rate card import.')
      return
    }
    setParseError('')
    setRows([])
    setValidations([])

    Papa.parse<CsvRow>(file, {
      header: true,
      skipEmptyLines: true,
      comments: '#',
      complete: (results) => {
        const parsed = results.data as CsvRow[]
        const validated = validateRows(parsed)
        setRows(parsed)
        setValidations(validated)
      },
      error: (err) => setParseError(err.message),
    })
  }, [])

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setIsDragging(false)
    processFile(e.dataTransfer.files[0])
  }

  // ── Import ────────────────────────────────────────────────────────────────

  async function runImport() {
    const toImport = skipErrors
      ? validations.filter((v) => v.errors.length === 0)
      : validations

    if (toImport.length === 0) return

    setImporting(true)
    setProgressDone(0)
    setProgressTotal(toImport.length)

    const inputs = toImport.map((v) => v.parsed!)

    const results = await batchRun(
      inputs,
      10,
      (input) => tenantFinanceService.createRateCard(input),
      (done, _total) => setProgressDone(done),
    )

    let created = 0
    const failures: string[] = []
    results.forEach((r, i) => {
      if (r.status === 'fulfilled') {
        created++
      } else {
        const originalRow = toImport[i].rowIndex + 1
        failures.push(
          `Row ${originalRow} (${toImport[i].parsed?.name ?? '?'}): ${
            r.reason instanceof Error ? r.reason.message : 'Failed'
          }`,
        )
      }
    })

    setResult({ created, failed: failures.length, failures })
    setImporting(false)
  }

  // ── Derived ───────────────────────────────────────────────────────────────

  const validCount = validations.filter((v) => v.errors.length === 0).length
  const errorCount = validations.filter((v) => v.errors.length > 0).length
  const canImport = rows.length > 0 && (skipErrors ? validCount > 0 : errorCount === 0)
  const progressPct = progressTotal > 0 ? Math.round((progressDone / progressTotal) * 100) : 0

  // ─────────────────────────────────────────────────────────────────────────

  return (
    <Modal
      open={open}
      title="Bulk import rate cards"
      description="Upload a CSV file to create multiple rate cards in one go."
      onClose={handleClose}
      maxWidth="max-w-3xl"
    >
      <StepIndicator current={step} />

      {/* ── Step 1: Upload & validate ── */}
      {step === 1 && (
        <div className="space-y-5">
          {/* Template download */}
          <div className="flex items-center justify-between rounded-[20px] border border-app bg-subtle px-4 py-3">
            <div>
              <p className="text-sm font-medium text-app">Download CSV template</p>
              <p className="mt-0.5 text-xs text-muted">Includes 5 example rows and valid-value comments.</p>
            </div>
            <button
              type="button"
              className="flex items-center gap-1.5 rounded-xl border border-app bg-surface px-3 py-1.5 text-xs font-medium text-app hover:border-teal-400/50 transition-colors"
              onClick={downloadTemplate}
            >
              <Download className="h-3.5 w-3.5" />
              Download template
            </button>
          </div>

          {/* Drop zone */}
          <div
            className={`relative flex cursor-pointer flex-col items-center justify-center gap-3 rounded-[20px] border-2 border-dashed px-6 py-10 transition-colors
              ${isDragging ? 'border-teal-400 bg-teal-500/10' : 'border-white/20 hover:border-teal-400/50 hover:bg-teal-500/5'}`}
            onDragOver={(e) => { e.preventDefault(); setIsDragging(true) }}
            onDragLeave={() => setIsDragging(false)}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
          >
            <Upload className="h-8 w-8 text-muted" />
            <div className="text-center">
              <p className="text-sm font-medium text-app">Drag & drop your CSV here, or click to browse</p>
              <p className="mt-1 text-xs text-muted">Accepts .csv only</p>
            </div>
            <input
              ref={fileInputRef}
              type="file"
              accept=".csv"
              className="sr-only"
              onChange={(e) => processFile(e.target.files?.[0])}
            />
          </div>

          {/* Parse error */}
          {parseError ? (
            <div className="rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
              {parseError}
            </div>
          ) : null}

          {/* Summary bar */}
          {rows.length > 0 && !parseError ? (
            <div className="flex flex-wrap items-center gap-4 rounded-2xl border border-app bg-subtle px-4 py-3">
              <span className="text-sm font-medium text-app">{rows.length} rows found</span>
              <span className="text-sm text-emerald-400">{validCount} valid</span>
              {errorCount > 0 ? <span className="text-sm text-rose-300">{errorCount} with errors</span> : null}
            </div>
          ) : null}

          {/* Preview table */}
          {rows.length > 0 && !parseError ? (
            <PreviewTable rows={rows} validations={validations} />
          ) : null}

          {/* Validation errors */}
          {errorCount > 0 ? (
            <div className="rounded-2xl border border-rose-400/20 bg-rose-500/5 px-4 py-3 space-y-2">
              <p className="text-sm font-semibold text-rose-300">Validation errors</p>
              {validations
                .filter((v) => v.errors.length > 0)
                .slice(0, 15)
                .map((v) => (
                  <div key={v.rowIndex} className="text-xs text-rose-200">
                    Row {v.rowIndex + 1}: {v.errors.join(' · ')}
                  </div>
                ))}
              {errorCount > 15 ? <p className="text-xs text-muted">…and {errorCount - 15} more</p> : null}
            </div>
          ) : null}

          {/* Skip errors checkbox */}
          {errorCount > 0 ? (
            <label className="flex cursor-pointer items-center gap-3">
              <input
                type="checkbox"
                checked={skipErrors}
                onChange={(e) => setSkipErrors(e.target.checked)}
                className="h-4 w-4 rounded accent-teal-500"
              />
              <span className="text-sm text-muted">
                Skip rows with errors and import {validCount} valid row{validCount !== 1 ? 's' : ''} only
              </span>
            </label>
          ) : null}

          <div className="flex justify-end pt-2">
            <button
              type="button"
              className="button-primary"
              disabled={!canImport}
              onClick={() => { setStep(2); void runImport() }}
            >
              Start import →
            </button>
          </div>
        </div>
      )}

      {/* ── Step 2: Progress & results ── */}
      {step === 2 && (
        <div className="space-y-6">
          {/* Progress bar */}
          <div className="space-y-2">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted">
                {importing
                  ? `Creating rate card ${progressDone + 1} of ${progressTotal}…`
                  : result
                  ? 'Done!'
                  : 'Preparing…'}
              </span>
              <span className="font-medium text-app">{progressPct}%</span>
            </div>
            <div className="h-2.5 w-full overflow-hidden rounded-full bg-white/10">
              <div
                className="h-full rounded-full bg-emerald-500 transition-all duration-300 ease-out"
                style={{ width: `${progressPct}%` }}
              />
            </div>
          </div>

          {/* Final summary */}
          {result && !importing ? (
            <div className="space-y-4">
              <div className="rounded-2xl border border-app bg-subtle p-5">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Rate cards</p>
                <p className="mt-2 text-3xl font-semibold text-emerald-400">{result.created}</p>
                <div className="mt-1 flex items-center gap-1.5 text-sm">
                  <span className="text-muted">created</span>
                  {result.failed > 0 ? (
                    <span className="text-rose-300">· {result.failed} failed</span>
                  ) : null}
                </div>
              </div>

              {result.failures.length > 0 ? (
                <div className="rounded-2xl border border-rose-400/20 bg-rose-500/5 px-4 py-3 space-y-1.5">
                  <p className="text-sm font-semibold text-rose-300">Failed rows</p>
                  {result.failures.map((msg, i) => (
                    <p key={i} className="text-xs text-rose-200">{msg}</p>
                  ))}
                </div>
              ) : null}

              <div className="flex justify-end pt-2">
                <button
                  type="button"
                  className="button-primary"
                  onClick={() => { reset(); onSuccess() }}
                >
                  ✅ Done — view rate cards
                </button>
              </div>
            </div>
          ) : null}
        </div>
      )}
    </Modal>
  )
}
