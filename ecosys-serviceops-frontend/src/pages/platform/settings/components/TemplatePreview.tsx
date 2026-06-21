import { EcosysLogo, PoweredByEcosys } from '../../../../components/brand'
import { Modal } from '../../../../components/ui/Modal'
import { useThemeMode } from '../../../../context/ThemeContext'
import type { PlatformTemplatePreview } from '../../../../services/platformSettingsService'

type Props = {
  open: boolean
  preview: PlatformTemplatePreview | null
  onClose: () => void
}

export function TemplatePreview({ open, preview, onClose }: Props) {
  const { theme } = useThemeMode()
  const logoVariant = theme === 'light' ? 'dark' : 'light'
  const poweredByTone = theme === 'light' ? 'light' : 'dark'

  return (
    <Modal
      open={open}
      title={preview ? `${preview.name} Preview` : 'Template Preview'}
      description="Rendered with sample data."
      onClose={onClose}
      maxWidth="max-w-5xl"
    >
      {!preview ? (
        <div data-testid="template-preview-panel" className="panel-subtle rounded-2xl p-4 text-sm text-muted">No preview available.</div>
      ) : (
        <div data-testid="template-preview-panel" className="space-y-3">
          <article className="panel-subtle flex flex-wrap items-center justify-between gap-3 rounded-2xl p-4">
            <EcosysLogo variant={logoVariant} size="md" subtitle="Document Template Preview" />
            <PoweredByEcosys tone={poweredByTone} />
          </article>
          <article className="panel-subtle rounded-2xl p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Subject</p>
            <p className="mt-2 text-sm text-app">{preview.subject}</p>
          </article>
          <article className="rounded-2xl border border-app bg-white p-2">
            <iframe title="template-preview" srcDoc={preview.renderedHtml} className="h-[460px] w-full rounded-xl border-0 bg-white" />
          </article>
        </div>
      )}
    </Modal>
  )
}
