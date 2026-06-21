import { useEffect, useState } from 'react'
import { DataTable } from '../../../../components/ui/DataTable'
import { InfoAlert } from '../../../../components/ui/InfoAlert'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useToast } from '../../../../components/ui/ToastProvider'
import { SectionCard, StickyActionFooter } from '../../../../components/ui/Workspace'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { platformSettingsService, type PlatformNumberingRule } from '../../../../services/platformSettingsService'
import { toServiceError } from '../../../../services/platformService'
import { SectionTitle } from '../../../../modules/platform-v2/PlatformCommon'

const order: Record<string, number> = {
  Quotation: 1,
  Invoice: 2,
  Receipt: 3,
  WorkOrder: 4,
  PurchaseOrder: 5,
  Expense: 6,
  CreditNote: 7,
  TenantCode: 8,
  AssetCode: 9,
}

function buildPreview(rule: PlatformNumberingRule) {
  const prefix = rule.prefix || ''
  const width = Math.max(1, Number(rule.paddingLength) || 1)
  const next = String(Math.max(1, Number(rule.nextNumber) || 1)).padStart(width, '0')
  return `${prefix}${next}`
}

export function NumberingSettingsPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(() => platformSettingsService.getNumberingRules(), [] as PlatformNumberingRule[], [])
  const [rules, setRules] = useState<PlatformNumberingRule[]>([])
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setRules([...data].sort((a, b) => (order[a.documentType] || 99) - (order[b.documentType] || 99)))
  }, [data])

  async function save() {
    setSaving(true)
    try {
      const response = await platformSettingsService.updateNumberingRules(rules)
      setRules(response)
      pushToast({ title: 'Numbering saved', description: 'Numbering rules were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save numbering rules.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <LoadingState label="Loading numbering settings" />
  if (error) return <InfoAlert title="Unable to load numbering settings" description={error} tone="danger" />

  return (
    <SectionCard title="Numbering Settings" description="Configure prefixes, sequence, padding, and reset frequency for all platform documents.">
      <SectionTitle title="Numbering Settings" description="Configure prefixes, sequence, padding, and reset frequency for all platform documents." />
      <DataTable
        rows={rules}
        rowKey={(row) => row.id}
        minTableWidth="min-w-[1120px] w-full"
        columns={[
          { key: 'documentType', header: 'Document Type', cell: (row) => row.documentType },
          { key: 'prefix', header: 'Prefix', cell: (row) => <input value={row.prefix} onChange={(event) => setRules((current) => current.map((item) => (item.id === row.id ? { ...item, prefix: event.target.value.toUpperCase() } : item)))} className="field-input h-10 min-w-[100px]" /> },
          { key: 'nextNumber', header: 'Next Number', cell: (row) => <input type="number" min={1} value={row.nextNumber} onChange={(event) => setRules((current) => current.map((item) => (item.id === row.id ? { ...item, nextNumber: Number(event.target.value) || 1 } : item)))} className="field-input h-10 min-w-[120px]" /> },
          { key: 'paddingLength', header: 'Padding', cell: (row) => <input type="number" min={3} max={12} value={row.paddingLength} onChange={(event) => setRules((current) => current.map((item) => (item.id === row.id ? { ...item, paddingLength: Number(event.target.value) || 6 } : item)))} className="field-input h-10 min-w-[110px]" /> },
          {
            key: 'resetFrequency',
            header: 'Reset Frequency',
            cell: (row) => (
              <select value={row.resetFrequency} onChange={(event) => setRules((current) => current.map((item) => (item.id === row.id ? { ...item, resetFrequency: event.target.value as PlatformNumberingRule['resetFrequency'] } : item)))} className="field-input h-10 min-w-[140px]">
                <option value="Never">Never</option>
                <option value="Monthly">Monthly</option>
                <option value="Yearly">Yearly</option>
              </select>
            ),
          },
          {
            key: 'preview',
            header: 'Preview',
            cell: (row) => {
              const typeKey = row.documentType.toLowerCase().replace(/[^a-z0-9]+/g, '-')
              return (
                <span data-testid={`numbering-preview-${typeKey}`} className="font-mono text-xs">
                  {buildPreview(row)}
                </span>
              )
            },
          },
        ]}
      />
      <StickyActionFooter>
        <button type="button" className="button-primary" onClick={() => void save()} disabled={saving}>{saving ? 'Saving...' : 'Save Numbering Settings'}</button>
      </StickyActionFooter>
    </SectionCard>
  )
}
