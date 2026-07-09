import { useCallback, useRef, useState } from 'react'
import Papa from 'papaparse'
import * as XLSX from 'xlsx'
import { CheckCircle, Download, FileSpreadsheet, Upload, Users, XCircle } from 'lucide-react'
import { Modal } from '../../components/ui/Modal'
import { clientService } from '../../services/clientService'
import { siteService } from '../../services/siteService'
import { assetService } from '../../services/assetService'
import type { UpsertClientInput, UpsertSiteInput, UpsertAssetInput } from '../../types/api'

// ─── Local Types ────────────────────────────────────────────────────────────

type ImportType = 'clients' | 'clients-sites' | 'full'
type ImportRow = { [key: string]: string }
type ValidationResult = { row: ImportRow; rowIndex: number; errors: string[] }
type ImportResult = { created: number; failed: number; errors: string[] }

type ParsedData = {
  clients: ImportRow[]
  sites: ImportRow[]
  assets: ImportRow[]
}

type ImportSummary = {
  clients: ImportResult
  sites: ImportResult
  assets: ImportResult
}

// ─── Constants ───────────────────────────────────────────────────────────────

const VALID_STATUSES = ['active', 'inactive', 'suspended']

const CLIENT_HEADERS = [
  'client_name*', 'client_code', 'contact_name', 'contact_email',
  'contact_phone', 'address', 'city', 'status',
]
const SITE_HEADERS = [
  'client_name*', 'site_name*', 'site_code', 'address', 'city',
  'contact_name', 'contact_email', 'contact_phone',
]
const ASSET_HEADERS = [
  'client_name*', 'site_name*', 'asset_name*', 'asset_code',
  'category', 'make', 'model', 'serial_number', 'install_date', 'status',
]

const CLIENT_EXAMPLES: ImportRow[] = [
  {
    'client_name*': 'Acme Hospitals Ltd',
    client_code: 'ACM001',
    contact_name: 'Jane Mwangi',
    contact_email: 'jane@acme.co.ke',
    contact_phone: '+254712345678',
    address: '12 Mombasa Rd',
    city: 'Nairobi',
    status: 'active',
  },
  {
    'client_name*': 'Sunrise Medical Centre',
    client_code: 'SUN002',
    contact_name: 'Peter Odhiambo',
    contact_email: 'peter@sunrise.co.ke',
    contact_phone: '+254722334455',
    address: '5 Kenyatta Ave',
    city: 'Kisumu',
    status: 'active',
  },
]

const SITE_EXAMPLES: ImportRow[] = [
  {
    'client_name*': 'Acme Hospitals Ltd',
    'site_name*': 'Main Branch',
    site_code: 'ACM-MB',
    address: '12 Mombasa Rd',
    city: 'Nairobi',
    contact_name: 'Jane Mwangi',
    contact_email: 'jane@acme.co.ke',
    contact_phone: '+254712345678',
  },
  {
    'client_name*': 'Acme Hospitals Ltd',
    'site_name*': 'Westlands Annex',
    site_code: 'ACM-WA',
    address: '18 Westlands Rd',
    city: 'Nairobi',
    contact_name: 'Tom Kariuki',
    contact_email: 'tom@acme.co.ke',
    contact_phone: '+254711223344',
  },
]

const ASSET_EXAMPLES: ImportRow[] = [
  {
    'client_name*': 'Acme Hospitals Ltd',
    'site_name*': 'Main Branch',
    'asset_name*': 'Autoclave Unit 1',
    asset_code: 'ACM-AC-001',
    category: 'Sterilisation',
    make: 'Tuttnauer',
    model: '3870EA',
    serial_number: 'TUT2023001',
    install_date: '2023-01-15',
    status: 'active',
  },
  {
    'client_name*': 'Acme Hospitals Ltd',
    'site_name*': 'Main Branch',
    'asset_name*': 'Ultrasound Machine A',
    asset_code: 'ACM-US-001',
    category: 'Imaging',
    make: 'GE Healthcare',
    model: 'Vivid S60',
    serial_number: 'GE20230045',
    install_date: '2023-03-01',
    status: 'active',
  },
]

