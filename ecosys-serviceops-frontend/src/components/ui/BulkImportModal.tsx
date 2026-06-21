import { useMemo, useState, type ReactNode } from 'react'
import type { ImportCommitResponse, ImportPreviewResponse } from '../../types/api'
import { Modal } from './Modal'

const acceptedFileTypes = '.csv'

export function BulkImportModal({
  open,
  title,
  description,
  onClose,
  onPreview,
  onCommit,
  onCommitted,
  instructions,
  optionsSlot,
}: {
  open: boolean
  title: string
  description: string
  onClose: () => void
  onPreview: (file: File) => Promise<ImportPreviewResponse>
  onCommit: (file: File) => Promise<ImportCommitResponse>
  onCommitted: (summary: ImportCommitResponse) => Promise<void> | void
  instructions?: string[]
  optionsSlot?: ReactNode
}) {
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<ImportPreviewResponse | null>(null)
  const [summary, setSummary] = useState<ImportCommitResponse | null>(null)
  const [error, setError] = useState('')
  const [previewing, setPreviewing] = useState(false)
  const [committing, setCommitting] = useState(false)

  const rowKeys = useMemo(() => {
    if (!preview?.rows[0]) return []
    return Object.keys(preview.rows[0].rawValues)
  }, [preview])
  const validRows = preview?.rows.filter((row) => row.isValid).length ?? 0
  const errorRows = (preview?.rows.length ?? 0) - validRows

  function resetState() {
    setFile(null)
    setPreview(null)
    setSummary(null)
    setError('')
    setPreviewing(false)
    setCommitting(false)
  }

  async function handlePreview() {
    if (!file || previewing) return

    setPreviewing(true)
    setError('')
    setSummary(null)

    try {
      const result = await onPreview(file)
      setPreview(result)
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : 'Unable to preview this import file.')
    } finally {
      setPreviewing(false)
    }
  }

  async function handleCommit() {
    if (!file || !preview || validRows === 0 || committing) return

    setCommitting(true)
    setError('')

    try {
      const result = await onCommit(file)
      setSummary(result)
      await onCommitted(result)
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : 'Unable to complete this import.')
    } finally {
      setCommitting(false)
    }
  }

  return (
    <Modal
      open={open}
      title={title}
      description={description}
      onClose={() => {
        if (previewing || committing) return
        resetState()
        onClose()
      }}
      maxWidth="max-w-5xl"
    >
      <div className="space-y-4">
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_auto_auto] lg:items-end">
          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Upload file</span>
            <input
              type="file"
              accept={acceptedFileTypes}
              onChange={(event) => {
                const nextFile = event.target.files?.[0] ?? null
                setFile(nextFile)
                setPreview(null)
                setSummary(null)
                setError('')
              }}
              className="field-input"
            />
          </label>
          <button type="button" className="button-secondary" onClick={() => void handlePreview()} disabled={!file || previewing || committing}>
            {previewing ? 'Previewing...' : 'Preview'}
          </button>
          <button type="button" className="button-primary" onClick={() => void handleCommit()} disabled={!preview || validRows === 0 || previewing || committing}>
            {committing ? 'Importing...' : 'Confirm import'}
          </button>
        </div>

        <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">
          {file ? `Selected file: ${file.name}` : 'Select a CSV or Excel file to preview the import results.'}
        </div>

        {instructions && instructions.length > 0 ? (
          <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">
            <p className="font-medium text-app">Import notes</p>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              {instructions.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </div>
        ) : null}

        {optionsSlot ? <div className="panel-subtle rounded-2xl p-4">{optionsSlot}</div> : null}

        {preview ? (
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="panel-subtle rounded-2xl p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Total rows</p>
              <p className="mt-2 text-2xl font-semibold text-app">{preview.totalRows}</p>
            </div>
            <div className="panel-subtle rounded-2xl p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Valid rows</p>
              <p className="mt-2 text-2xl font-semibold text-emerald-300">{validRows}</p>
            </div>
            <div className="panel-subtle rounded-2xl p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Rows with errors</p>
              <p className="mt-2 text-2xl font-semibold text-amber-300">{errorRows}</p>
            </div>
          </div>
        ) : null}

        {error ? <div className="rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{error}</div> : null}

        {preview ? (
          <div className="overflow-hidden rounded-[28px] border border-app">
            <div className="overflow-x-auto">
              <table className="min-w-full divide-app text-sm">
                <thead className="table-head">
                  <tr>
                    <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-secondary">Row</th>
                    <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-secondary">Status</th>
                    {rowKeys.map((key) => (
                      <th key={key} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-secondary">
                        {key}
                      </th>
                    ))}
                    <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-secondary">Errors</th>
                  </tr>
                </thead>
                <tbody className="divide-app">
                  {preview.rows.map((row) => (
                    <tr key={row.rowNumber} className="table-row">
                      <td className="px-4 py-3 text-app">{row.rowNumber}</td>
                      <td className="px-4 py-3">
                        <span className={row.isValid ? 'text-emerald-300' : 'text-amber-300'}>{row.isValid ? 'Valid' : 'Needs review'}</span>
                      </td>
                      {rowKeys.map((key) => (
                        <td key={key} className="px-4 py-3 text-app">{row.rawValues[key] || '-'}</td>
                      ))}
                      <td className="px-4 py-3 text-rose-200">{row.errors.join(' ') || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}

        {summary ? (
          <div className="space-y-3">
            <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">
              Imported {summary.successfulRows} of {summary.totalRows} rows. Skipped {summary.skippedRows}; errors {summary.failedRows}.
            </div>
            {summary.rowErrors && summary.rowErrors.length > 0 ? (
              <div className="overflow-hidden rounded-[28px] border border-app">
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-app text-sm">
                    <thead className="table-head">
                      <tr>
                        <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-secondary">Row</th>
                        <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-secondary">Errors</th>
                      </tr>
                    </thead>
                    <tbody className="divide-app">
                      {summary.rowErrors.map((row) => (
                        <tr key={row.rowNumber} className="table-row">
                          <td className="px-4 py-3 text-app">{row.rowNumber}</td>
                          <td className="px-4 py-3 text-rose-200">{row.errors.join(' ')}</td>
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
    </Modal>
  )
}
