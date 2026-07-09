import { Plus } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useUnsavedChangesGuard } from '../../hooks/useUnsavedChangesGuard'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { branchService } from '../../services/branchService'
import { licenseService } from '../../services/licenseService'
import { settingsService } from '../../services/settingsService'
import { technicianService } from '../../services/technicianService'
import type {
  LicenseUsageSnapshot,
  TenantLicenseSnapshot,
  TenantOperationsPolicySettings,
  TenantSecuritySettings,
  UpsertAssignmentGroupInput,
} from '../../types/api'

export function LiveAssignmentGroupsSettingsPage() {
  const { pushToast } = useToast()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<UpsertAssignmentGroupInput>({ branchId: null, name: '', description: '', isActive: true, technicianIds: [] })
  const { data, loading, error, reload } = useAsyncData(
    async (signal) => {
      const [groups, branches, technicians] = await Promise.all([
        settingsService.listAssignmentGroups(signal),
        branchService.list(signal),
        technicianService.list(undefined, signal),
      ])
      return { groups, branches, technicians }
    },
    { groups: [], branches: [], technicians: [] },
    [],
  )

  function openEditor(groupId?: string) {
    const group = data.groups.find((item) => item.id === groupId)
    setEditingId(group?.id ?? null)
    setForm({
      branchId: group?.branchId ?? null,
      name: group?.name ?? '',
      description: group?.description ?? '',
      isActive: group?.isActive ?? true,
      technicianIds: group?.technicianIds ?? [],
    })
    setDrawerOpen(true)
  }

  return (
    <SettingsCard
      title="Assignment Groups"
      description="Route work orders to specialty groups and manage technician membership."
      loading={loading}
      error={error}
      onRefresh={reload}
      actions={<button type="button" className="button-primary" onClick={() => openEditor()}><Plus className="h-4 w-4" />Add group</button>}
    >
      <DataTable
        rows={data.groups}
        rowKey={(row) => row.id}
        pageSize={10}
        emptyTitle="No assignment groups yet"
        emptyDescription="Create the first routing group for dispatch."
        columns={[
          { key: 'name', header: 'Group', cell: (row) => <div><p className="font-semibold text-app">{row.name}</p><p className="mt-1 text-xs text-muted">{row.branchName || 'All branches'}</p></div> },
          { key: 'members', header: 'Technicians', cell: (row) => <span>{row.technicianIds.length}</span> },
          { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'danger'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
          { key: 'actions', header: 'Actions', cell: (row) => <button type="button" className="button-secondary px-3 py-2" onClick={() => openEditor(row.id)}>Edit</button> },
        ]}
      />
      <Drawer open={drawerOpen} title={editingId ? 'Edit assignment group' : 'Add assignment group'} description="Group membership is saved through the live settings API." onClose={() => setDrawerOpen(false)}>
        <div className="space-y-4">
          <Field label="Name"><input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} className="field-input" /></Field>
          <Field label="Description"><textarea value={form.description || ''} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[100px]" /></Field>
          <Field label="Branch">
            <select value={form.branchId || ''} onChange={(event) => setForm((current) => ({ ...current, branchId: event.target.value || null }))} className="field-input">
              <option value="">All branches</option>
              {data.branches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          </Field>
          <Field label="Technicians">
            <div className="grid gap-2">
              {data.technicians.map((technician) => (
                <label key={technician.id} className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                  <span className="text-sm text-app">{technician.fullName}</span>
                  <input
                    type="checkbox"
                    checked={form.technicianIds.includes(technician.id)}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        technicianIds: event.target.checked
                          ? [...current.technicianIds, technician.id]
                          : current.technicianIds.filter((id) => id !== technician.id),
                      }))}
                  />
                </label>
              ))}
            </div>
          </Field>
          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => setDrawerOpen(false)}>Cancel</button>
            <button
              type="button"
              className="button-primary"
              onClick={async () => {
                try {
                  if (editingId) {
                    await settingsService.updateAssignmentGroup(editingId, form)
                  } else {
                    await settingsService.createAssignmentGroup(form)
                  }
                  pushToast({ title: 'Assignment group saved', description: 'The routing group has been updated.', tone: 'success' })
                  setDrawerOpen(false)
                  await reload()
                } catch (nextError) {
                  pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save assignment group.', tone: 'danger' })
                }
              }}
            >
              Save group
            </button>
          </div>
        </div>
      </Drawer>
    </SettingsCard>
  )
}

export function LiveSecuritySettingsPage() {
  return <PolicyPage title="Security" description="Password rules and session controls." getData={settingsService.getSecurity} saveData={settingsService.updateSecurity} />
}

