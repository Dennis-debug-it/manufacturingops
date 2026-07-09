import { Download, Upload } from 'lucide-react'
import { useMemo, useState } from 'react'
import { BulkImportModal } from '../../components/ui/BulkImportModal'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { importService, type ImportTemplateType } from '../../services/importService'

type ImportOption = {
  id: ImportTemplateType
  label: string
  description: string
}

const importOptions: ImportOption[] = [
  { id: 'clients', label: 'Clients', description: 'Import customer records and contact details using the standard import format.' },
  { id: 'assets', label: 'Assets', description: 'Preview and import asset registers with PM-ready fields.' },
  { id: 'users', label: 'Users', description: 'Create workspace users in bulk and map branches from one CSV.' },
  { id: 'branches', label: 'Branches', description: 'Create or validate branches and outlet structures in bulk.' },
]

export function DataImportSettingsPage() {
  const { pushToast } = useToast()
  const [selectedType, setSelectedType] = useState<ImportTemplateType>('clients')
  const [downloading, setDownloading] = useState(false)
  const [downloadError, setDownloadError] = useState('')
  const [modalOpen, setModalOpen] = useState(false)

  const selectedOption = useMemo(
    () => importOptions.find((option) => option.id === selectedType) ?? importOptions[0],
    [selectedType],
  )

  const actions = useMemo(
    () => ({
      preview: selectedType === 'clients'
        ? importService.previewClients
        : selectedType === 'assets'
          ? importService.previewAssets
          : selectedType === 'users'
            ? importService.previewUsers
            : importService.previewBranches,
      commit: selectedType === 'clients'
        ? importService.commitClients
        : selectedType === 'assets'
          ? importService.commitAssets
          : selectedType === 'users'
            ? importService.commitUsers
            : importService.commitBranches,
    }),
    [selectedType],
  )

  async function handleDownload() {
    if (downloading) return

    setDownloading(true)
    setDownloadError('')
    try {
      await importService.downloadTemplate(selectedType)
      pushToast({
        title: 'Template downloaded',
        description: `${selectedOption.label} import template downloaded successfully.`,
        tone: 'success',
      })
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Template download failed. Please try again.'
      setDownloadError(message)
      pushToast({ title: 'Download failed', description: message, tone: 'danger' })
    } finally {
      setDownloading(false)
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Data Import"
        description="Download the right template first, preview your file, then commit only validated rows into your workspace."
      />

      <section className="surface-card space-y-6">
        <div className="grid gap-3 lg:grid-cols-4">
          {importOptions.map((option) => (
            <button
              key={option.id}
              type="button"
              className={`rounded-[28px] border px-5 py-5 text-left transition ${selectedType === option.id ? 'border-[var(--app-accent)] bg-[color:color-mix(in_srgb,var(--app-accent)_12%,transparent)]' : 'border-app bg-[var(--app-surface-elevated)] hover:border-[var(--app-accent)]/50'}`}
              onClick={() => {
                setSelectedType(option.id)
                setDownloadError('')
              }}
            >
              <p className="text-sm font-semibold text-app">{option.label}</p>
              <p className="mt-2 text-sm leading-6 text-muted">{option.description}</p>
            </button>
          ))}
        </div>

        <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_auto_auto] xl:items-end">
          <div className="panel-subtle rounded-[28px] p-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Selected import</p>
            <p className="mt-3 text-2xl font-semibold text-app">{selectedOption.label}</p>
            <p className="mt-3 text-sm leading-6 text-muted">
              Download the CSV template first so column headers match the validation rules exactly.
            </p>
            {downloadError ? (
              <div className="mt-4 rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
                {downloadError}
              </div>
            ) : null}
          </div>

          <button type="button" className="button-secondary" onClick={() => void handleDownload()} disabled={downloading}>
            <Download className="h-4 w-4" />
            {downloading ? 'Downloading...' : 'Download Template'}
          </button>

          <button type="button" className="button-primary" onClick={() => setModalOpen(true)}>
            <Upload className="h-4 w-4" />
            Upload & Preview
          </button>
        </div>
      </section>

      <BulkImportModal
        open={modalOpen}
        title={`Bulk import ${selectedOption.label.toLowerCase()}`}
        description={`Preview ${selectedOption.label.toLowerCase()} rows before committing them to your workspace.`}
        onClose={() => setModalOpen(false)}
        onPreview={(file) => actions.preview(file)}
        onCommit={(file) => actions.commit(file)}
        onCommitted={(summary) => {
          pushToast({
            title: `${selectedOption.label} import complete`,
            description: `Imported ${summary.successfulRows} of ${summary.totalRows} rows.`,
            tone: summary.failedRows > 0 ? 'warning' : 'success',
          })
        }}
      />
    </div>
  )
}
