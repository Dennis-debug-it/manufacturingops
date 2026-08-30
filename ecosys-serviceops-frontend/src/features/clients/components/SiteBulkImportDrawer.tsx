import { useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Download, Upload } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { importService } from '../../../shared/services/importService'
import type { ImportCommitResponse, ImportPreviewResponse } from '../../../shared/types/import'

interface SiteBulkImportDrawerProps {
  open: boolean
  onClose: () => void
}

export function SiteBulkImportDrawer({ open, onClose }: SiteBulkImportDrawerProps) {
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<ImportPreviewResponse | null>(null)
  const [summary, setSummary] = useState<ImportCommitResponse | null>(null)
  const [previewing, setPreviewing] = useState(false)
  const [importing, setImporting] = useState(false)
  const [downloading, setDownloading] = useState(false)
  const [error, setError] = useState('')

  const rowKeys = useMemo(() => Object.keys(preview?.rows[0]?.rawValues ?? {}), [preview])
  const validRows = preview?.rows.filter((row) => row.isValid).length ?? 0
  const errorRows = (preview?.rows.length ?? 0) - validRows

  function reset() {
    setFile(null)
    setPreview(null)
    setSummary(null)
    setPreviewing(false)
    setImporting(false)
    setDownloading(false)
    setError('')
  }

  function handleClose() {
    if (previewing || importing) {
      return
    }
    reset()
    onClose()
  }

  async function handleDownloadTemplate() {
    try {
      setDownloading(true)
      await importService.downloadSiteTemplate()
    } catch {
      // toast handled by interceptor
    } finally {
      setDownloading(false)
    }
  }

  async function handlePreview() {
    if (!file) {
      toast.error('Choose a CSV file first')
      return
    }

    try {
      setPreviewing(true)
      setSummary(null)
      setError('')
      const response = await importService.previewSites(file)
      setPreview(response)
      if (response.totalRows === 0) {
        toast.error('The selected file has no rows to import')
      }
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : 'Could not preview this file.')
    } finally {
      setPreviewing(false)
    }
  }

  async function handleImport() {
    if (!file || !preview || validRows === 0) {
      return
    }

    try {
      setImporting(true)
      setError('')
      const response = await importService.commitSites(file)
      setSummary(response)
      await queryClient.invalidateQueries({ queryKey: ['sites'] })
      toast.success(`Imported ${response.successfulRows} sites`)
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : 'Could not complete this import.')
    } finally {
      setImporting(false)
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Bulk Import Sites"
      onClose={handleClose}
      footer={(
        <div className="flex flex-wrap items-center justify-between gap-2">
          <button
            type="button"
            onClick={() => void handleDownloadTemplate()}
            disabled={downloading || previewing || importing}
            className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60"
          >
            <Download className="h-4 w-4" />
            {downloading ? 'Preparing Template...' : 'Download Template'}
          </button>
          <div className="flex flex-wrap items-center gap-2">
            <button type="button" onClick={handleClose} disabled={previewing || importing} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              Close
            </button>
            <button type="button" onClick={() => void handlePreview()} disabled={!file || previewing || importing} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              {previewing ? 'Previewing...' : 'Preview Import'}
            </button>
            <button type="button" onClick={() => void handleImport()} disabled={!preview || validRows === 0 || previewing || importing} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {importing ? 'Importing...' : 'Confirm Import'}
            </button>
          </div>
        </div>
      )}
    >
      <div className="space-y-4">
        <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
          Upload a sites CSV. Each row must reference an existing client by name or ID, preview the rows, then confirm the import. Download the template for the exact column order. Duplicate site names per client are flagged and skipped.
        </div>

        <label className="flex min-h-32 cursor-pointer flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-8 text-center transition hover:border-slate-400 hover:bg-slate-50">
          <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-slate-100 text-slate-700">
            <Upload className="h-5 w-5" />
          </span>
          <span className="space-y-1">
            <span className="block text-sm font-medium text-slate-900">Choose a sites CSV file</span>
            <span className="block text-sm text-slate-500">{file ? file.name : 'Only CSV files are supported.'}</span>
          </span>
          <input
            type="file"
            accept=".csv"
            className="hidden"
            onChange={(event) => {
              setFile(event.target.files?.[0] ?? null)
              setPreview(null)
              setSummary(null)
              setError('')
            }}
          />
        </label>

        {preview ? (
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Rows</p>
              <p className="mt-2 text-2xl font-semibold text-slate-900">{preview.totalRows}</p>
            </div>
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Valid</p>
              <p className="mt-2 text-2xl font-semibold text-emerald-600">{validRows}</p>
            </div>
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Needs Review</p>
              <p className="mt-2 text-2xl font-semibold text-amber-600">{errorRows}</p>
            </div>
          </div>
        ) : null}

        {error ? <div className="rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</div> : null}

        {preview ? (
          <div className="overflow-hidden rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
            <div className="max-h-[22rem] overflow-auto">
              <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                <thead className="sticky top-0 bg-slate-50">
                  <tr>
                    <th className="px-4 py-3 font-medium text-slate-600">Row</th>
                    <th className="px-4 py-3 font-medium text-slate-600">Status</th>
                    {rowKeys.map((key) => (
                      <th key={key} className="px-4 py-3 font-medium text-slate-600">{key}</th>
                    ))}
                    <th className="px-4 py-3 font-medium text-slate-600">Errors</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {preview.rows.map((row) => (
                    <tr key={row.rowNumber}>
                      <td className="px-4 py-3 text-slate-700">{row.rowNumber}</td>
                      <td className="px-4 py-3">
                        <span className={row.isValid ? 'font-medium text-emerald-700' : 'font-medium text-amber-700'}>
                          {row.isValid ? 'Valid' : 'Needs Review'}
                        </span>
                      </td>
                      {rowKeys.map((key) => (
                        <td key={key} className="px-4 py-3 text-slate-700">{row.rawValues[key] ?? '-'}</td>
                      ))}
                      <td className="px-4 py-3 text-rose-700">{row.errors.join(' ') || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}

        {summary ? (
          <div className="space-y-3">
            <div className="rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-700">
              Imported {summary.successfulRows} of {summary.totalRows} rows. Failed {summary.failedRows}. Skipped {summary.skippedRows}.
            </div>
            {summary.rowErrors && summary.rowErrors.length > 0 ? (
              <div className="overflow-hidden rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
                <div className="max-h-48 overflow-auto">
                  <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                    <thead className="bg-slate-50">
                      <tr>
                        <th className="px-4 py-3 font-medium text-slate-600">Row</th>
                        <th className="px-4 py-3 font-medium text-slate-600">Errors</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {summary.rowErrors.map((row) => (
                        <tr key={row.rowNumber}>
                          <td className="px-4 py-3 text-slate-700">{row.rowNumber}</td>
                          <td className="px-4 py-3 text-rose-700">{row.errors.join(' ')}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : null}
          </div>
        ) : null}
      </div>
    </FormDrawer>
  )
}
