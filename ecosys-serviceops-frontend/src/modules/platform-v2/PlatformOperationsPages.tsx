import { useMemo, useState } from 'react'
import { Activity, BarChart3, Building2, FileText, RefreshCw, ShieldCheck, Users, Wallet } from 'lucide-react'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { InfoAlert } from '../../components/ui/InfoAlert'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { PageHeader } from '../../components/ui/PageHeader'
import { StatCard } from '../../components/ui/StatCard'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { generateId, platformService, toServiceError } from '../../services/platformService'
import type { AuditLog, PlatformRole, PlatformUser, PlatformUserStatus, ReportSummary, ReportsMeta } from '../../types/platform'
import { formatDateTime } from '../../utils/date'
import { getPlatformRoleDescription, getPlatformRoleLabel, normalizePlatformRole, PLATFORM_ROLE_OPTIONS } from '../../utils/roles'
import { Field, SectionTitle, userStatusBadge } from './PlatformCommon'
import { PlatformSettingsPage as PlatformSettingsModulePage } from '../../pages/platform/settings/PlatformSettingsPage'

const platformRoles: PlatformRole[] = ['PlatformOwner', 'PlatformAdmin', 'SupportAdmin']

export function PlatformUsersPage() {
  const { data, loading, error, reload } = useAsyncData(async () => platformService.platformUsersApi.list(), { data: [] as PlatformUser[], backendAvailable: true, message: '' }, [])
  const { pushToast } = useToast()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<'All' | PlatformUserStatus>('All')
  const [roleFilter, setRoleFilter] = useState<'All' | PlatformRole>('All')
  const [open, setOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState<PlatformUser>({ id: '', fullName: '', email: '', phone: '', role: 'PlatformAdmin', status: 'Active', lastLogin: null })
  const [resendingUserId, setResendingUserId] = useState<string | null>(null)

  const rows = useMemo(() => {
    const query = search.trim().toLowerCase()
    return data.data.filter((item) => {
      const matchesQuery = !query || item.fullName.toLowerCase().includes(query) || item.email.toLowerCase().includes(query)
      const matchesStatus = statusFilter === 'All' || item.status === statusFilter
      const matchesRole = roleFilter === 'All' || item.role === roleFilter
      return matchesQuery && matchesStatus && matchesRole
    })
  }, [data.data, roleFilter, search, statusFilter])

  async function saveUser() {
    if (!form.fullName.trim() || !form.email.trim()) {
      pushToast({ title: 'Missing fields', description: 'Full name and email are required.', tone: 'warning' })
      return
    }

    setSaving(true)
    try {
      const payload: PlatformUser = {
        ...form,
        id: form.id || generateId('pu'),
        lastLogin: form.lastLogin || null,
        createdAt: form.createdAt || new Date().toISOString(),
      }
      const response = await platformService.platformUsersApi.save(payload)
      pushToast({
        title: 'Platform user saved',
        description: response.data.credentialDelivery?.success ? 'User created. An account setup link has been sent to their email.' : `${payload.fullName} was saved${form.id ? '.' : ', but the account setup email failed. Please resend the invitation.'}`,
        tone: response.data.credentialDelivery?.success || form.id ? 'success' : 'warning',
      })
      setOpen(false)
      setForm({ id: '', fullName: '', email: '', phone: '', role: 'PlatformAdmin', status: 'Active', lastLogin: null })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save platform user.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function toggleStatus(user: PlatformUser) {
    const nextStatus: PlatformUserStatus = user.status === 'Active' ? 'Inactive' : 'Active'
    try {
      await platformService.platformUsersApi.updateStatus(user.id, nextStatus)
      pushToast({ title: 'Status updated', description: `${user.fullName} is now ${nextStatus}.`, tone: 'success' })
      await reload()
    } catch (statusError) {
      pushToast({ title: 'Update failed', description: toServiceError(statusError, 'Unable to update status.'), tone: 'danger' })
    }
  }

  async function resetPassword(user: PlatformUser) {
    try {
      const response = await platformService.platformUsersApi.resetPassword(user.id)
      pushToast({
        title: 'Password reset',
        description: response.data.success ? `${user.fullName} was emailed fresh credentials.` : response.data.message || 'Password reset, but credential email failed. Please resend credentials.',
        tone: response.data.success ? 'success' : 'warning',
      })
      await reload()
    } catch (resetError) {
      pushToast({ title: 'Reset failed', description: toServiceError(resetError, 'Unable to reset password.'), tone: 'danger' })
    }
  }

  async function resendCredentials(user: PlatformUser) {
    if (resendingUserId) return

    setResendingUserId(user.id)
    try {
      const response = await platformService.platformUsersApi.resendCredentials(user.id)
      pushToast({
        title: response.data.success ? 'Invitation sent' : 'Invitation email failed',
        description: response.data.success ? `${user.fullName} was emailed a fresh account setup link.` : response.data.message || 'Please verify email settings and try again.',
        tone: response.data.success ? 'success' : 'warning',
      })
      await reload()
    } catch (error) {
      pushToast({ title: 'Resend failed', description: toServiceError(error, 'Unable to resend the invitation.'), tone: 'danger' })
    } finally {
      setResendingUserId(null)
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Platform Command Centre" title="Platform Users" description="Manage Platform Owner, Platform Admin, and Support Admin access for the Ecosys platform." />

      {loading ? <LoadingState label="Loading platform users" /> : null}
      {!loading && error ? (
        <section className="surface-card space-y-3">
          <InfoAlert title="Unable to load platform users" description={error} tone="danger" />
          <button type="button" className="button-secondary" onClick={() => void reload()}>
            <RefreshCw className="h-4 w-4" />Retry
          </button>
        </section>
      ) : null}

      {!loading && !error ? (
        <section className="surface-card space-y-4">
          <SectionTitle title="Users" description="Add, edit, activate, deactivate, and reset platform users." action={<button type="button" className="button-primary" onClick={() => setOpen(true)}>Add Platform User</button>} />
          <InfoAlert
            title="Platform user scope"
            description="Platform users are Ecosys staff or support users with access to the Command Centre. Tenant users are managed inside each tenant workspace."
            tone="info"
          />
          <div className="grid gap-3 md:grid-cols-3">
            <Field label="Search"><input value={search} onChange={(event) => setSearch(event.target.value)} className="field-input" placeholder="Search name or email" /></Field>
            <Field label="Role">
              <select value={roleFilter} onChange={(event) => setRoleFilter(event.target.value as typeof roleFilter)} className="field-input">
                <option value="All">All roles</option>
                {platformRoles.map((item) => <option key={item} value={item}>{getPlatformRoleLabel(item)}</option>)}
              </select>
            </Field>
            <Field label="Status">
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} className="field-input">
                <option value="All">All statuses</option>
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
                <option value="Disabled">Disabled</option>
              </select>
            </Field>
          </div>

          {rows.length === 0 ? <EmptyState title="No platform users found" description="Adjust your filters or add a new platform user." /> : (
            <DataTable
              rows={rows}
              rowKey={(row) => row.id}
              minTableWidth="min-w-[1220px] w-full"
              columns={[
                { key: 'name', header: 'Name', cell: (row) => <span className="font-semibold text-app">{row.fullName}</span> },
                { key: 'email', header: 'Email', cell: (row) => row.email },
                { key: 'phone', header: 'Phone', cell: (row) => row.phone || '-' },
                { key: 'role', header: 'Role', cell: (row) => getPlatformRoleLabel(row.role) },
                { key: 'status', header: 'Status', cell: (row) => userStatusBadge(row.status) },
                { key: 'lastLogin', header: 'Last Login', cell: (row) => row.lastLogin ? formatDateTime(row.lastLogin) : 'Never' },
                { key: 'lastCredentialSentAt', header: 'Last Credentials Sent', cell: (row) => row.lastCredentialSentAt ? formatDateTime(row.lastCredentialSentAt) : 'Never' },
                { key: 'mustChangePassword', header: 'Password Change', cell: (row) => row.mustChangePassword ? 'Required' : 'Not required' },
                {
                  key: 'actions',
                  header: 'Actions',
                  className: 'min-w-[430px]',
                  cell: (row) => (
                    <div className="flex flex-wrap gap-2">
                      <button type="button" className="button-secondary px-3 py-2" onClick={() => { setOpen(true); setForm(row) }}>Edit</button>
                      <button type="button" className="button-secondary px-3 py-2" onClick={() => void toggleStatus(row)}>{row.status === 'Active' ? 'Deactivate' : 'Activate'}</button>
                      <button type="button" className="button-secondary px-3 py-2" onClick={() => void resendCredentials(row)} disabled={resendingUserId === row.id}>{resendingUserId === row.id ? 'Sending...' : 'Resend Invite'}</button>
                      <button type="button" className="button-secondary px-3 py-2" onClick={() => void resetPassword(row)}>Reset Password</button>
                    </div>
                  ),
                },
              ]}
            />
          )}
        </section>
      ) : null}

      <Modal open={open} title="Platform User" description="Create or update platform-side access for Ecosys operations." onClose={() => !saving && setOpen(false)}>
        <div className="grid gap-3 md:grid-cols-2">
          <Field label="Full Name"><input value={form.fullName} onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))} className="field-input" /></Field>
          <Field label="Email"><input type="email" value={form.email} onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))} className="field-input" /></Field>
          <Field label="Phone"><input value={form.phone || ''} onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))} className="field-input" /></Field>
          <Field label="Role">
            <select value={normalizePlatformRole(form.role)} onChange={(event) => setForm((current) => ({ ...current, role: event.target.value as PlatformRole }))} className="field-input">
              {PLATFORM_ROLE_OPTIONS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
            <p className="text-xs text-muted">{getPlatformRoleDescription(form.role)}</p>
          </Field>
          <Field label="Status">
            <select value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as PlatformUserStatus }))} className="field-input">
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
            </select>
          </Field>
        </div>
        <div className="panel-subtle mt-4 rounded-2xl p-4">
          <p className="text-sm font-semibold text-app">Platform role scope</p>
          <p className="mt-2 text-sm text-muted">
            These roles are for Ecosys platform operations only and route to <span className="font-mono">/platform</span> after login. Tenant-side roles such as Tenant Admin, Supervisor/Manager, Technician, Stores User, and Viewer/User should be managed in tenant user administration.
          </p>
        </div>
        <div className="mt-6 flex justify-end gap-3">
          <button type="button" className="button-secondary" onClick={() => setOpen(false)} disabled={saving}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveUser()} disabled={saving}>{saving ? 'Saving...' : 'Save User'}</button>
        </div>
      </Modal>
    </div>
  )
}