export function LiveOperationsPolicySettingsPage() {
  const { pushToast } = useToast()
  const defaultPolicy: TenantOperationsPolicySettings = {
    requireTechnicianSignature: true,
    requireClientSignature: true,
    allowRemoteCompletion: false,
  }
  const { data, loading, error, reload } = useAsyncData<TenantOperationsPolicySettings>(
    (signal) => settingsService.getOperationsPolicy(signal),
    defaultPolicy,
    [],
  )
  const [form, setForm] = useState<TenantOperationsPolicySettings | null>(null)
  const current = form ?? data
  useUnsavedChangesGuard(form !== null)

  const toggles: Array<[keyof TenantOperationsPolicySettings, string, string]> = [
    ['requireTechnicianSignature', 'Require technician signature', 'Technician must sign before the work order can be completed.'],
    ['requireClientSignature', 'Require client signature', 'Client must sign before the work order can be completed.'],
    ['allowRemoteCompletion', 'Allow remote completion', 'Bypasses all signature requirements — useful for remote or unattended jobs.'],
  ]

  return (
    <SettingsCard title="Completion Policy" description="Control signature requirements for work order completion." loading={loading} error={error} onRefresh={reload}>
      <div className="grid gap-2">
        {toggles.map(([key, label, hint]) => (
          <label key={key} className="panel-subtle flex items-start justify-between gap-4 rounded-2xl px-4 py-3">
            <div>
              <p className="text-sm font-semibold text-app">{label}</p>
              <p className="mt-0.5 text-xs text-muted">{hint}</p>
            </div>
            <input
              type="checkbox"
              className="mt-1 shrink-0"
              checked={Boolean(current[key])}
              onChange={(event) => setForm({ ...current, [key]: event.target.checked })}
            />
          </label>
        ))}
      </div>
      {form !== null ? <p className="mt-2 text-xs text-amber-400">You have unsaved changes.</p> : null}
      <div className="mt-4 flex justify-end">
        <SaveButton onClick={async () => {
          try {
            await settingsService.updateOperationsPolicy(current)
            pushToast({ title: 'Completion policy saved', description: 'Settings updated successfully.', tone: 'success' })
            setForm(null)
            await reload()
          } catch (nextError) {
            pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save settings.', tone: 'danger' })
          }
        }} />
      </div>
    </SettingsCard>
  )
}

export function LiveNotificationsSettingsPage() {
  return <SettingsFormPage title="Notifications" description="Notification toggles backed by the tenant settings API." getData={settingsService.getNotifications} saveData={settingsService.updateNotifications} />
}

export function LiveMonitoringSettingsPage() {
  return <SettingsFormPage title="Monitoring Intake" description="Webhook foundation for automated monitoring work-order creation." getData={settingsService.getMonitoring} saveData={settingsService.updateMonitoring} />
}

export function LiveLicenseUsageSettingsPage() {
  const { data, loading, error, reload } = useAsyncData<{ license: TenantLicenseSnapshot | null; usage: LicenseUsageSnapshot | null }>(
    async (signal) => {
      const [license, usage] = await Promise.all([licenseService.getSettingsLicense(signal), licenseService.getTenantUsage(signal)])
      return { license, usage }
    },
    { license: null, usage: null },
    [],
  )

  return (
    <SettingsCard title="License & Usage" description="Read-only current plan, feature flags, and usage counters." loading={loading} error={error} onRefresh={reload}>
      {data.license ? (
        <div className="grid gap-4 md:grid-cols-2">
          <div className="panel-subtle rounded-2xl p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Plan</p>
            <p className="mt-3 text-2xl font-semibold text-app">{data.license.planName}</p>
            <p className="mt-2 text-sm text-muted">{data.license.status}</p>
            {data.license.warningMessage ? <p className="mt-3 text-sm text-amber-200">{data.license.warningMessage}</p> : null}
          </div>
          <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">
            <p>Users: <span className="text-app">{data.usage?.activeUsers ?? 0}</span> / <span className="text-app">{data.license.maxUsers ?? 'Unlimited'}</span></p>
            <p className="mt-2">Seats remaining: <span className="text-app">{data.license.maxUsers == null ? 'Unlimited' : Math.max(data.license.maxUsers - (data.usage?.activeUsers ?? 0), 0)}</span></p>
            <p className="mt-2">Branches: <span className="text-app">{data.usage?.activeBranches ?? 0}</span> / <span className="text-app">{data.license.maxBranches ?? 'Unlimited'}</span></p>
            <p className="mt-2">Assets: <span className="text-app">{data.usage?.activeAssets ?? 0}</span> / <span className="text-app">{data.license.maxAssets ?? 'Unlimited'}</span></p>
            <p className="mt-2">Monthly WOs: <span className="text-app">{data.usage?.monthlyWorkOrders ?? 0}</span> / <span className="text-app">{data.license.monthlyWorkOrders ?? 'Unlimited'}</span></p>
          </div>
        </div>
      ) : null}
    </SettingsCard>
  )
}

