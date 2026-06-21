import { useState } from 'react'
import { useAuth } from '../../auth/AuthContext'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { RowActionSelector } from '../../components/ui/RowActionSelector'
import { useToast } from '../../components/ui/ToastProvider'
import { SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import type { RateCardRecord } from '../../types/api'
import { RateCardDrawer } from './RateCardDrawer'
import { RateCardBulkImportModal } from './RateCardBulkImportModal'

const CATEGORY_ORDER = ['Labour', 'Callout', 'Service', 'Transport', 'Material', 'Other']

function categoryTone(cat: string): 'info' | 'success' | 'warning' | 'neutral' | 'default' {
  switch (cat) {
    case 'Labour': return 'info'
    case 'Callout': return 'warning'
    case 'Service': return 'success'
    default: return 'neutral'
  }
}

function statusTone(active: boolean) {
  return active ? ('success' as const) : ('neutral' as const)
}

export function TenantRateCardsPage() {
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canManage = Boolean(permissions?.canManageFinance || permissions?.canManageFinanceRates)

  const [drawerOpen, setDrawerOpen] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [editing, setEditing] = useState<RateCardRecord | null>(null)

  const { data, loading, error, reload } = useAsyncData<RateCardRecord[]>(
    (signal) => tenantFinanceService.getRateCards(signal),
    [],
    [],
  )

  function openCreate() {
    setEditing(null)
    setDrawerOpen(true)
  }

  function openEdit(card: RateCardRecord) {
    setEditing(card)
    setDrawerOpen(true)
  }

  async function deactivate(card: RateCardRecord) {
    try {
      await tenantFinanceService.deactivateRateCard(card.id)
      pushToast({ title: 'Rate card deactivated', description: `"${card.name}" has been deactivated.`, tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Deactivation failed', description: err instanceof Error ? err.message : 'Unable to deactivate rate card.', tone: 'danger' })
    }
  }

  const sorted = [...data].sort((a, b) => {
    const ca = CATEGORY_ORDER.indexOf(a.category)
    const cb = CATEGORY_ORDER.indexOf(b.category)
    if (ca !== cb) return ca - cb
    return a.name.localeCompare(b.name)
  })

  return (
    <>
      <SectionCard
        title="Rate cards"
        description="Unit prices for labour, callout, service, and material categories. Used when auto-populating billable items on work orders."
      >
        <div className="mb-4 flex flex-wrap justify-end gap-2">
          {canManage ? (
            <>
              <button type="button" className="button-secondary" onClick={() => setImportOpen(true)}>
                Import CSV
              </button>
              <button type="button" className="button-primary" onClick={openCreate}>
                + Add rate
              </button>
            </>
          ) : null}
        </div>

        {loading ? <LoadingState label="Loading rate cards" /> : null}
        {!loading && error ? <ErrorState title="Unable to load rate cards" description={error} /> : null}
        {!loading && !error && sorted.length === 0 ? (
          <EmptyState
            title="No rate cards yet"
            description="Create rate cards to define standard prices for labour, callout fees, and other billable items."
          />
        ) : null}

        {!loading && !error && sorted.length > 0 ? (
          <DataTable
            rows={sorted}
            rowKey={(row) => row.id}
            minTableWidth="min-w-[900px] w-full"
            columns={[
              {
                key: 'name',
                header: 'Name',
                cell: (row) => (
                  <div>
                    <p className="font-semibold text-app">{row.name}</p>
                    {row.description ? <p className="mt-0.5 text-xs text-muted">{row.description}</p> : null}
                  </div>
                ),
              },
              {
                key: 'category',
                header: 'Category',
                cell: (row) => <Badge tone={categoryTone(row.category)}>{row.category}</Badge>,
              },
              {
                key: 'timeType',
                header: 'Time Type',
                cell: (row) => row.timeType,
              },
              {
                key: 'skillLevel',
                header: 'Skill Level',
                cell: (row) => row.skillLevel ?? <span className="text-muted">Any</span>,
              },
              {
                key: 'unitRate',
                header: 'Unit Rate',
                cell: (row) => (
                  <span className="font-mono text-sm font-semibold text-app">
                    KES {row.unitRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    <span className="ml-1 font-normal text-muted">/ {row.unit}</span>
                  </span>
                ),
              },
              {
                key: 'tax',
                header: 'Tax',
                cell: (row) =>
                  row.taxRate != null
                    ? `${row.taxRate}%`
                    : <span className="text-muted">Default</span>,
              },
              {
                key: 'status',
                header: 'Status',
                cell: (row) => (
                  <div className="flex items-center gap-2">
                    <Badge tone={statusTone(row.isActive)}>{row.isActive ? 'Active' : 'Inactive'}</Badge>
                    {row.isDefault ? <Badge tone="info">Default</Badge> : null}
                  </div>
                ),
              },
              {
                key: 'actions',
                header: 'Actions',
                className: 'min-w-[160px]',
                cell: (row) => (
                  <RowActionSelector
                    options={[
                      { value: 'edit', label: 'Edit', disabled: !canManage },
                      { value: 'deactivate', label: 'Deactivate', disabled: !canManage || !row.isActive },
                    ]}
                    onApply={async (action) => {
                      if (action === 'edit') openEdit(row)
                      if (action === 'deactivate') await deactivate(row)
                    }}
                  />
                ),
              },
            ]}
          />
        ) : null}
      </SectionCard>

      <RateCardBulkImportModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        onSuccess={async () => { setImportOpen(false); await reload() }}
      />
      <RateCardDrawer
        open={drawerOpen}
        editing={editing}
        onClose={() => setDrawerOpen(false)}
        onSaved={reload}
      />
    </>
  )
}