type PlatformReportsPayload = {
  summary: ReportSummary & ReportsMeta
  dashboard: Record<string, unknown>
  revenue: Record<string, unknown>
  workOrders: Record<string, unknown>
  finance: Record<string, unknown>
  subscriptions: Array<Record<string, unknown>>
  audit: Array<Record<string, unknown>>
}

export function PlatformReportsPage() {
  const { data, loading, error, reload } = useAsyncData<PlatformReportsPayload>(
    async () => {
      const [summary, dashboard, revenue, workOrders, finance, subscriptions, audit] = await Promise.all([
        platformService.reportsApi.summary(),
        platformService.reportsApi.dashboardSummary(),
        platformService.reportsApi.revenue(),
        platformService.reportsApi.workOrders(),
        platformService.reportsApi.finance(),
        platformService.reportsApi.subscriptions(),
        platformService.reportsApi.audit(),
      ])

      return {
        summary: summary.data,
        dashboard,
        revenue,
        workOrders,
        finance,
        subscriptions,
        audit,
      }
    },
    {
      summary: {
        totalTenants: 0,
        activeTenants: 0,
        deactivatedTenants: 0,
        suspendedTenants: 0,
        totalUsers: 0,
        openWorkOrders: 0,
        closedWorkOrders: 0,
        overdueWorkOrders: 0,
        totalInvoices: 0,
        overdueInvoices: 0,
        totalRevenue: 0,
        outstandingInvoices: 0,
        monthlyRecurringRevenue: 0,
        asAtMonthStartUtc: '',
        apiAvailable: true,
      },
      dashboard: {},
      revenue: {},
      workOrders: {},
      finance: {},
      subscriptions: [],
      audit: [],
    },
    [],
  )

  const cards = [
    { title: 'Total Tenants', value: String(data.summary.totalTenants), detail: 'All registered tenants.', icon: Building2 },
    { title: 'Active Tenants', value: String(data.summary.activeTenants), detail: 'Tenants currently active.', icon: ShieldCheck },
    { title: 'Total Users', value: String(data.summary.totalUsers), detail: 'Platform-wide user count.', icon: Users },
    { title: 'Open Work Orders', value: String(data.summary.openWorkOrders), detail: 'Open tenant work orders.', icon: BarChart3 },
    { title: 'Overdue Work Orders', value: String(data.summary.overdueWorkOrders), detail: 'Past due work orders.', icon: Activity },
    { title: 'Total Revenue', value: `KES ${Number(data.summary.totalRevenue || 0).toLocaleString()}`, detail: 'Paid platform revenue.', icon: Wallet },
    { title: 'Open Balances', value: `KES ${Number(data.summary.outstandingInvoices || 0).toLocaleString()}`, detail: 'Amounts still awaiting settlement.', icon: FileText },
    { title: 'Monthly Recurring Revenue', value: `KES ${Number(data.summary.monthlyRecurringRevenue || 0).toLocaleString()}`, detail: 'MRR from active subscriptions.', icon: Wallet },
  ]

  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Platform Command Centre" title="Reports" description="Review platform activity, tenant performance, and operational totals." />

      {loading ? <LoadingState label="Loading platform reports" /> : null}
      {!loading && error ? (
        <section className="surface-card space-y-3">
          <InfoAlert title="Unable to load reports" description={error} tone="danger" />
          <button type="button" className="button-secondary" onClick={() => void reload()}>
            <RefreshCw className="h-4 w-4" />Retry
          </button>
        </section>
      ) : null}

      {!loading && !error ? (
        <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            {cards.map((card) => (
              <StatCard key={card.title} title={card.title} value={card.value} detail={card.detail} icon={card.icon} />
            ))}
          </section>

          <section className="surface-card space-y-3">
            <SectionTitle title="Report Summary" description="A quick snapshot of the data currently available across platform reporting." />
            <DataTable
              rows={[
                { id: 'subscriptions', label: 'Subscription records', value: String(data.subscriptions.length) },
                { id: 'audit', label: 'Audit records', value: String(data.audit.length) },
                { id: 'workOrders', label: 'Work order data', value: Object.keys(data.workOrders).length > 0 ? 'Available' : 'No data yet' },
                { id: 'finance', label: 'Operations summary', value: Object.keys(data.finance).length > 0 ? 'Available' : 'No data yet' },
                { id: 'revenue', label: 'Revenue summary', value: Object.keys(data.revenue).length > 0 ? 'Available' : 'No data yet' },
                { id: 'dashboard', label: 'Dashboard summary', value: Object.keys(data.dashboard).length > 0 ? 'Available' : 'No data yet' },
              ]}
              rowKey={(row) => row.id}
              minTableWidth="min-w-[980px] w-full"
              columns={[
                { key: 'label', header: 'Source', cell: (row) => row.label },
                { key: 'value', header: 'Status', cell: (row) => <span>{row.value}</span> },
              ]}
            />
          </section>
        </>
      ) : null}
    </div>
  )
}

