import { useEffect, useState } from 'react'
import { InfoAlert } from '../../../../components/ui/InfoAlert'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useToast } from '../../../../components/ui/ToastProvider'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { platformSettingsService, type PlatformTaxFinanceSettings } from '../../../../services/platformSettingsService'
import { toServiceError } from '../../../../services/platformService'
import { Field, SectionTitle } from '../../../../modules/platform-v2/PlatformCommon'

export function TaxFinanceSettingsPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(() => platformSettingsService.getTaxFinance(), {
    defaultVatRate: 16,
    enableVat: true,
    taxName: 'VAT',
    defaultPaymentTerms: '30 days',
    defaultInvoiceDueDays: 30,
    defaultQuotationValidityDays: 14,
    defaultExpenseApprovalRequired: true,
    defaultCurrency: 'KES',
    invoiceNotes: '',
    quotationTermsAndConditions: '',
  }, [])
  const [form, setForm] = useState<PlatformTaxFinanceSettings>(data)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setForm(data)
  }, [data])

  async function save() {
    setSaving(true)
    try {
      await platformSettingsService.updateTaxFinance(form)
      pushToast({ title: 'Tax & finance saved', description: 'Tax and finance defaults were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save tax and finance settings.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <LoadingState label="Loading tax and finance settings" />
  if (error) return <InfoAlert title="Unable to load tax and finance settings" description={error} tone="danger" />

  return (
    <section className="surface-card space-y-4">
      <SectionTitle title="Tax & Finance Settings" description="VAT, payment terms, due rules, and default finance text." />
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="Default VAT Rate"><input type="number" min={0} value={form.defaultVatRate} onChange={(event) => setForm((current) => ({ ...current, defaultVatRate: Number(event.target.value) || 0 }))} className="field-input" /></Field>
        <Field label="Tax Name"><input value={form.taxName} onChange={(event) => setForm((current) => ({ ...current, taxName: event.target.value }))} className="field-input" /></Field>
        <Field label="Default Payment Terms"><input value={form.defaultPaymentTerms} onChange={(event) => setForm((current) => ({ ...current, defaultPaymentTerms: event.target.value }))} className="field-input" /></Field>
        <Field label="Default Currency"><input value={form.defaultCurrency} onChange={(event) => setForm((current) => ({ ...current, defaultCurrency: event.target.value.toUpperCase() }))} className="field-input" /></Field>
        <Field label="Default Invoice Due Days"><input type="number" min={1} value={form.defaultInvoiceDueDays} onChange={(event) => setForm((current) => ({ ...current, defaultInvoiceDueDays: Number(event.target.value) || 1 }))} className="field-input" /></Field>
        <Field label="Default Quotation Validity Days"><input type="number" min={1} value={form.defaultQuotationValidityDays} onChange={(event) => setForm((current) => ({ ...current, defaultQuotationValidityDays: Number(event.target.value) || 1 }))} className="field-input" /></Field>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Enable VAT</span><input type="checkbox" checked={form.enableVat} onChange={(event) => setForm((current) => ({ ...current, enableVat: event.target.checked }))} /></label>
        <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Default expense approval required</span><input type="checkbox" checked={form.defaultExpenseApprovalRequired} onChange={(event) => setForm((current) => ({ ...current, defaultExpenseApprovalRequired: event.target.checked }))} /></label>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="Invoice Notes"><textarea value={form.invoiceNotes} onChange={(event) => setForm((current) => ({ ...current, invoiceNotes: event.target.value }))} className="field-input min-h-[120px]" /></Field>
        <Field label="Quotation Terms & Conditions"><textarea value={form.quotationTermsAndConditions} onChange={(event) => setForm((current) => ({ ...current, quotationTermsAndConditions: event.target.value }))} className="field-input min-h-[120px]" /></Field>
      </div>
      <div className="flex justify-end">
        <button type="button" className="button-primary" onClick={() => void save()} disabled={saving}>{saving ? 'Saving...' : 'Save Tax & Finance Settings'}</button>
      </div>
    </section>
  )
}
