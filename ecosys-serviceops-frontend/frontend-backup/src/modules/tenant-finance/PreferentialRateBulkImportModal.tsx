import { useCallback, useMemo, useRef, useState } from 'react'
import Papa from 'papaparse'
import { CheckCircle, Download, Upload, XCircle } from 'lucide-react'
import { Modal } from '../../components/ui/Modal'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import type { ClientRecord, UpsertPreferentialRateInput } from '../../types/api'

const VALID_ITEM_TYPES = ['labour', 'material', 'callout', 'transport', 'service', 'other'] as const
const VALID_STATUSES = ['Active', 'Inactive', 'PendingApproval'] as const
const CSV_HEADERS = [
  'client_name*',
  'rate_name*',
  'rate_type',
  'item_type*',
  'unit*',
  'default_rate*',
  'preferential_rate*',
  'currency',
  'effective_from*',
  'effective_to',
  'status',
  'requires_approval',
] as const

const CSV_COMMENT = [
  '# Valid values:',
  '# item_type: labour|material|callout|transport|service|other',
  '# status: Active|Inactive|PendingApproval',
  '# requires_approval: TRUE|FALSE',
  '# effective_from/effective_to: YYYY-MM-DD',
]

type CsvRow = Record<string, string>

type ValidationResult = {
  row: CsvRow
  rowIndex: number
  errors: string[]
  parsed?: UpsertPreferentialRateInput
}

type ImportResult = {
  created: number
  failed: number
  failures: string[]
}

const EXAMPLE_ROWS: CsvRow[] = [
  {
    'client_name*': 'Acme Facilities',
    'rate_name*': 'Preferred Generator Labour',
    rate_type: 'Preferential',
    'item_type*': 'labour',
    'unit*': 'hour',
    'default_rate*': '3500',
    'preferential_rate*': '3000',
    currency: 'KES',
    'effective_from*': '2026-06-01',
    effective_to: '2026-12-31',
    status: 'Active',
    requires_approval: 'FALSE',
  },
  {
    'client_name*': 'Acme Facilities',
    'rate_name*': 'Preferred Parts Markup',
    rate_type: 'Preferential',
    'item_type*': 'material',
    'unit*': 'unit',
    'default_rate*': '1500',
    'preferential_rate*': '1400',
    currency: 'KES',
    'effective_from*': '2026-06-01',
    effective_to: '',
    status: 'PendingApproval',
    requires_approval: 'TRUE',
  },
]