export function PlatformAuditLogsPage() {
  const { data, loading, error, reload } = useAsyncData(async () => platformService.auditLogsApi.list(), { data: [] as AuditLog[], backendAvailable: true, message: '' }, [])
  const [search, setSearch] = useState('')
  const [actionFilter, setActionFilter] = useState('All')
  const [entityFilter, setEntityFilter] = useState('All')
  const [tenantFilter, setTenantFilter] = useState('All')
  const [severityFilter, setSeverityFilter] = useState('All')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  const actionOptions = useMemo(() => ['All', ...Array.from(new Set(data.data.map((item) => item.action)))], [data.data])
  const entityOptions = useMemo(() => ['All', ...Array.from(new Set(data.data.map((item) => item.entityType)))], [data.data])
  const tenantOptions = useMemo(() => ['All', ...Array.from(new Set(data.data.map((item) => item.tenant).filter(Boolean) as string[]))], [data.data])
  const severityOptions = useMemo(() => ['All', ...Array.from(new Set(data.data.map((item) => item.severity).filter(Boolean) as string[]))], [data.data])

  const rows = useMemo(() => {
    const query = search.trim().toLowerCase()
    return data.data.filter((item) => {
      const matchesQuery = !query || item.action.toLowerCase().includes(query) || item.actor.toLowerCase().includes(query) || (item.entityName || '').toLowerCase().includes(query)
      const matchesAction = actionFilter === 'All' || item.action === actionFilter
      const matchesEntity = entityFilter === 'All' || item.entityType === entityFilter
      const matchesTenant = tenantFilter === 'All' || item.tenant === tenantFilter
      const matchesSeverity = severityFilter === 'All' || item.severity === severityFilter
      const dateOnly = item.dateTime.slice(0, 10)
      const matchesFrom = !from || dateOnly >= from
      const matchesTo = !to || dateOnly <= to
      return matchesQuery && matchesAction && matchesEntity && matchesTenant && matchesSeverity && matchesFrom && matchesTo
    })
  }, [actionFilter, data.data, entityFilter, from, search, severityFilter, tenantFilter, to])

  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Platform Command Centre" title="Audit Logs" description="Search and filter platform activity by actor, entity, tenant, and date range." />

      {loading ? <LoadingState label="Loading audit logs" /> : null}
      {!loading && error ? (
        <section className="surface-card space-y-3">
          <InfoAlert title="Unable to load audit logs" description={error} tone="danger" />
          <button type="button" className="button-secondary" onClick={() => void reload()}>
            <RefreshCw className="h-4 w-4" />Retry
          </button>
        </section>
      ) : null}

      {!loading && !error ? (
        <section className="surface-card space-y-4">
          <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-7">
            <Field label="Search"><input value={search} onChange={(event) => setSearch(event.target.value)} className="field-input" placeholder="Actor, action, entity" /></Field>
            <Field label="Action"><select value={actionFilter} onChange={(event) => setActionFilter(event.target.value)} className="field-input">{actionOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
            <Field label="Entity Type"><select value={entityFilter} onChange={(event) => setEntityFilter(event.target.value)} className="field-input">{entityOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
            <Field label="Tenant"><select value={tenantFilter} onChange={(event) => setTenantFilter(event.target.value)} className="field-input">{tenantOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
            <Field label="Severity"><select value={severityFilter} onChange={(event) => setSeverityFilter(event.target.value)} className="field-input">{severityOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
            <Field label="From"><input type="date" value={from} onChange={(event) => setFrom(event.target.value)} className="field-input" /></Field>
            <Field label="To"><input type="date" value={to} onChange={(event) => setTo(event.target.value)} className="field-input" /></Field>
          </div>

          {rows.length === 0 ? <EmptyState title="No audit logs found" description="Adjust your filters and try again." /> : (
            <DataTable
              rows={rows}
              rowKey={(row) => row.id}
              minTableWidth="min-w-[1280px] w-full"
              columns={[
                { key: 'date', header: 'Date/Time', cell: (row) => formatDateTime(row.dateTime) },
                { key: 'actor', header: 'Actor', cell: (row) => row.actor },
                { key: 'action', header: 'Action', cell: (row) => row.action },
                { key: 'entityType', header: 'Entity Type', cell: (row) => row.entityType },
                { key: 'entityName', header: 'Description', cell: (row) => row.entityName || row.entityId },
                { key: 'tenant', header: 'Tenant', cell: (row) => row.tenant || '-' },
                { key: 'ip', header: 'IP Address', cell: (row) => row.ipAddress || '-' },
              ]}
            />
          )}
        </section>
      ) : null}
    </div>
  )
}

export function PlatformSettingsPage() {
  return <PlatformSettingsModulePage />
}
