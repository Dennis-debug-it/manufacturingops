import { useState } from 'react'
import toast from 'react-hot-toast'
import { CheckCircle2, Download, TriangleAlert } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { downloadImportTemplate, useImportAssets } from '../api'
import type { ImportPreview } from '../types'
import { primaryButton, secondaryButton } from './ui'

/** Bulk onboarding: download the template, check the file, then import it all at once. */
export function ImportAssetsDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<ImportPreview | null>(null)
  const importAssets = useImportAssets()

  const close = () => {
    setFile(null)
    setPreview(null)
    onClose()
  }

  const check = async (chosen: File) => {
    setFile(chosen)
    setPreview(null)
    try {
      setPreview(await importAssets.mutateAsync({ file: chosen, commit: false }) as ImportPreview)
    } catch {
      // The API client shows the error.
    }
  }

  const commit = async () => {
    if (!file) return
    try {
      const result = await importAssets.mutateAsync({ file, commit: true }) as { created: number; tagged: number }
      toast.success(`Imported ${result.created} asset${result.created === 1 ? '' : 's'}${result.tagged ? `, ${result.tagged} with tags` : ''}.`)
      close()
    } catch {
      // The API client shows the error.
    }
  }

  const problems = preview?.rows.filter((row) => row.errors.length > 0) ?? []

  return (
    <FormDrawer
      open={open}
      title="Import assets"
      onClose={close}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" className={secondaryButton} onClick={close}>Cancel</button>
          <button type="button" className={primaryButton} disabled={!preview || problems.length > 0 || preview.totalRows === 0 || importAssets.isPending} onClick={() => void commit()}>
            Import {preview ? preview.validRows : ''} asset{preview?.validRows === 1 ? '' : 's'}
          </button>
        </div>
      }
    >
      <div className="space-y-4 text-sm text-slate-700">
        <ol className="list-decimal space-y-1 pl-5">
          <li>Download the template. Its other sheets list your location codes and asset classes.</li>
          <li>Fill in one row per asset. Components name their parent's number.</li>
          <li>Choose the file below. Nothing is saved until every row is correct.</li>
        </ol>
        <button type="button" className={secondaryButton} onClick={() => void downloadImportTemplate()}><Download className="h-4 w-4" />Download template</button>
        <input
          type="file"
          accept=".xlsx,.xls,.csv"
          onChange={(event) => {
            const chosen = event.target.files?.[0]
            if (chosen) void check(chosen)
          }}
          className="block w-full text-sm file:mr-3 file:rounded-lg file:border-0 file:bg-slate-100 file:px-3 file:py-2 file:text-sm file:font-medium"
        />
        {importAssets.isPending && !preview ? <p>Checking the file…</p> : null}
        {preview ? (
          problems.length === 0 ? (
            <p className="flex items-center gap-2 rounded-lg bg-emerald-50 px-3 py-2 text-emerald-800">
              <CheckCircle2 className="h-4 w-4" />All {preview.totalRows} rows are ready to import.
            </p>
          ) : (
            <div className="space-y-2">
              <p className="flex items-center gap-2 rounded-lg bg-amber-50 px-3 py-2 text-amber-900">
                <TriangleAlert className="h-4 w-4" />{problems.length} of {preview.totalRows} rows need fixing. Correct them in the file and choose it again.
              </p>
              <ul className="max-h-80 divide-y divide-slate-100 overflow-y-auto rounded-lg border border-slate-200">
                {problems.map((row) => (
                  <li key={row.rowNumber} className="px-3 py-2">
                    <span className="font-medium">Row {row.rowNumber}</span>
                    {row.assetCode ? <span className="ml-2 font-mono text-xs text-slate-500">{row.assetCode}</span> : null}
                    <ul className="mt-1 list-disc pl-5 text-rose-700">
                      {row.errors.map((error) => <li key={error}>{error}</li>)}
                    </ul>
                  </li>
                ))}
              </ul>
            </div>
          )
        ) : null}
      </div>
    </FormDrawer>
  )
}
