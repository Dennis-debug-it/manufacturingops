import { Modal } from '../../../../components/ui/Modal'
import type { PlatformTemplate, PlatformTemplatePageSize, PlatformTemplateOrientation } from '../../../../services/platformSettingsService'

type Props = {
  open: boolean
  value: PlatformTemplate | null
  saving: boolean
  onClose: () => void
  onChange: (next: PlatformTemplate) => void
  onSave: () => void
}

const placeholderList = [
  '{{platform.name}}',
  '{{platform.logo}}',
  '{{tenant.name}}',
  '{{tenant.logo}}',
  '{{tenant.email}}',
  '{{tenant.phone}}',
  '{{tenant.address}}',
  '{{customer.name}}',
  '{{customer.email}}',
  '{{customer.phone}}',
  '{{document.number}}',
  '{{document.date}}',
  '{{document.dueDate}}',
  '{{document.subtotal}}',
  '{{document.tax}}',
  '{{document.discount}}',
  '{{document.total}}',
  '{{document.balance}}',
  '{{invoice.number}}',
  '{{quotation.number}}',
  '{{payment.amount}}',
  '{{payment.method}}',
  '{{workOrder.number}}',
  '{{workOrder.title}}',
  '{{workOrder.status}}',
  '{{asset.name}}',
  '{{technician.name}}',
  '{{currentUser.name}}',
]

export function TemplateEditor({ open, value, saving, onClose, onChange, onSave }: Props) {
  if (!value) return null

  const isEmailTemplate = value.type.toLowerCase().includes('email')

  return (
    <Modal
      open={open}
      title={value.id ? 'Edit Template' : 'Create Template'}
      description="Build reusable document and email templates with dynamic placeholders."
      onClose={onClose}
      maxWidth="max-w-6xl"
    >
      <div data-testid="template-editor" className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2">
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Template Name</span>
            <input value={value.name} onChange={(event) => onChange({ ...value, name: event.target.value })} className="field-input" />
          </label>
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Template Type</span>
            <input data-testid="template-type-select" value={value.type} onChange={(event) => onChange({ ...value, type: event.target.value })} className="field-input" />
          </label>
        </div>
        {isEmailTemplate ? (
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Subject</span>
            <input data-testid="template-subject-input" value={value.subject} onChange={(event) => onChange({ ...value, subject: event.target.value })} className="field-input" />
          </label>
        ) : null}
        <div className="grid gap-3 md:grid-cols-2">
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Header Content</span>
            <textarea data-testid="template-header-editor" value={value.headerHtml} onChange={(event) => onChange({ ...value, headerHtml: event.target.value })} className="field-input min-h-[90px]" />
          </label>
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Footer Content</span>
            <textarea data-testid="template-footer-editor" value={value.footerHtml} onChange={(event) => onChange({ ...value, footerHtml: event.target.value })} className="field-input min-h-[90px]" />
          </label>
        </div>
        <label className="space-y-2">
          <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Body Content</span>
          <textarea data-testid="template-body-editor" value={value.bodyHtml} onChange={(event) => onChange({ ...value, bodyHtml: event.target.value })} className="field-input min-h-[220px] font-mono text-xs" />
        </label>
        <div className="grid gap-3 md:grid-cols-2">
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Terms & Conditions</span>
            <textarea value={value.termsHtml} onChange={(event) => onChange({ ...value, termsHtml: event.target.value })} className="field-input min-h-[100px]" />
          </label>
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Signature Block</span>
            <textarea value={value.signatureHtml} onChange={(event) => onChange({ ...value, signatureHtml: event.target.value })} className="field-input min-h-[100px]" />
          </label>
        </div>
        <div className="grid gap-3 md:grid-cols-2">
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Page Size</span>
            <select value={value.pageSize} onChange={(event) => onChange({ ...value, pageSize: event.target.value as PlatformTemplatePageSize })} className="field-input">
              <option value="A4">A4</option>
              <option value="Letter">Letter</option>
              <option value="Receipt">Receipt</option>
            </select>
          </label>
          <label className="space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Orientation</span>
            <select value={value.orientation} onChange={(event) => onChange({ ...value, orientation: event.target.value as PlatformTemplateOrientation })} className="field-input">
              <option value="Portrait">Portrait</option>
              <option value="Landscape">Landscape</option>
            </select>
          </label>
        </div>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Logo visibility</span><input type="checkbox" checked={value.showLogo} onChange={(event) => onChange({ ...value, showLogo: event.target.checked })} /></label>
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Show tenant branding</span><input type="checkbox" checked={value.showTenantBranding} onChange={(event) => onChange({ ...value, showTenantBranding: event.target.checked })} /></label>
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Show Powered by Ecosys</span><input type="checkbox" checked={value.showPoweredByEcosys} onChange={(event) => onChange({ ...value, showPoweredByEcosys: event.target.checked })} /></label>
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Template active</span><input type="checkbox" checked={value.isActive} onChange={(event) => onChange({ ...value, isActive: event.target.checked })} /></label>
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3 md:col-span-2 xl:col-span-1"><span className="text-sm text-app">Set as default</span><input type="checkbox" checked={value.isDefault} onChange={(event) => onChange({ ...value, isDefault: event.target.checked })} /></label>
        </div>

        <article className="panel-subtle rounded-2xl p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">Available Placeholders</p>
          <div className="mt-3 flex flex-wrap gap-2">
            {placeholderList.map((placeholder) => <code key={placeholder} className="rounded-lg bg-black/20 px-2 py-1 text-xs">{placeholder}</code>)}
          </div>
        </article>

        <div className="flex justify-end gap-3">
          <button type="button" className="button-secondary" onClick={onClose} disabled={saving}>Cancel</button>
          <button data-testid="template-save-button" type="button" className="button-primary" onClick={onSave} disabled={saving}>{saving ? 'Saving...' : 'Save Template'}</button>
        </div>
      </div>
    </Modal>
  )
}