function downloadTemplate() {
  const header = CSV_HEADERS.join(',')
  const rows = EXAMPLE_ROWS.map((row) => CSV_HEADERS.map((key) => `"${row[key] ?? ''}"`).join(','))
  const csv = [...CSV_COMMENT, header, ...rows].join('\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = 'preferential_rates_import_template.csv'
  anchor.click()
  URL.revokeObjectURL(url)
}

function normalizeDate(value?: string | null) {
  if (!value?.trim()) return null
  const candidate = value.trim()
  if (!/^\d{4}-\d{2}-\d{2}$/.test(candidate)) return null
  return new Date(`${candidate}T00:00:00.000Z`).toISOString()
}

function normalizeBoolean(value?: string | null) {
  const normalized = (value || '').trim().toUpperCase()
  if (!normalized) return false
  if (normalized === 'TRUE') return true
  if (normalized === 'FALSE') return false
  return null
}

function normalizeStatus(value?: string | null) {
  const candidate = (value || '').trim()
  if (!candidate) return 'Active'
  return VALID_STATUSES.find((status) => status.toLowerCase() === candidate.toLowerCase()) ?? null
}

function normalizeItemType(value?: string | null) {
  const candidate = (value || '').trim().toLowerCase()
  return VALID_ITEM_TYPES.find((item) => item === candidate) ?? null
}

function batchClientLookup(clients: ClientRecord[]) {
  return new Map(
    clients.map((client) => [client.clientName.trim().toLowerCase(), client.id] as const),
  )
}

function validateRows(rows: CsvRow[], clients: ClientRecord[]) {
  const clientLookup = batchClientLookup(clients)

  return rows.map((row, index) => {
    const errors: string[] = []
    const clientName = row['client_name*']?.trim()
    const rateName = row['rate_name*']?.trim()
    const rateType = row.rate_type?.trim() || 'Preferential'
    const itemType = normalizeItemType(row['item_type*'])
    const unit = row['unit*']?.trim()
    const defaultRateRaw = row['default_rate*']?.trim()
    const preferentialRateRaw = row['preferential_rate*']?.trim()
    const currency = (row.currency?.trim() || 'KES').toUpperCase()
    const effectiveFrom = normalizeDate(row['effective_from*'])
    const effectiveTo = row.effective_to?.trim() ? normalizeDate(row.effective_to) : null
    const status = normalizeStatus(row.status)
    const requiresApproval = normalizeBoolean(row.requires_approval)

    if (!clientName) errors.push('client_name is required.')
    if (clientName && !clientLookup.get(clientName.toLowerCase())) errors.push(`Client "${clientName}" was not found.`)
    if (!rateName) errors.push('rate_name is required.')
    if (!itemType) errors.push(`Invalid item_type "${row['item_type*']}". Use: ${VALID_ITEM_TYPES.join('|')}.`)
    if (!unit) errors.push('unit is required.')

    const defaultRate = Number(defaultRateRaw)
    if (!defaultRateRaw || Number.isNaN(defaultRate) || defaultRate < 0) {
      errors.push('default_rate must be a valid number of 0 or more.')
    }

    const preferentialRate = Number(preferentialRateRaw)
    if (!preferentialRateRaw || Number.isNaN(preferentialRate) || preferentialRate < 0) {
      errors.push('preferential_rate must be a valid number of 0 or more.')
    }

    if (!effectiveFrom) errors.push('effective_from must be a valid YYYY-MM-DD date.')
    if (row.effective_to?.trim() && !effectiveTo) errors.push('effective_to must be a valid YYYY-MM-DD date.')
    if (!status) errors.push(`Invalid status "${row.status}". Use: ${VALID_STATUSES.join('|')}.`)
    if (requiresApproval === null) errors.push('requires_approval must be TRUE or FALSE.')

    const clientId = clientName ? clientLookup.get(clientName.toLowerCase()) : null
    const parsed =
      errors.length === 0 && clientId && itemType && unit && effectiveFrom && status !== null && requiresApproval !== null
        ? {
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
          } as UpsertPreferentialRateInput
        : undefined

    return { row, rowIndex: index, errors, parsed }
  })
}

async function batchRun<T, R>(
  items: T[],
  batchSize: number,
  fn: (item: T) => Promise<R>,
  onProgress: (done: number, total: number) => void,
) {
  const results: PromiseSettledResult<R>[] = []
  for (let index = 0; index < items.length; index += batchSize) {
    const batch = items.slice(index, index + batchSize)
    const batchResults = await Promise.allSettled(batch.map(fn))
    results.push(...batchResults)
    onProgress(Math.min(index + batch.length, items.length), items.length)
  }
  return results
}

function PreviewTable({ rows, validations }: { rows: CsvRow[]; validations: ValidationResult[] }) {
  const displayRows = rows.slice(0, 20)

  return (
    <div className="space-y-1.5">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Preview</p>
      <div className="overflow-hidden rounded-[20px] border border-app">
        <div className="max-h-56 overflow-x-auto">
          <table className="min-w-full text-xs">
            <thead className="sticky top-0 z-10 bg-[var(--app-surface-strong)]">
              <tr>
                {CSV_HEADERS.map((header) => (
                  <th key={header} className="whitespace-nowrap px-3 py-2 text-left font-semibold uppercase tracking-wider text-muted">
                    {header}
                  </th>
                ))}
                <th className="px-3 py-2 text-left font-semibold uppercase tracking-wider text-muted">Status</th>
              </tr>
            </thead>
            <tbody>
              {displayRows.map((row, index) => {
                const invalid = (validations[index]?.errors.length ?? 0) > 0
                return (
                  <tr key={index} className={`border-t border-app ${invalid ? 'bg-rose-500/5' : ''}`}>
                    {CSV_HEADERS.map((header) => (
                      <td key={header} className="whitespace-nowrap px-3 py-2 text-app">
                        {row[header] || '-'}
                      </td>
                    ))}
                    <td className="px-3 py-2">
                      {invalid ? <XCircle className="h-4 w-4 text-rose-400" /> : <CheckCircle className="h-4 w-4 text-emerald-400" />}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        {rows.length > 20 ? <p className="border-t border-app px-3 py-2 text-xs text-muted">Showing 20 of {rows.length} rows</p> : null}
      </div>
    </div>
  )
}

export function PreferentialRateBulkImportModal({
  open,
  onClose,
  onSuccess,
  clients,
}: {
  open: boolean
  onClose: () => void
  onSuccess: () => void | Promise<void>
  clients: ClientRecord[]
}) {
  const [rows, setRows] = useState<CsvRow[]>([])
  const [validations, setValidations] = useState<ValidationResult[]>([])
  const [parseError, setParseError] = useState('')
  const [skipErrors, setSkipErrors] = useState(true)
  const [importing, setImporting] = useState(false)
  const [progressDone, setProgressDone] = useState(0)
  const [progressTotal, setProgressTotal] = useState(0)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [isDragging, setIsDragging] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const validRows = useMemo(() => validations.filter((row) => row.errors.length === 0), [validations])
  const invalidRows = validations.length - validRows.length

  const reset = useCallback(() => {
    setRows([])
    setValidations([])
    setParseError('')
    setSkipErrors(true)
    setImporting(false)
    setProgressDone(0)
    setProgressTotal(0)
    setResult(null)
    setIsDragging(false)
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
  }, [])

  function handleClose() {
    if (importing) return
    reset()
    onClose()
  }

  const processFile = useCallback((file: File | null | undefined) => {
    if (!file) return
    if (!file.name.toLowerCase().endsWith('.csv')) {
      setParseError('Only .csv files are supported for preferential rate import.')
      return
    }

    setParseError('')
    setRows([])
    setValidations([])
    setResult(null)

    Papa.parse<CsvRow>(file, {
      header: true,
      skipEmptyLines: true,
      comments: '#',
      complete: (results) => {
        const parsedRows = results.data as CsvRow[]
        setRows(parsedRows)
        setValidations(validateRows(parsedRows, clients))
      },
      error: (error) => setParseError(error.message),
    })
  }, [clients])

  async function runImport() {
    const candidates = skipErrors ? validRows : validations
    const toImport = candidates.filter((row): row is ValidationResult & { parsed: UpsertPreferentialRateInput } => Boolean(row.parsed))
    if (toImport.length === 0) return

    setImporting(true)
    setProgressDone(0)
    setProgressTotal(toImport.length)
    setResult(null)

    const responses = await batchRun(
      toImport,
      10,
      (row) => tenantFinanceService.createPreferentialRate(row.parsed),
      setProgressDone,
    )

    const failures: string[] = []
    let created = 0

    responses.forEach((response, index) => {
      if (response.status === 'fulfilled') {
        created += 1
      } else {
        failures.push(`Row ${toImport[index].rowIndex + 2}: ${response.reason instanceof Error ? response.reason.message : 'Import failed.'}`)
      }
    })

    setResult({ created, failed: failures.length, failures })
    setImporting(false)

    if (created > 0) {
      await onSuccess()
    }
  }

  return (
    <Modal
      open={open}
      title="Import preferential rates"
      description="Upload a CSV of client-specific billing overrides, validate the rows, then apply the import."
      onClose={handleClose}
      maxWidth="max-w-5xl"
    >
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm font-medium text-app">CSV template</p>
            <p className="text-sm text-muted">Match clients by exact name and keep dates in YYYY-MM-DD format.</p>
          </div>
          <button type="button" className="button-secondary" onClick={downloadTemplate}>
            <Download className="h-4 w-4" />
            Download template
          </button>
        </div>

        <div
          className={`rounded-[20px] border border-dashed px-5 py-6 text-center transition ${
            isDragging ? 'border-[var(--app-accent-strong)] bg-[var(--app-subtle-hover)]' : 'border-app bg-[var(--app-surface-strong)]'
          }`}
          onDragOver={(event) => {
            event.preventDefault()
            setIsDragging(true)
          }}
          onDragLeave={() => setIsDragging(false)}
          onDrop={(event) => {
            event.preventDefault()
            setIsDragging(false)
            processFile(event.dataTransfer.files[0])
          }}
        >
          <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--app-subtle)] text-app">
            <Upload className="h-5 w-5" />
          </div>
          <p className="mt-3 text-sm font-medium text-app">Drop a preferential-rates CSV here or select a file.</p>
          <p className="mt-1 text-sm text-muted">Only CSV files are supported for this import.</p>
          <div className="mt-4 flex flex-wrap justify-center gap-2">
            <label className="button-secondary cursor-pointer">
              Choose file
              <input
                ref={fileInputRef}
                type="file"
                accept=".csv"
                className="hidden"
                onChange={(event) => processFile(event.target.files?.[0])}
              />
            </label>
            {rows.length > 0 ? <button type="button" className="button-secondary" onClick={reset}>Clear</button> : null}
          </div>
        </div>

        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
          <div>
            <p className="text-sm font-medium text-app">Skip invalid rows</p>
            <p className="text-xs text-muted">Import valid rows and report any rows that need correction.</p>
          </div>
          <input type="checkbox" checked={skipErrors} onChange={(event) => setSkipErrors(event.target.checked)} />
        </label>

        {parseError ? <div className="rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{parseError}</div> : null}

        {validations.length > 0 ? (
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="panel-subtle rounded-2xl p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Rows</p>
              <p className="mt-1 text-2xl font-semibold text-app">{validations.length}</p>
            </div>
            <div className="panel-subtle rounded-2xl p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Valid</p>
              <p className="mt-1 text-2xl font-semibold text-emerald-300">{validRows.length}</p>
            </div>
            <div className="panel-subtle rounded-2xl p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Needs review</p>
              <p className="mt-1 text-2xl font-semibold text-amber-300">{invalidRows}</p>
            </div>
          </div>
        ) : null}

        {rows.length > 0 ? <PreviewTable rows={rows} validations={validations} /> : null}

        {validations.some((row) => row.errors.length > 0) ? (
          <div className="overflow-hidden rounded-[20px] border border-app">
            <div className="max-h-44 overflow-auto">
              <table className="min-w-full text-sm">
                <thead className="table-head">
                  <tr>
                    <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-secondary">Row</th>
                    <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-secondary">Errors</th>
                  </tr>
                </thead>
                <tbody className="divide-app">
                  {validations.filter((row) => row.errors.length > 0).map((row) => (
                    <tr key={row.rowIndex} className="table-row">
                      <td className="px-4 py-3 text-app">{row.rowIndex + 2}</td>
                      <td className="px-4 py-3 text-rose-200">{row.errors.join(' ')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}

        {progressTotal > 0 ? (
          <div className="panel-subtle rounded-2xl px-4 py-3 text-sm text-muted">
            Import progress: {progressDone} of {progressTotal}
          </div>
        ) : null}

        {result ? (
          <div className="space-y-3">
            <div className="panel-subtle rounded-2xl px-4 py-3 text-sm text-muted">
              Created {result.created} preferential rates. Failed {result.failed}.
            </div>
            {result.failures.length > 0 ? (
              <div className="overflow-hidden rounded-[20px] border border-app">
                <div className="max-h-40 overflow-auto px-4 py-3 text-sm text-rose-200">
                  {result.failures.map((failure) => (
                    <p key={failure}>{failure}</p>
                  ))}
                </div>
              </div>
            ) : null}
          </div>
        ) : null}

        <div className="flex flex-wrap justify-end gap-2">
          <button type="button" className="button-secondary" onClick={handleClose} disabled={importing}>Close</button>
          <button type="button" className="button-primary" onClick={() => void runImport()} disabled={importing || validRows.length === 0}>
            {importing ? 'Importing...' : 'Import rates'}
          </button>
        </div>
      </div>
    </Modal>
  )
}