// ─── Template Generators ─────────────────────────────────────────────────────

function downloadCsvTemplate() {
  const headers = CLIENT_HEADERS.join(',')
  const rows = CLIENT_EXAMPLES.map((row) =>
    CLIENT_HEADERS.map((h) => `"${row[h] ?? ''}"`).join(','),
  )
  const csv = [headers, ...rows].join('\n')
  triggerDownload(
    new Blob([csv], { type: 'text/csv' }),
    'clients_import_template.csv',
  )
}

function downloadExcelTemplate(importType: ImportType) {
  const wb = XLSX.utils.book_new()

  // Clients sheet
  const clientRows = [CLIENT_HEADERS, ...CLIENT_EXAMPLES.map((r) => CLIENT_HEADERS.map((h) => r[h] ?? ''))]
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(clientRows), 'Clients')

  if (importType === 'clients-sites' || importType === 'full') {
    const siteRows = [SITE_HEADERS, ...SITE_EXAMPLES.map((r) => SITE_HEADERS.map((h) => r[h] ?? ''))]
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(siteRows), 'Sites')
  }

  if (importType === 'full') {
    const assetRows = [ASSET_HEADERS, ...ASSET_EXAMPLES.map((r) => ASSET_HEADERS.map((h) => r[h] ?? ''))]
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(assetRows), 'Assets')
  }

  XLSX.writeFile(wb, `ecosys_import_template_${importType}.xlsx`)
}

function triggerDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

// ─── Validation ───────────────────────────────────────────────────────────────

function validateClientRows(rows: ImportRow[]): ValidationResult[] {
  return rows.map((row, i) => {
    const errors: string[] = []
    if (!row['client_name*']?.trim()) errors.push('client_name is required.')
    const status = row['status']?.toLowerCase()
    if (status && !VALID_STATUSES.includes(status)) {
      errors.push(`Invalid status "${row['status']}". Use: active, inactive, suspended.`)
    }
    return { row, rowIndex: i, errors }
  })
}

function validateSiteRows(rows: ImportRow[], clientNames: Set<string>): ValidationResult[] {
  return rows.map((row, i) => {
    const errors: string[] = []
    if (!row['client_name*']?.trim()) errors.push('client_name is required.')
    else if (!clientNames.has(row['client_name*'].trim())) {
      errors.push(`client_name "${row['client_name*']}" not found in Clients sheet.`)
    }
    if (!row['site_name*']?.trim()) errors.push('site_name is required.')
    return { row, rowIndex: i, errors }
  })
}

function validateAssetRows(rows: ImportRow[], clientNames: Set<string>, siteKeys: Set<string>): ValidationResult[] {
  return rows.map((row, i) => {
    const errors: string[] = []
    if (!row['client_name*']?.trim()) errors.push('client_name is required.')
    else if (!clientNames.has(row['client_name*'].trim())) {
      errors.push(`client_name "${row['client_name*']}" not found in Clients sheet.`)
    }
    if (!row['site_name*']?.trim()) errors.push('site_name is required.')
    else {
      const key = `${row['client_name*']?.trim()}||${row['site_name*']?.trim()}`
      if (!siteKeys.has(key)) {
        errors.push(`site_name "${row['site_name*']}" not found in Sites sheet for this client.`)
      }
    }
    if (!row['asset_name*']?.trim()) errors.push('asset_name is required.')
    const installDate = row['install_date']
    if (installDate?.trim() && Number.isNaN(Date.parse(installDate))) {
      errors.push(`Invalid install_date "${installDate}". Use YYYY-MM-DD.`)
    }
    const status = row['status']?.toLowerCase()
    if (status && !VALID_STATUSES.includes(status)) {
      errors.push(`Invalid status "${row['status']}". Use: active, inactive, suspended.`)
    }
    return { row, rowIndex: i, errors }
  })
}

// ─── File Parsing ─────────────────────────────────────────────────────────────