function PolicyPage({
  title,
  description,
  getData,
  saveData,
}: {
  title: string
  description: string
  getData: (signal?: AbortSignal) => Promise<TenantSecuritySettings>
  saveData: (input: TenantSecuritySettings) => Promise<TenantSecuritySettings>
}) {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData<TenantSecuritySettings>(
    (signal) => getData(signal),
    { minPasswordLength: 8, requireUppercase: true, requireLowercase: true, requireDigit: true, requireSpecialCharacter: false, passwordRotationDays: 90, sessionTimeoutMinutes: 60, requireMfa: false },
    [],
  )
  const [form, setForm] = useState<TenantSecuritySettings | null>(null)
  const current = form ?? data
  useUnsavedChangesGuard(form !== null)

  return (
    <SettingsCard title={title} description={description} loading={loading} error={error} onRefresh={reload}>
      <div className="grid gap-4 md:grid-cols-3">
        <Field label="Min length"><input type="number" value={current.minPasswordLength} onChange={(event) => setForm({ ...current, minPasswordLength: Number(event.target.value) || 8 })} className="field-input" /></Field>
        <Field label="Rotation days"><input type="number" value={current.passwordRotationDays} onChange={(event) => setForm({ ...current, passwordRotationDays: Number(event.target.value) || 90 })} className="field-input" /></Field>
        <Field label="Session timeout"><input type="number" value={current.sessionTimeoutMinutes} onChange={(event) => setForm({ ...current, sessionTimeoutMinutes: Number(event.target.value) || 60 })} className="field-input" /></Field>
      </div>
      <div className="mt-4 grid gap-2">
        {[
          ['requireUppercase', 'Require uppercase'],
          ['requireLowercase', 'Require lowercase'],
          ['requireDigit', 'Require digit'],
          ['requireSpecialCharacter', 'Require special character'],
          ['requireMfa', 'Require MFA'],
        ].map(([key, label]) => (
          <label key={key} className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">{label}</span>
            <input type="checkbox" checked={Boolean(current[key as keyof TenantSecuritySettings])} onChange={(event) => setForm({ ...current, [key]: event.target.checked })} />
          </label>
        ))}
      </div>
      {form !== null ? <p className="mt-2 text-xs text-amber-400">You have unsaved changes.</p> : null}
      <div className="mt-4 flex justify-end">
        <SaveButton onClick={async () => {
          try {
            await saveData(current)
            pushToast({ title: `${title} saved`, description: 'Settings updated successfully.', tone: 'success' })
            setForm(null)
            await reload()
          } catch (nextError) {
            pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save settings.', tone: 'danger' })
          }
        }} />
      </div>
    </SettingsCard>
  )
}

function SettingsFormPage<T extends object>({
  title,
  description,
  getData,
  saveData,
}: {
  title: string
  description: string
  getData: (signal?: AbortSignal) => Promise<T>
  saveData: (input: T) => Promise<T>
}) {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData<T>(getData, {} as T, [])
  const [form, setForm] = useState<T | null>(null)
  const current = form ?? data
  useUnsavedChangesGuard(form !== null)
  const objectView = current as Record<string, unknown>
  const entries = Object.entries(objectView).filter(([key]) => key !== 'id' && key !== 'tenantId')

  return (
    <SettingsCard title={title} description={description} loading={loading} error={error} onRefresh={reload}>
      <div className="grid gap-4 md:grid-cols-2">
        {entries.map(([key, value]) => (
          <Field key={key} label={key}>
                    {typeof value === 'boolean' ? (
              <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                <span className="text-sm text-app">{key}</span>
                <input type="checkbox" checked={value} onChange={(event) => setForm({ ...(current as object), [key]: event.target.checked } as T)} />
              </label>
            ) : (
              <input value={String(value ?? '')} onChange={(event) => setForm({ ...(current as object), [key]: event.target.value } as T)} className="field-input" />
            )}
          </Field>
        ))}
      </div>
      {form !== null ? <p className="mt-2 text-xs text-amber-400">You have unsaved changes.</p> : null}
      <div className="mt-4 flex justify-end">
        <SaveButton onClick={async () => {
          try {
            await saveData(current)
            pushToast({ title: `${title} saved`, description: 'Settings updated successfully.', tone: 'success' })
            setForm(null)
            await reload()
          } catch (nextError) {
            pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save settings.', tone: 'danger' })
          }
        }} />
      </div>
    </SettingsCard>
  )
}

function SettingsCard({
  title,
  description,
  loading,
  error,
  onRefresh,
  actions,
  children,
}: {
  title: string
  description: string
  loading: boolean
  error: string | null
  onRefresh: () => void | Promise<void>
  actions?: ReactNode
  children: ReactNode
}) {
  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Settings" title={title} description={description} actions={actions} />
      <section className="surface-card">
        {loading ? <LoadingState label={`Loading ${title.toLowerCase()}`} /> : null}
        {!loading && error ? <ErrorState title={`Unable to load ${title.toLowerCase()}`} description={error} /> : null}
        {!loading && !error ? children : null}
        {!loading && !error ? <div className="mt-4 flex justify-end"><button type="button" className="button-secondary" onClick={() => void onRefresh()}>Refresh</button></div> : null}
      </section>
    </div>
  )
}

function SaveButton({ onClick }: { onClick: () => void }) {
  return <button type="button" className="button-primary" onClick={onClick}>Save</button>
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium capitalize text-app">{label.replace(/([A-Z])/g, ' $1')}</span>
      {children}
    </label>
  )
}
