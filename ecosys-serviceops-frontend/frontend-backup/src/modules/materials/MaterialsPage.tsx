import { Download, Plus, Search, X } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { useAuth } from '../../auth/AuthContext'
import { useShellContext } from '../../components/layout/AppShell'
import { BulkImportModal } from '../../components/ui/BulkImportModal'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { RowActionSelector } from '../../components/ui/RowActionSelector'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { importService } from '../../services/importService'
import { materialIssueService } from '../../services/materialIssueService'
import { materialService } from '../../services/materialService'
import { userService } from '../../services/userService'
import { workOrderService } from '../../services/workOrderService'
import type { CreateMaterialIssueInput, MaterialIssueRecord, MaterialItem, UpsertMaterialInput, UserRecord, WorkOrder } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import { PageScaffold, SearchToolbar, SectionCard } from '../../components/ui/Workspace'

const emptyForm = (selectedBranchId: string): UpsertMaterialInput => ({
  itemCode: '',
  itemName: '',
  category: '',
  unitOfMeasure: 'pcs',
  quantityOnHand: 0,
  reorderLevel: 0,
  unitCost: null,
  branchId: selectedBranchId === 'all' ? null : selectedBranchId,
})

export function MaterialsPage() {
  const { selectedBranchId } = useShellContext()
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canViewMaterials = Boolean(
    permissions?.canViewMaterials
    || permissions?.canRequestMaterials
    || permissions?.canManageMaterials
    || permissions?.canApproveMaterials
    || permissions?.canIssueMaterials,
  )
  const canRequestMaterials = Boolean(
    permissions?.canRequestMaterials
    || permissions?.canManageMaterials
    || permissions?.canIssueMaterials,
  )
  const canManageMaterials = Boolean(permissions?.canManageMaterials)
  const canUpdateInventory = Boolean(permissions?.canManageMaterials || permissions?.canIssueMaterials)
  const canApproveMaterials = Boolean(permissions?.canApproveMaterials)
  const canIssueMaterials = Boolean(permissions?.canIssueMaterials)
  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<'active' | 'inactive' | 'all'>('active')
  const [editorOpen, setEditorOpen] = useState(false)
  const [movementOpen, setMovementOpen] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [editing, setEditing] = useState<MaterialItem | null>(null)
  const [movementMode, setMovementMode] = useState<'in' | 'out'>('in')
  const [movementTarget, setMovementTarget] = useState<MaterialItem | null>(null)
  const [movement, setMovement] = useState({ quantity: 1, reason: '', unitCost: '' })
  const [form, setForm] = useState<UpsertMaterialInput>(emptyForm(selectedBranchId))
  const [saveError, setSaveError] = useState('')
  const [saving, setSaving] = useState(false)
  const [movementError, setMovementError] = useState('')
  const [moving, setMoving] = useState(false)
  const [issueOpen, setIssueOpen] = useState(false)
  const [issueTarget, setIssueTarget] = useState<MaterialItem | null>(null)
  const [issueForm, setIssueForm] = useState<CreateMaterialIssueInput>({
    branchId: selectedBranchId === 'all' ? null : selectedBranchId,
    workOrderId: null,
    assetId: null,
    clientId: null,
    siteId: null,
    issuedToUserId: null,
    notes: '',
    lines: [],
  })
  const [issuing, setIssuing] = useState(false)
  const [issueError, setIssueError] = useState('')
  const [returnTarget, setReturnTarget] = useState<MaterialIssueRecord | null>(null)
  const [returnQuantity, setReturnQuantity] = useState(0)

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedSearch(searchInput.trim())
    }, 300)
    return () => window.clearTimeout(handle)
  }, [searchInput])

  const { data, loading, error, reload } = useAsyncData(
    async (signal) => {
      const [materials, issues, workOrders, users] = await Promise.all([
        materialService.list({ branchId: selectedBranchId, search: debouncedSearch || undefined, status: statusFilter, signal }),
        materialIssueService.list(selectedBranchId, signal),
        canRequestMaterials ? workOrderService.list(selectedBranchId, signal).catch(() => [] as WorkOrder[]) : Promise.resolve([] as WorkOrder[]),
        canRequestMaterials ? userService.list(signal).catch(() => [] as UserRecord[]) : Promise.resolve([] as UserRecord[]),
      ])
      return { materials, issues, workOrders, users }
    },
    { materials: [] as MaterialItem[], issues: [] as MaterialIssueRecord[], workOrders: [] as WorkOrder[], users: [] as UserRecord[] },
    [canRequestMaterials, debouncedSearch, selectedBranchId, statusFilter],
  )

  const hasFilters = Boolean(debouncedSearch) || statusFilter !== 'active'
  const totalMaterials = data.materials.length
  const lowStockCount = data.materials.filter((item) => item.isLowStock).length
  const activeMaterialsCount = data.materials.filter((item) => item.isActive).length
  const issueCount = data.issues.length

  if (!canViewMaterials) {
    return <ErrorState title="Permission required" description="Your account does not currently have access to the Materials workspace." />
  }

  function materialRowActions(row?: MaterialItem) {
    const actions: Array<{ value: string; label: string }> = []
    if (canManageMaterials) {
      actions.push({ value: 'edit', label: 'Edit' })
      if (row?.isActive ?? true) {
        actions.push({ value: 'deactivate', label: 'Disable' })
      }
    }
    if (canRequestMaterials) {
      actions.push({ value: 'issue', label: 'Issue Material' })
    }
    if (canUpdateInventory) {
      actions.push({ value: 'stockin', label: 'Stock-in' })
      actions.push({ value: 'stockout', label: 'Stock-out' })
    }
    return actions
  }

  async function deactivateMaterial(item: MaterialItem) {
    const confirmed = window.confirm(`Disable material "${item.itemName}"?`)
    if (!confirmed) return

    try {
      await materialService.remove(item.id)
      pushToast({ title: 'Material disabled', description: 'The material item was removed from active stock use.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Disable failed', description: nextError instanceof Error ? nextError.message : 'Unable to disable the material item.', tone: 'danger' })
    }
  }

  function issueHistoryActions(row: MaterialIssueRecord) {
    const actions: Array<{ value: string; label: string; disabled?: boolean }> = []
    if (canApproveMaterials) {
      actions.push({ value: 'approve', label: 'Approve', disabled: row.issueStatus !== 'pending_approval' })
    }
    if (canIssueMaterials) {
      actions.push({ value: 'issue', label: 'Issue', disabled: !['approved', 'draft'].includes(row.issueStatus) })
    }
    if (canRequestMaterials) {
      actions.push({ value: 'return', label: 'Return', disabled: row.issueStatus !== 'issued' })
      actions.push({ value: 'cancel', label: 'Cancel', disabled: !['draft', 'pending_approval'].includes(row.issueStatus) })
    }
    return actions
  }

  function openEditor(item?: MaterialItem) {
    setEditing(item ?? null)
    setSaveError('')
    setForm({
      itemCode: item?.itemCode ?? '',
      itemName: item?.itemName ?? '',
      category: item?.category ?? '',
      unitOfMeasure: item?.unitOfMeasure ?? 'pcs',
      quantityOnHand: item?.quantityOnHand ?? 0,
      reorderLevel: item?.reorderLevel ?? 0,
      unitCost: item?.unitCost ?? null,
      branchId: item?.branchId ?? (selectedBranchId === 'all' ? null : selectedBranchId),
    })
    setEditorOpen(true)
  }

  function openMovement(mode: 'in' | 'out', item: MaterialItem) {
    setMovementMode(mode)
    setMovementTarget(item)
    setMovement({ quantity: 1, reason: '', unitCost: '' })
    setMovementError('')
    setMovementOpen(true)
  }

  function openIssue(item?: MaterialItem) {
    const target = item ?? null
    setIssueTarget(target)
    setIssueForm({
      branchId: target?.branchId ?? (selectedBranchId === 'all' ? null : selectedBranchId),
      workOrderId: null,
      assetId: null,
      clientId: null,
      siteId: null,
      issuedToUserId: null,
      notes: '',
      lines: target ? [{
        materialItemId: target.id,
        quantityRequested: 1,
        quantityIssued: 1,
        notes: '',
      }] : [],
    })
    setIssueError('')
    setIssueOpen(true)
  }

  async function submitIssue() {
    if (issuing) return

    setIssuing(true)
    setIssueError('')

    try {
      await materialIssueService.create(issueForm)
      pushToast({ title: 'Material issue created', description: 'The issue workflow has been recorded and any approval rules have been applied.', tone: 'success' })
      setIssueOpen(false)
      setIssueTarget(null)
      await reload()
    } catch (nextError) {
      setIssueError(nextError instanceof Error ? nextError.message : 'Unable to create the material issue.')
    } finally {
      setIssuing(false)
    }
  }

  async function applyIssueAction(issue: MaterialIssueRecord, action: string) {
    try {
      if (action === 'approve') await materialIssueService.approve(issue.id)
      if (action === 'issue') await materialIssueService.issue(issue.id)
      if (action === 'cancel') {
        const confirmed = window.confirm(`Cancel material issue ${issue.issueNumber}?`)
        if (!confirmed) return
        await materialIssueService.cancel(issue.id)
      }
      if (action === 'return') {
        setReturnTarget(issue)
        setReturnQuantity(Math.max(issue.lines[0]?.quantityIssued ?? 0, 0))
        return
      }
      pushToast({ title: 'Material issue updated', description: `Action "${action}" completed successfully.`, tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Issue action failed', description: nextError instanceof Error ? nextError.message : 'Unable to complete the selected material issue action.', tone: 'danger' })
    }
  }

  async function submitReturn() {
    if (!returnTarget || returnQuantity <= 0 || returnTarget.lines.length === 0) return

    try {
      await materialIssueService.returnIssue(returnTarget.id, [{ materialIssueLineId: returnTarget.lines[0].id, quantityReturned: returnQuantity }])
      pushToast({ title: 'Material returned', description: 'Returned stock has been added back to inventory.', tone: 'success' })
      setReturnTarget(null)
      setReturnQuantity(0)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Return failed', description: nextError instanceof Error ? nextError.message : 'Unable to return the issued material.', tone: 'danger' })
    }
  }

  async function saveItem() {
    if (saving) return

    setSaving(true)
    setSaveError('')

    try {
      if (editing) {
        await materialService.update(editing.id, form)
        pushToast({ title: 'Material updated', description: 'Material item saved successfully.', tone: 'success' })
      } else {
        await materialService.create(form)
        pushToast({ title: 'Material created', description: 'New material item added successfully.', tone: 'success' })
      }
      setForm(emptyForm(selectedBranchId))
      setEditing(null)
      setEditorOpen(false)
      await reload()
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : 'Unable to save material item.')
    } finally {
      setSaving(false)
    }
  }

  async function submitMovement() {
    if (!movementTarget || moving) return

    setMoving(true)
    setMovementError('')

    try {
      if (movementMode === 'in') {
        await materialService.stockIn(movementTarget.id, {
          branchId: movementTarget.branchId ?? (selectedBranchId === 'all' ? null : selectedBranchId),
          quantity: movement.quantity,
          unitCost: movement.unitCost ? Number(movement.unitCost) : null,
          reason: movement.reason || 'Manual stock-in from frontend',
        })
      } else {
        await materialService.stockOut(movementTarget.id, {
          branchId: movementTarget.branchId ?? (selectedBranchId === 'all' ? null : selectedBranchId),
          quantity: movement.quantity,
          reason: movement.reason || 'Manual stock-out from frontend',
        })
      }
      pushToast({
        title: movementMode === 'in' ? 'Stock increased' : 'Stock reduced',
        description: 'Stock balances were updated successfully.',
        tone: 'success',
      })
      setMovementOpen(false)
      await reload()
    } catch (error) {
      setMovementError(error instanceof Error ? error.message : 'Unable to post stock movement.')
    } finally {
      setMoving(false)
    }
  }

  return (
    <PageScaffold
      eyebrow="Materials"
      title="Materials and stock"
      description="Track stock, replenishment, and movements."
      actions={(
        <>
          <button type="button" className="button-secondary w-full sm:w-auto" onClick={() => materialService.downloadImportTemplate()}>
            <Download className="h-4 w-4" />
            Import template
          </button>
          {canManageMaterials ? (
            <button type="button" className="button-secondary w-full sm:w-auto" onClick={() => setImportOpen(true)}>
              Bulk import
            </button>
          ) : null}
          {canRequestMaterials ? (
            <button type="button" className="button-secondary w-full sm:w-auto" onClick={() => openIssue()}>
              Issue material
            </button>
          ) : null}
          {canManageMaterials ? (
            <button type="button" className="button-primary w-full sm:w-auto" onClick={() => openEditor()}>
              <Plus className="h-4 w-4" />
              Add material
            </button>
          ) : null}
        </>
      )}
    >
      <div className="flex flex-wrap gap-2">
        <SummaryChip label="Materials" value={totalMaterials} />
        <SummaryChip label="Low stock" value={lowStockCount} tone="warning" />
        <SummaryChip label="Active" value={activeMaterialsCount} />
        <SummaryChip label="Issue records" value={issueCount} />
      </div>

      <SectionCard title="Stock register" description="Search materials, then use the row actions for stock movements, issue requests, or edits.">
        <SearchToolbar
          searchSlot={(
            <label className="panel-subtle flex items-center gap-3 rounded-[14px] px-4 py-2.5">
              <Search className="h-4 w-4 text-muted" />
              <input
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
                className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
                placeholder="Search by material, SKU, category, description, or UOM"
              />
            </label>
          )}
          filters={(
            <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} className="field-input">
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
              <option value="all">All</option>
            </select>
          )}
          actions={(
            <button
              type="button"
              className="button-secondary"
              onClick={() => {
                setSearchInput('')
                setDebouncedSearch('')
                setStatusFilter('active')
              }}
              disabled={!hasFilters}
            >
              <X className="h-4 w-4" />
              Clear
            </button>
          )}
        />

        {loading ? <LoadingState label="Loading materials" /> : null}
        {!loading && error ? <ErrorState title="Unable to load materials" description={error} /> : null}
        {!loading && !error ? (
          <DataTable
            rows={data.materials}
            rowKey={(row) => row.id}
            pageSize={10}
            variant="open"
            onRowClick={(row) => openEditor(row)}
            emptyTitle="No materials found"
            emptyDescription={hasFilters ? 'Try clearing search or changing status filters.' : 'Create a material item to start stock tracking.'}
            mobileCard={(row) => (
              <div className="space-y-3 rounded-[20px] bg-[var(--app-subtle)] p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="font-semibold text-app">{row.itemName}</p>
                    <p className="mt-1 text-xs text-muted">{row.itemCode}</p>
                  </div>
                  <Badge tone={row.isLowStock ? 'warning' : 'success'}>{row.isLowStock ? 'Low' : 'Healthy'}</Badge>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <Detail label="Branch" value={row.branchName || 'Global scope'} />
                  <Detail label="On Hand" value={`${row.quantityOnHand} ${row.unitOfMeasure}`} />
                  <Detail label="Reorder Level" value={String(row.reorderLevel)} />
                  <Detail label="Created" value={formatDateOnly(row.createdAt)} />
                </div>
                {materialRowActions(row).length > 0 ? (
                  <RowActionSelector
                    options={materialRowActions(row)}
                    onApply={(action) => {
                      if (action === 'edit') openEditor(row)
                      if (action === 'deactivate') void deactivateMaterial(row)
                      if (action === 'issue') openIssue(row)
                      if (action === 'stockin') openMovement('in', row)
                      if (action === 'stockout') openMovement('out', row)
                    }}
                    buttonLabel="Apply"
                  />
                ) : null}
              </div>
            )}
            columns={[
                {
                  key: 'item',
                  header: 'Item',
                  cell: (row) => (
                    <div>
                      <button type="button" className="font-semibold text-app hover:underline" onClick={() => openEditor(row)}>
                        {row.itemName}
                      </button>
                      <p className="mt-1 text-xs text-muted">{row.itemCode}</p>
                    </div>
                  ),
                },
              { key: 'branch', header: 'Branch', cell: (row) => <span>{row.branchName || 'Global scope'}</span> },
              { key: 'qty', header: 'On Hand', cell: (row) => <span>{row.quantityOnHand} {row.unitOfMeasure}</span> },
              { key: 'reorder', header: 'Reorder Level', cell: (row) => <span>{row.reorderLevel}</span> },
              { key: 'status', header: 'Stock', cell: (row) => <Badge tone={row.isLowStock ? 'warning' : 'success'}>{row.isLowStock ? 'Low' : 'Healthy'}</Badge> },
              { key: 'created', header: 'Created', cell: (row) => <span>{formatDateOnly(row.createdAt)}</span> },
              ...(canManageMaterials || canRequestMaterials || canUpdateInventory
                ? [{
                    key: 'actions',
                    header: 'Actions',
                    className: 'min-w-[240px]',
                    cell: (row: MaterialItem) => (
                      <RowActionSelector
                        options={materialRowActions(row)}
                        onApply={(action) => {
                          if (action === 'edit') openEditor(row)
                          if (action === 'deactivate') void deactivateMaterial(row)
                          if (action === 'issue') openIssue(row)
                          if (action === 'stockin') openMovement('in', row)
                          if (action === 'stockout') openMovement('out', row)
                        }}
                        buttonLabel="Apply"
                      />
                    ),
                  }]
                : []),
            ]}
          />
        ) : null}
      </SectionCard>

      <SectionCard title="Material issue history" description="Issue, approval, and return workflow records linked to materials and work orders.">
        {loading ? <LoadingState label="Loading material issue history" /> : null}
        {!loading && !error ? (
          <DataTable
            rows={data.issues}
            rowKey={(row) => row.id}
            variant="open"
            minTableWidth="min-w-[1160px] w-full"
            emptyTitle="No material issues yet"
            emptyDescription="Issue records will appear here once materials are allocated to work orders or users."
            columns={[
              { key: 'number', header: 'Issue', cell: (row) => <div><p className="font-semibold text-app">{row.issueNumber}</p><p className="mt-1 text-xs text-muted">{formatDateOnly(row.createdAt)}</p></div> },
              { key: 'materials', header: 'Materials', cell: (row) => row.lines.map((line) => `${line.materialItemName || line.itemCode} (${line.quantityIssued || line.quantityRequested} ${line.unit})`).join(', ') },
              { key: 'workOrder', header: 'Work Order', cell: (row) => row.workOrderNumber || 'Not linked' },
              { key: 'issuedTo', header: 'Issued To', cell: (row) => row.issuedToUserName || 'Not assigned' },
              { key: 'stock', header: 'Cost', cell: (row) => row.totalIssuedCost > 0 ? row.totalIssuedCost.toLocaleString() : row.totalEstimatedCost.toLocaleString() },
              { key: 'status', header: 'Status', cell: (row) => renderIssueStatusBadge(row.issueStatus) },
              {
                key: 'actions',
                header: 'Actions',
                className: 'min-w-[250px]',
                cell: (row) =>
                  issueHistoryActions(row).length > 0 ? (
                    <RowActionSelector
                      options={issueHistoryActions(row)}
                      onApply={(action) => applyIssueAction(row, action)}
                      buttonLabel="Apply"
                    />
                  ) : (
                    <span className="text-sm text-muted">No actions</span>
                  ),
              },
            ]}
          />
        ) : null}
      </SectionCard>

      <Modal open={editorOpen} title={editing ? 'Edit material item' : 'Add material item'} description="Add or update a material item for stock tracking." onClose={() => setEditorOpen(false)} maxWidth="max-w-4xl">
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Item code"><input value={form.itemCode} onChange={(event) => setForm((current) => ({ ...current, itemCode: event.target.value }))} className="field-input" /></Field>
          <Field label="Item name"><input value={form.itemName} onChange={(event) => setForm((current) => ({ ...current, itemName: event.target.value }))} className="field-input" /></Field>
          <Field label="Category"><input value={form.category || ''} onChange={(event) => setForm((current) => ({ ...current, category: event.target.value }))} className="field-input" /></Field>
          <Field label="Unit"><input value={form.unitOfMeasure} onChange={(event) => setForm((current) => ({ ...current, unitOfMeasure: event.target.value }))} className="field-input" /></Field>
          <Field label="Opening quantity"><input type="number" value={form.quantityOnHand} onChange={(event) => setForm((current) => ({ ...current, quantityOnHand: Number(event.target.value) || 0 }))} className="field-input" /></Field>
          <Field label="Reorder level"><input type="number" value={form.reorderLevel} onChange={(event) => setForm((current) => ({ ...current, reorderLevel: Number(event.target.value) || 0 }))} className="field-input" /></Field>
          <Field label="Unit cost"><input type="number" value={form.unitCost ?? ''} onChange={(event) => setForm((current) => ({ ...current, unitCost: event.target.value ? Number(event.target.value) : null }))} className="field-input" /></Field>
        </div>
        {saveError ? <div className="mt-4 rounded-[14px] bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{saveError}</div> : null}
        <div className="mt-6 flex justify-end gap-3">
          <button type="button" className="button-secondary" onClick={() => setEditorOpen(false)} disabled={saving}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveItem()} disabled={saving}>{saving ? 'Saving item...' : 'Save item'}</button>
        </div>
      </Modal>

      <Modal open={movementOpen} title={movementMode === 'in' ? 'Stock-in' : 'Stock-out'} description="Record a stock movement for this item." onClose={() => setMovementOpen(false)} maxWidth="max-w-2xl">
        <div className="space-y-3">
          <div className="rounded-[14px] bg-[var(--app-subtle)] px-4 py-4 text-sm text-muted">
            {movementTarget ? (
              <>
                <span className="font-medium text-app">{movementTarget.itemName}</span>
                <span> currently has </span>
                <span className="font-medium text-app">{movementTarget.quantityOnHand} {movementTarget.unitOfMeasure}</span>
                <span> on hand.</span>
              </>
            ) : null}
          </div>
          <Field label="Quantity">
            <input type="number" min={1} value={movement.quantity} onChange={(event) => setMovement((current) => ({ ...current, quantity: Number(event.target.value) || 1 }))} className="field-input" />
          </Field>
          {movementMode === 'in' ? (
            <Field label="Unit cost">
              <input type="number" value={movement.unitCost} onChange={(event) => setMovement((current) => ({ ...current, unitCost: event.target.value }))} className="field-input" />
            </Field>
          ) : null}
          <Field label="Reason">
            <textarea value={movement.reason} onChange={(event) => setMovement((current) => ({ ...current, reason: event.target.value }))} className="field-input min-h-[120px]" />
          </Field>
          {movementError ? <div className="rounded-[14px] bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{movementError}</div> : null}
          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => setMovementOpen(false)} disabled={moving}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void submitMovement()} disabled={moving}>{moving ? 'Posting...' : 'Post movement'}</button>
          </div>
        </div>
      </Modal>

      <Modal open={issueOpen} title="Issue material" description="Allocate stock to a work order or user and let approval rules decide whether it can issue immediately." onClose={() => setIssueOpen(false)} maxWidth="max-w-4xl">
        <div className="space-y-3">
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Material">
              <select
                value={issueForm.lines[0]?.materialItemId || ''}
                onChange={(event) => {
                  const selectedMaterial = data.materials.find((item) => item.id === event.target.value) ?? null
                  setIssueTarget(selectedMaterial)
                  setIssueForm((current) => ({
                    ...current,
                    branchId: selectedMaterial?.branchId ?? current.branchId,
                    lines: [{
                      materialItemId: event.target.value,
                      quantityRequested: current.lines[0]?.quantityRequested || 1,
                      quantityIssued: current.lines[0]?.quantityIssued || 1,
                      notes: current.lines[0]?.notes || '',
                    }],
                  }))
                }}
                className="field-input"
              >
                <option value="">Select material</option>
                {data.materials.map((item) => <option key={item.id} value={item.id}>{item.itemName} ({item.quantityOnHand} {item.unitOfMeasure})</option>)}
              </select>
            </Field>
            <Field label="Stock on hand"><input value={issueTarget ? `${issueTarget.quantityOnHand} ${issueTarget.unitOfMeasure}` : 'Select material'} className="field-input" readOnly /></Field>
            <Field label="Quantity requested"><input type="number" min={1} value={issueForm.lines[0]?.quantityRequested || 1} onChange={(event) => setIssueForm((current) => ({ ...current, lines: [{ ...(current.lines[0] || { materialItemId: issueTarget?.id || '', notes: '' }), quantityRequested: Number(event.target.value) || 1, quantityIssued: current.lines[0]?.quantityIssued || Number(event.target.value) || 1 }] }))} className="field-input" /></Field>
            <Field label="Quantity to issue"><input type="number" min={1} value={issueForm.lines[0]?.quantityIssued || 1} onChange={(event) => setIssueForm((current) => ({ ...current, lines: [{ ...(current.lines[0] || { materialItemId: issueTarget?.id || '', notes: '' }), quantityRequested: current.lines[0]?.quantityRequested || Number(event.target.value) || 1, quantityIssued: Number(event.target.value) || 1 }] }))} className="field-input" /></Field>
            <Field label="Work order">
              <select value={issueForm.workOrderId || ''} onChange={(event) => setIssueForm((current) => ({ ...current, workOrderId: event.target.value || null }))} className="field-input">
                <option value="">No work order</option>
                {data.workOrders.map((workOrder) => <option key={workOrder.id} value={workOrder.id}>{workOrder.workOrderNumber} - {workOrder.title}</option>)}
              </select>
            </Field>
            <Field label="Issued to">
              <select value={issueForm.issuedToUserId || ''} onChange={(event) => setIssueForm((current) => ({ ...current, issuedToUserId: event.target.value || null }))} className="field-input">
                <option value="">Select user</option>
                {data.users.filter((user) => user.isActive).map((user) => <option key={user.id} value={user.id}>{user.fullName}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Notes">
            <textarea value={issueForm.notes || ''} onChange={(event) => setIssueForm((current) => ({ ...current, notes: event.target.value }))} className="field-input min-h-[120px]" />
          </Field>
          {issueError ? <div className="rounded-[14px] bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{issueError}</div> : null}
          <div className="rounded-[14px] bg-[var(--app-subtle)] px-4 py-3 text-sm text-muted">
            {issueTarget ? `Available stock: ${issueTarget.quantityOnHand} ${issueTarget.unitOfMeasure}.` : 'Choose a material to issue.'}
          </div>
          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => setIssueOpen(false)} disabled={issuing}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void submitIssue()} disabled={issuing || !issueForm.lines[0]?.materialItemId}>{issuing ? 'Submitting...' : 'Submit issue'}</button>
          </div>
        </div>
      </Modal>

      <Modal open={Boolean(returnTarget)} title="Return issued material" description="Record returned stock so inventory is restored correctly." onClose={() => setReturnTarget(null)} maxWidth="max-w-2xl">
        <div className="space-y-3">
          <div className="rounded-[14px] bg-[var(--app-subtle)] px-4 py-4 text-sm text-muted">
            {returnTarget ? `${returnTarget.issueNumber} | ${returnTarget.lines[0]?.materialItemName || returnTarget.lines[0]?.itemCode || 'Material'} | Issued ${returnTarget.lines[0]?.quantityIssued || 0} ${returnTarget.lines[0]?.unit || ''}` : 'No issue selected.'}
          </div>
          <Field label="Quantity returned">
            <input type="number" min={1} value={returnQuantity} onChange={(event) => setReturnQuantity(Number(event.target.value) || 0)} className="field-input" />
          </Field>
          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => setReturnTarget(null)}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void submitReturn()} disabled={!returnTarget || returnQuantity <= 0}>Return material</button>
          </div>
        </div>
      </Modal>

      <BulkImportModal
        open={importOpen}
        title="Bulk import materials"
        description="Upload a CSV or Excel file to preview material rows before importing them."
        onClose={() => setImportOpen(false)}
        onPreview={(file) => importService.previewMaterials(file)}
        onCommit={(file) => importService.commitMaterials(file)}
        onCommitted={async (summary) => {
          pushToast({
            title: 'Material import complete',
            description: `Imported ${summary.successfulRows} of ${summary.totalRows} rows.`,
            tone: summary.failedRows > 0 ? 'warning' : 'success',
          })
          await reload()
        }}
      />
    </PageScaffold>
  )
}