function parseCsvFile(file: File): Promise<ImportRow[]> {
  return new Promise((resolve, reject) => {
    Papa.parse<ImportRow>(file, {
      header: true,
      skipEmptyLines: true,
      complete: (results) => resolve(results.data),
      error: (err) => reject(new Error(err.message)),
    })
  })
}

function parseExcelFile(file: File, sheets: string[]): Promise<Record<string, ImportRow[]>> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = (e) => {
      try {
        const data = e.target?.result
        if (!data) { reject(new Error('Failed to read file.')); return }
        const wb = XLSX.read(data, { type: 'array' })
        const result: Record<string, ImportRow[]> = {}
        for (const sheet of sheets) {
          const ws = wb.Sheets[sheet]
          result[sheet] = ws ? (XLSX.utils.sheet_to_json<ImportRow>(ws, { defval: '' }) as ImportRow[]) : []
        }
        resolve(result)
      } catch (err) {
        reject(err instanceof Error ? err : new Error('Failed to parse Excel file.'))
      }
    }
    reader.onerror = () => reject(new Error('File read error.'))
    reader.readAsArrayBuffer(file)
  })
}

// ─── Import Batching ──────────────────────────────────────────────────────────

async function batchRun<T, R>(
  items: T[],
  batchSize: number,
  fn: (item: T) => Promise<R>,
  onProgress: (done: number, total: number, label: string) => void,
  label: string,
): Promise<PromiseSettledResult<R>[]> {
  const results: PromiseSettledResult<R>[] = []
  for (let i = 0; i < items.length; i += batchSize) {
    const batch = items.slice(i, i + batchSize)
    const batchResults = await Promise.allSettled(batch.map(fn))
    results.push(...batchResults)
    onProgress(Math.min(i + batchSize, items.length), items.length, label)
  }
  return results
}

// ─── Step Indicator ───────────────────────────────────────────────────────────

function StepIndicator({ current }: { current: 1 | 2 | 3 }) {
  const steps = [
    { n: 1 as const, label: 'Choose type' },
    { n: 2 as const, label: 'Upload & validate' },
    { n: 3 as const, label: 'Import' },
  ]
  return (
    <div className="flex items-center gap-0 mb-6">
      {steps.map((step, idx) => (
        <div key={step.n} className="flex items-center flex-1 last:flex-none">
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
            <span className={`text-sm hidden sm:inline ${step.n === current ? 'font-semibold text-app' : 'text-muted'}`}>
              {step.label}
            </span>
          </div>
          {idx < steps.length - 1 && (
            <div className={`flex-1 mx-3 h-px ${step.n < current ? 'bg-emerald-500/60' : 'bg-white/10'}`} />
          )}
        </div>
      ))}
    </div>
  )
}

// ─── Main Component ───────────────────────────────────────────────────────────

export function ClientBulkImportModal({
  open,
  onClose,
  onSuccess,
}: {
  open: boolean
  onClose: () => void
  onSuccess: () => void
}) {
  const [step, setStep] = useState<1 | 2 | 3>(1)
  const [importType, setImportType] = useState<ImportType>('full')

  // Step 2 state
  const [parsed, setParsed] = useState<ParsedData>({ clients: [], sites: [], assets: [] })
  const [clientValidation, setClientValidation] = useState<ValidationResult[]>([])
  const [siteValidation, setSiteValidation] = useState<ValidationResult[]>([])
  const [assetValidation, setAssetValidation] = useState<ValidationResult[]>([])
  const [parseError, setParseError] = useState('')
  const [skipErrors, setSkipErrors] = useState(false)
  const [isDragging, setIsDragging] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  // Step 3 state
  const [progressLabel, setProgressLabel] = useState('')
  const [progressValue, setProgressValue] = useState(0)
  const [importing, setImporting] = useState(false)
  const [summary, setSummary] = useState<ImportSummary | null>(null)

  function resetAll() {
    setStep(1)
    setImportType('full')
    setParsed({ clients: [], sites: [], assets: [] })
    setClientValidation([])
    setSiteValidation([])
    setAssetValidation([])
    setParseError('')
    setSkipErrors(false)
    setIsDragging(false)
    setProgressLabel('')
    setProgressValue(0)
    setImporting(false)
    setSummary(null)
  }

  function handleClose() {
    if (importing) return
    resetAll()
    onClose()
  }

  // ── Step 2: file handling ─────────────────────────────────────────────────

  async function processFile(file: File) {
    setParseError('')
    setClientValidation([])
    setSiteValidation([])
    setAssetValidation([])

    try {
      const isCsv = file.name.toLowerCase().endsWith('.csv')
      const isXlsx = file.name.toLowerCase().endsWith('.xlsx') || file.name.toLowerCase().endsWith('.xls')

      if (!isCsv && !isXlsx) {
        setParseError('Unsupported file type. Please upload a .csv or .xlsx file.')
        return
      }

      let clientRows: ImportRow[] = []
      let siteRows: ImportRow[] = []
      let assetRows: ImportRow[] = []

      if (isCsv) {
        clientRows = await parseCsvFile(file)
      } else {
        const sheetNames = ['Clients']
        if (importType === 'clients-sites' || importType === 'full') sheetNames.push('Sites')
        if (importType === 'full') sheetNames.push('Assets')
        const sheets = await parseExcelFile(file, sheetNames)
        clientRows = sheets['Clients'] ?? []
        siteRows = sheets['Sites'] ?? []
        assetRows = sheets['Assets'] ?? []
      }

      setParsed({ clients: clientRows, sites: siteRows, assets: assetRows })

      const clientResults = validateClientRows(clientRows)
      const clientNameSet = new Set(
        clientRows
          .filter((_, i) => clientResults[i]?.errors.length === 0)
          .map((r) => r['client_name*']?.trim())
          .filter(Boolean),
      )

      const siteResults = (importType !== 'clients') ? validateSiteRows(siteRows, clientNameSet) : []
      const siteKeySet = new Set(
        siteRows
          .filter((_, i) => siteResults[i]?.errors.length === 0)
          .map((r) => `${r['client_name*']?.trim()}||${r['site_name*']?.trim()}`)
          .filter((k) => k !== '||'),
      )

      const assetResults = (importType === 'full') ? validateAssetRows(assetRows, clientNameSet, siteKeySet) : []

      setClientValidation(clientResults)
      setSiteValidation(siteResults)
      setAssetValidation(assetResults)
    } catch (err) {
      setParseError(err instanceof Error ? err.message : 'Failed to parse file.')
    }
  }

  const handleFileChange = useCallback(
    (file: File | null | undefined) => {
      if (!file) return
      void processFile(file)
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [importType],
  )

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setIsDragging(false)
    handleFileChange(e.dataTransfer.files[0])
  }

  // ── Step 3: import ────────────────────────────────────────────────────────

  async function runImport() {
    setImporting(true)
    setProgressValue(0)

    const validClients = clientValidation.filter((v) => v.errors.length === 0)
    const validSites = siteValidation.filter((v) => v.errors.length === 0)
    const validAssets = assetValidation.filter((v) => v.errors.length === 0)

    const clientsToImport = skipErrors
      ? validClients.map((v) => v.row)
      : clientValidation.every((v) => v.errors.length === 0)
      ? clientValidation.map((v) => v.row)
      : []

    const sitesToImport = skipErrors
      ? validSites.map((v) => v.row)
      : siteValidation.every((v) => v.errors.length === 0)
      ? siteValidation.map((v) => v.row)
      : []

    const assetsToImport = skipErrors
      ? validAssets.map((v) => v.row)
      : assetValidation.every((v) => v.errors.length === 0)
      ? assetValidation.map((v) => v.row)
      : []

    const totalSteps = (clientsToImport.length > 0 ? 1 : 0) +
      (sitesToImport.length > 0 ? 1 : 0) +
      (assetsToImport.length > 0 ? 1 : 0) || 1
    let stepsCompleted = 0

    const updateProgress = (_done: number, _total: number, label: string) => {
      setProgressLabel(label)
      setProgressValue(Math.round(((stepsCompleted + (_done / _total)) / totalSteps) * 100))
    }

    // 1. Create clients
    const clientResult: ImportResult = { created: 0, failed: 0, errors: [] }
    const clientNameToId = new Map<string, string>()

    if (clientsToImport.length > 0) {
      setProgressLabel('Creating clients…')
      const results = await batchRun(
        clientsToImport,
        10,
        (row) => {
          const input: UpsertClientInput = {
            clientName: row['client_name*'].trim(),
            contactPerson: row['contact_name']?.trim() || null,
            email: row['contact_email']?.trim() || null,
            phone: row['contact_phone']?.trim() || null,
            location: [row['address'], row['city']].filter(Boolean).join(', ') || null,
          }
          return clientService.create(input)
        },
        (done, total, label) => updateProgress(done, total, label),
        'Creating clients…',
      )
      results.forEach((r, i) => {
        if (r.status === 'fulfilled') {
          clientResult.created++
          clientNameToId.set(clientsToImport[i]['client_name*'].trim(), r.value.id)
        } else {
          clientResult.failed++
          clientResult.errors.push(
            `Row ${clientValidation[i]?.rowIndex + 1}: ${r.reason instanceof Error ? r.reason.message : 'Failed'}`,
          )
        }
      })
      stepsCompleted++
    }

    // 2. Create sites
    const siteResult: ImportResult = { created: 0, failed: 0, errors: [] }
    const siteNameToId = new Map<string, string>()

    if (sitesToImport.length > 0) {
      setProgressLabel('Creating sites…')
      const results = await batchRun(
        sitesToImport,
        10,
        (row) => {
          const clientId = clientNameToId.get(row['client_name*'].trim())
          if (!clientId) return Promise.reject(new Error(`No ID for client "${row['client_name*']}"`))
          const input: UpsertSiteInput = {
            siteName: row['site_name*'].trim(),
            siteType: 'Branch',
            streetAddress: row['address']?.trim() || null,
            townCity: row['city']?.trim() || null,
            contactPerson: row['contact_name']?.trim() || null,
            contactEmail: row['contact_email']?.trim() || null,
            contactPhone: row['contact_phone']?.trim() || null,
          }
          return siteService.create(clientId, input)
        },
        (done, total, label) => updateProgress(done, total, label),
        'Creating sites…',
      )
      results.forEach((r, i) => {
        if (r.status === 'fulfilled') {
          siteResult.created++
          const row = sitesToImport[i]
          const key = `${row['client_name*'].trim()}||${row['site_name*'].trim()}`
          siteNameToId.set(key, r.value.id)
        } else {
          siteResult.failed++
          siteResult.errors.push(
            `Row ${siteValidation[i]?.rowIndex + 1}: ${r.reason instanceof Error ? r.reason.message : 'Failed'}`,
          )
        }
      })
      stepsCompleted++
    }

    // 3. Create assets
    const assetResult: ImportResult = { created: 0, failed: 0, errors: [] }

    if (assetsToImport.length > 0) {
      setProgressLabel('Creating assets…')
      const results = await batchRun(
        assetsToImport,
        10,
        (row) => {
          const clientId = clientNameToId.get(row['client_name*'].trim())
          if (!clientId) return Promise.reject(new Error(`No ID for client "${row['client_name*']}"`))
          const siteKey = `${row['client_name*'].trim()}||${row['site_name*'].trim()}`
          const siteId = siteNameToId.get(siteKey) ?? null
          const input: UpsertAssetInput = {
            clientId,
            siteId,
            assetName: row['asset_name*'].trim(),
            assetCode: row['asset_code']?.trim() || undefined,
            manufacturer: row['make']?.trim() || null,
            model: row['model']?.trim() || null,
            serialNumber: row['serial_number']?.trim() || null,
            installationDate: row['install_date']?.trim() || null,
            status: row['status']?.trim() || 'Active',
            autoSchedulePm: false,
          }
          return assetService.create(input)
        },
        (done, total, label) => updateProgress(done, total, label),
        'Creating assets…',
      )
      results.forEach((r, i) => {
        if (r.status === 'fulfilled') {
          assetResult.created++
        } else {
          assetResult.failed++
          assetResult.errors.push(
            `Row ${assetValidation[i]?.rowIndex + 1}: ${r.reason instanceof Error ? r.reason.message : 'Failed'}`,
          )
        }
      })
    }

    setProgressValue(100)
    setProgressLabel('Done!')
    setSummary({ clients: clientResult, sites: siteResult, assets: assetResult })
    setImporting(false)
  }

  // ── Derived counts ────────────────────────────────────────────────────────

  const clientErrors = clientValidation.filter((v) => v.errors.length > 0)
  const siteErrors = siteValidation.filter((v) => v.errors.length > 0)
  const assetErrors = assetValidation.filter((v) => v.errors.length > 0)
  const totalRows = parsed.clients.length + parsed.sites.length + parsed.assets.length
  const totalValid =
    clientValidation.filter((v) => v.errors.length === 0).length +
    siteValidation.filter((v) => v.errors.length === 0).length +
    assetValidation.filter((v) => v.errors.length === 0).length
  const totalErrCount = clientErrors.length + siteErrors.length + assetErrors.length

  const canStartImport =
    parsed.clients.length > 0 &&
    (skipErrors ? totalValid > 0 : totalErrCount === 0)

  // ─────────────────────────────────────────────────────────────────────────

  return (
    <Modal
      open={open}
      title="Bulk import clients"
      description="Import clients, sites, and assets from a CSV or Excel file."
      onClose={handleClose}
      maxWidth="max-w-4xl"
    >
      <StepIndicator current={step} />

      {/* ─── Step 1: Choose type ─── */}
      {step === 1 && (
        <div className="space-y-5">
          <p className="text-sm text-muted">Select what you want to import, then download the matching template.</p>

          <div className="grid gap-4 sm:grid-cols-3">
            <ImportTypeCard
              selected={importType === 'clients'}
              onClick={() => setImportType('clients')}
              icon={<Users className="h-6 w-6" />}
              title="Clients only"
              description="Import client records. Assign sites separately."
              badge={null}
              onDownload={() => downloadCsvTemplate()}
              templateLabel="Download CSV template"
            />
            <ImportTypeCard
              selected={importType === 'clients-sites'}
              onClick={() => setImportType('clients-sites')}
              icon={<FileSpreadsheet className="h-6 w-6" />}
              title="Clients + Sites"
              description="Import clients and their physical locations together."
              badge={null}
              onDownload={() => downloadExcelTemplate('clients-sites')}
              templateLabel="Download Excel template"
            />
            <ImportTypeCard
              selected={importType === 'full'}
              onClick={() => setImportType('full')}
              icon={<FileSpreadsheet className="h-6 w-6" />}
              title="Full import"
              description="Clients, sites, and assets in one file. Recommended for onboarding."
              badge="Recommended"
              onDownload={() => downloadExcelTemplate('full')}
              templateLabel="Download Excel template"
            />
          </div>

          <div className="flex justify-end pt-2">
            <button type="button" className="button-primary" onClick={() => setStep(2)}>
              Next: Upload file →
            </button>
          </div>
        </div>
      )}

      {/* ─── Step 2: Upload & validate ─── */}
      {step === 2 && (
        <div className="space-y-5">
          {/* Drop zone */}
          <div
            className={`relative flex flex-col items-center justify-center gap-3 rounded-[20px] border-2 border-dashed px-6 py-10 transition-colors cursor-pointer
              ${isDragging ? 'border-teal-400 bg-teal-500/10' : 'border-white/20 hover:border-teal-400/50 hover:bg-teal-500/5'}`}
            onDragOver={(e) => { e.preventDefault(); setIsDragging(true) }}
            onDragLeave={() => setIsDragging(false)}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
          >
            <Upload className="h-8 w-8 text-muted" />
            <div className="text-center">
              <p className="text-sm font-medium text-app">Drag & drop your file here, or click to browse</p>
              <p className="mt-1 text-xs text-muted">Accepts .csv and .xlsx files</p>
            </div>
            <input
              ref={fileInputRef}
              type="file"
              accept=".csv,.xlsx,.xls"
              className="sr-only"
              onChange={(e) => handleFileChange(e.target.files?.[0])}
            />
          </div>

          {parseError && (
            <div className="rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
              {parseError}
            </div>
          )}

          {/* Summary bar */}
          {totalRows > 0 && !parseError && (
            <div className="flex flex-wrap items-center gap-4 rounded-2xl border border-app bg-subtle px-4 py-3">
              <span className="text-sm text-app font-medium">{totalRows} rows found</span>
              <span className="text-sm text-emerald-400">{totalValid} valid</span>
              {totalErrCount > 0 && <span className="text-sm text-rose-300">{totalErrCount} with errors</span>}
            </div>
          )}

          {/* Preview tables */}
          {parsed.clients.length > 0 && (
            <PreviewSection
              title={`Clients (${parsed.clients.length})`}
              headers={CLIENT_HEADERS}
              rows={parsed.clients}
              validations={clientValidation}
            />
          )}
          {parsed.sites.length > 0 && (
            <PreviewSection
              title={`Sites (${parsed.sites.length})`}
              headers={SITE_HEADERS}
              rows={parsed.sites}
              validations={siteValidation}
            />
          )}
          {parsed.assets.length > 0 && (
            <PreviewSection
              title={`Assets (${parsed.assets.length})`}
              headers={ASSET_HEADERS}
              rows={parsed.assets}
              validations={assetValidation}
            />
          )}

          {/* Error list */}
          {totalErrCount > 0 && (
            <div className="rounded-2xl border border-rose-400/20 bg-rose-500/5 px-4 py-3 space-y-2">
              <p className="text-sm font-semibold text-rose-300">Validation errors</p>
              {[...clientErrors, ...siteErrors, ...assetErrors].slice(0, 20).map((v) => (
                <div key={`${v.rowIndex}-${v.errors[0]}`} className="text-xs text-rose-200">
                  Row {v.rowIndex + 1}: {v.errors.join(' · ')}
                </div>
              ))}
              {totalErrCount > 20 && <p className="text-xs text-muted">…and {totalErrCount - 20} more</p>}
            </div>
          )}

          {totalErrCount > 0 && (
            <label className="flex items-center gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={skipErrors}
                onChange={(e) => setSkipErrors(e.target.checked)}
                className="h-4 w-4 rounded accent-teal-500"
              />
              <span className="text-sm text-muted">Skip rows with errors and import valid rows only</span>
            </label>
          )}

          <div className="flex items-center justify-between pt-2">
            <button type="button" className="button-secondary" onClick={() => setStep(1)}>← Back</button>
            <button
              type="button"
              className="button-primary"
              disabled={!canStartImport}
              onClick={() => { setStep(3); void runImport() }}
            >
              Start import →
            </button>
          </div>
        </div>
      )}

      {/* ─── Step 3: Progress & results ─── */}
      {step === 3 && (
        <div className="space-y-6">
          {/* Progress bar */}
          <div className="space-y-2">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted">{progressLabel}</span>
              <span className="text-app font-medium">{progressValue}%</span>
            </div>
            <div className="h-2.5 w-full rounded-full bg-white/10 overflow-hidden">
              <div
                className="h-full rounded-full bg-emerald-500 transition-all duration-300 ease-out"
                style={{ width: `${progressValue}%` }}
              />
            </div>
          </div>

          {/* Final summary */}
          {summary && !importing && (
            <div className="space-y-4">
              <div className="grid gap-3 sm:grid-cols-3">
                <ResultCard
                  label="Clients"
                  created={summary.clients.created}
                  failed={summary.clients.failed}
                />
                <ResultCard
                  label="Sites"
                  created={summary.sites.created}
                  failed={summary.sites.failed}
                />
                <ResultCard
                  label="Assets"
                  created={summary.assets.created}
                  failed={summary.assets.failed}
                />
              </div>

              {/* Failed rows */}
              {[...summary.clients.errors, ...summary.sites.errors, ...summary.assets.errors].length > 0 && (
                <div className="rounded-2xl border border-rose-400/20 bg-rose-500/5 px-4 py-3 space-y-1.5">
                  <p className="text-sm font-semibold text-rose-300">Failed rows</p>
                  {[...summary.clients.errors, ...summary.sites.errors, ...summary.assets.errors].map((e, i) => (
                    <p key={i} className="text-xs text-rose-200">{e}</p>
                  ))}
                </div>
              )}

              <div className="flex justify-end pt-2">
                <button
                  type="button"
                  className="button-primary"
                  onClick={() => { resetAll(); onSuccess() }}
                >
                  View clients
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </Modal>
  )
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function ImportTypeCard({
  selected,
  onClick,
  icon,
  title,
  description,
  badge,
  onDownload,
  templateLabel,
}: {
  selected: boolean
  onClick: () => void
  icon: React.ReactNode
  title: string
  description: string
  badge: string | null
  onDownload: () => void
  templateLabel: string
}) {
  return (
    <div
      onClick={onClick}
      className={`relative flex cursor-pointer flex-col gap-3 rounded-[20px] border p-4 transition-all
        ${selected
          ? 'border-teal-500 bg-teal-500/10 ring-2 ring-teal-500/30'
          : 'border-app bg-subtle hover:border-teal-400/40 hover:bg-teal-500/5'
        }`}
    >
      {badge && (
        <span className="absolute right-3 top-3 rounded-full bg-emerald-500/20 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-emerald-300">
          {badge}
        </span>
      )}
      <div className={`${selected ? 'text-teal-400' : 'text-muted'}`}>{icon}</div>
      <div>
        <p className="font-semibold text-app text-sm">{title}</p>
        <p className="mt-1 text-xs text-muted">{description}</p>
      </div>
      <button
        type="button"
        className="mt-auto flex items-center gap-1.5 text-xs font-medium text-teal-400 hover:text-teal-300 transition-colors"
        onClick={(e) => { e.stopPropagation(); onDownload() }}
      >
        <Download className="h-3.5 w-3.5" />
        {templateLabel}
      </button>
    </div>
  )
}

function PreviewSection({
  title,
  headers,
  rows,
  validations,
}: {
  title: string
  headers: string[]
  rows: ImportRow[]
  validations: ValidationResult[]
}) {
  const displayRows = rows.slice(0, 20)
  return (
    <div className="space-y-1.5">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{title}</p>
      <div className="overflow-hidden rounded-[20px] border border-app">
        <div className="overflow-x-auto max-h-48">
          <table className="min-w-full text-xs">
            <thead className="sticky top-0 z-10 bg-overlay/80">
              <tr>
                {headers.map((h) => (
                  <th key={h} className="whitespace-nowrap px-3 py-2 text-left font-semibold uppercase tracking-wider text-muted">
                    {h}
                  </th>
                ))}
                <th className="px-3 py-2 text-left font-semibold uppercase tracking-wider text-muted">Status</th>
              </tr>
            </thead>
            <tbody>
              {displayRows.map((row, i) => {
                const isInvalid = (validations[i]?.errors.length ?? 0) > 0
                return (
                  <tr
                    key={i}
                    className={`border-t border-app/50 ${isInvalid ? 'border-l-2 border-l-rose-400 bg-rose-500/5' : ''}`}
                  >
                    {headers.map((h) => (
                      <td key={h} className="whitespace-nowrap px-3 py-2 text-app">{row[h] || '—'}</td>
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

function ResultCard({ label, created, failed }: { label: string; created: number; failed: number }) {
  return (
    <div className="rounded-2xl border border-app bg-subtle p-4">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{label}</p>
      <p className="mt-2 text-2xl font-semibold text-emerald-400">{created}</p>
      <div className="mt-1 flex items-center gap-1.5 text-xs">
        <span className="text-muted">created</span>
        {failed > 0 && <span className="text-rose-300">· {failed} failed</span>}
      </div>
    </div>
  )
}