function SummaryChip({
  label,
  value,
  tone = 'default',
}: {
  label: string
  value: number
  tone?: 'default' | 'warning' | 'danger'
}) {
  const toneClass =
    tone === 'warning'
      ? 'border-amber-500/20 bg-amber-500/10'
      : tone === 'danger'
        ? 'border-rose-500/20 bg-rose-500/10'
        : 'border-[var(--app-divider)] bg-[var(--app-surface-strong)]'

  return (
    <div className={`inline-flex min-h-[40px] items-center gap-2 rounded-full border px-3 py-1.5 ${toneClass}`}>
      <span className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</span>
      <span className="rounded-full bg-black/5 px-2 py-0.5 text-xs font-semibold tabular-nums text-app">{value}</span>
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-1 break-words text-sm text-app">{value}</p>
    </div>
  )
}

function renderIssueStatusBadge(status: string) {
  const normalized = status.toLowerCase()
  if (normalized === 'issued') return <Badge tone="success">Issued</Badge>
  if (normalized === 'pending_approval' || normalized === 'approved') return <Badge tone="warning">{normalized === 'approved' ? 'Approved' : 'Pending Approval'}</Badge>
  if (normalized === 'rejected' || normalized === 'cancelled') return <Badge tone="danger">{normalized === 'rejected' ? 'Rejected' : 'Cancelled'}</Badge>
  if (normalized === 'returned') return <Badge tone="info">Returned</Badge>
  return <Badge tone="neutral">Draft</Badge>
}
