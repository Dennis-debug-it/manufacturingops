import { KeyRound, Plus, Power, RefreshCw, Search, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useLocation, useSearchParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { Avatar, AvatarStack } from '../../components/ui/Avatar'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/ToastProvider'
import { MetricCard, MetricGrid, PageScaffold, PageTabs, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { branchService } from '../../services/branchService'
import { settingsService } from '../../services/settingsService'
import { userService } from '../../services/userService'
import { useAppStore } from '../../store/appStore'
import type { ApiPermissions, AssignmentGroupRecord, BranchRecord, UpsertAssignmentGroupInput, UpsertUserInput, UserRecord } from '../../types/api'

type WorkforceTab = 'users' | 'groups' | 'roles'

type WorkforcePayload = {
  users: UserRecord[]
  branches: BranchRecord[]
  groups: AssignmentGroupRecord[]
  usersError: string | null
  branchesError: string | null
  groupsError: string | null
}

type GroupEditorState = {
  name: string
  description: string
  skillArea: string
  branchId: string
  isActive: boolean
  memberUserIds: string[]
  teamLeadUserId: string
}

const emptyPermissions: ApiPermissions = {
  canViewWorkOrders: true,
  canCreateWorkOrders: true,
  canAssignWorkOrders: false,
  canCompleteWorkOrders: false,
  canViewKnowledge: true,
  canManageKnowledge: false,
  canViewMaterials: true,
  canRequestMaterials: true,
  canManageMaterials: false,
  canApproveMaterials: false,
  canIssueMaterials: false,
  canManageAssets: false,
  canManageSettings: false,
  canViewReports: false,
  canViewFinance: false,
  canManageFinance: false,
  canViewFinanceRates: false,
  canManageFinanceRates: false,
  canViewFinanceApprovals: false,
  canManageFinanceApprovals: false,
  canViewBillableWork: false,
  canManageBillableWork: false,
  canOverrideMaterialStock: false,
}

const permissionFields: Array<{ key: keyof ApiPermissions; label: string }> = [
  { key: 'canViewWorkOrders', label: 'View work orders' },
  { key: 'canCreateWorkOrders', label: 'Create work orders' },
  { key: 'canAssignWorkOrders', label: 'Assign work orders' },
  { key: 'canCompleteWorkOrders', label: 'Complete work orders' },
  { key: 'canViewKnowledge', label: 'View knowledge' },
  { key: 'canManageKnowledge', label: 'Manage knowledge' },
  { key: 'canViewMaterials', label: 'View materials' },
  { key: 'canRequestMaterials', label: 'Request materials' },
  { key: 'canManageMaterials', label: 'Manage materials' },
  { key: 'canApproveMaterials', label: 'Approve materials' },
  { key: 'canIssueMaterials', label: 'Issue materials' },
  { key: 'canManageAssets', label: 'Manage assets' },
  { key: 'canManageSettings', label: 'Manage settings' },
  { key: 'canViewReports', label: 'View reports' },
  { key: 'canViewFinance', label: 'View finance' },
  { key: 'canManageFinance', label: 'Manage finance' },
  { key: 'canViewFinanceRates', label: 'View rates' },
  { key: 'canManageFinanceRates', label: 'Manage rates' },
  { key: 'canViewFinanceApprovals', label: 'View approvals' },
  { key: 'canManageFinanceApprovals', label: 'Manage approvals' },
  { key: 'canViewBillableWork', label: 'View billable work' },
  { key: 'canManageBillableWork', label: 'Manage billable work' },
  { key: 'canOverrideMaterialStock', label: 'Override stock limits' },
]

const emptyPayload: WorkforcePayload = {
  users: [],
  branches: [],
  groups: [],
  usersError: null,
  branchesError: null,
  groupsError: null,
}

function defaultUserForm(): UpsertUserInput {
  return {
    fullName: '',
    email: '',
    phoneNumber: '',
    password: '',
    role: 'User',
    jobTitle: '',
    department: '',
    isActive: true,
    permissions: emptyPermissions,
    branchIds: [],
    defaultBranchId: null,
    hasAllBranchAccess: false,
    assignmentGroupIds: [],
  }
}

function defaultGroupForm(): GroupEditorState {
  return {
    name: '',
    description: '',
    skillArea: '',
    branchId: '',
    isActive: true,
    memberUserIds: [],
    teamLeadUserId: '',
  }
}

export function SettingsUsersPage() {
  const { pushToast } = useToast()
  const { session } = useAuth()
  const location = useLocation()
  const tenantData = useAppStore((database) => (session?.tenantId ? database.tenantData[session.tenantId] : undefined))
  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab = asTab(searchParams.get('tab'), location.pathname)
  const [userSearch, setUserSearch] = useState('')
  const [userStatusFilter, setUserStatusFilter] = useState('All')
  const [groupSearch, setGroupSearch] = useState('')
  const [groupStatusFilter, setGroupStatusFilter] = useState('All')
  const [selectedRoleUserId, setSelectedRoleUserId] = useState('')
  const [userDrawerOpen, setUserDrawerOpen] = useState(false)
  const [editingUser, setEditingUser] = useState<UserRecord | null>(null)
  const [userForm, setUserForm] = useState<UpsertUserInput>(defaultUserForm)
  const [isSavingUser, setIsSavingUser] = useState(false)
  const [userActionTarget, setUserActionTarget] = useState<{ type: 'status' | 'delete'; user: UserRecord; nextIsActive?: boolean } | null>(null)
  const [isSubmittingUserAction, setIsSubmittingUserAction] = useState(false)
  const [resetPasswordUser, setResetPasswordUser] = useState<UserRecord | null>(null)
  const [temporaryPassword, setTemporaryPassword] = useState('')
  const [isResettingPassword, setIsResettingPassword] = useState(false)
  const [resendingCredentialsUserId, setResendingCredentialsUserId] = useState<string | null>(null)
  const [groupDrawerOpen, setGroupDrawerOpen] = useState(false)
  const [editingGroup, setEditingGroup] = useState<AssignmentGroupRecord | null>(null)
  const [groupForm, setGroupForm] = useState<GroupEditorState>(defaultGroupForm)

  const { data, loading, reload, setData } = useAsyncData<WorkforcePayload>(
    async (signal) => {
      const [usersResult, branchesResult, groupsResult] = await Promise.allSettled([
        userService.list(signal),
        branchService.list(signal),
        settingsService.listAssignmentGroups(signal),
      ])

      return {
        users: usersResult.status === 'fulfilled' ? usersResult.value : [],
        branches: branchesResult.status === 'fulfilled' ? branchesResult.value : [],
        groups: groupsResult.status === 'fulfilled' ? groupsResult.value : [],
        usersError: usersResult.status === 'rejected' ? toErrorMessage(usersResult.reason, 'Unable to load users.') : null,
        branchesError: branchesResult.status === 'rejected' ? toErrorMessage(branchesResult.reason, 'Unable to load branches.') : null,
        groupsError: groupsResult.status === 'rejected' ? toErrorMessage(groupsResult.reason, 'Unable to load groups.') : null,
      }
    },
    emptyPayload,
    [],
  )

  useEffect(() => {
    if (!selectedRoleUserId && data.users.length > 0) {
      setSelectedRoleUserId(data.users[0].id)
    }
  }, [data.users, selectedRoleUserId])

  const selectedRoleUser = data.users.find((user) => user.id === selectedRoleUserId) ?? data.users[0] ?? null
  const usersById = useMemo(() => new Map(data.users.map((user) => [user.id, user])), [data.users])
  const canDeleteUsers = typeof userService.remove === 'function'

  const filteredUsers = useMemo(
    () =>
      data.users
        .filter((user) => userStatusFilter === 'All' || (userStatusFilter === 'Active' ? user.isActive : !user.isActive))
        .filter((user) =>
          `${user.fullName} ${user.email} ${user.role} ${user.defaultBranchName || ''} ${user.assignmentGroups.map((group) => group.name).join(' ')}`
            .toLowerCase()
            .includes(userSearch.toLowerCase()),
        ),
    [data.users, userSearch, userStatusFilter],
  )

  const filteredGroups = useMemo(
    () =>
      data.groups
        .filter((group) => groupStatusFilter === 'All' || (groupStatusFilter === 'Active' ? group.isActive : !group.isActive))
        .filter((group) =>
          `${group.name} ${group.skillArea || ''} ${group.branchName || ''} ${group.members.map((member) => member.memberName || '').join(' ')}`
            .toLowerCase()
            .includes(groupSearch.toLowerCase()),
        ),
    [data.groups, groupSearch, groupStatusFilter],
  )

  function setTab(tab: WorkforceTab) {
    const next = new URLSearchParams(searchParams)
    next.set('tab', tab)
    setSearchParams(next, { replace: true })
  }

  function openUserEditor(user?: UserRecord) {
    setEditingUser(user ?? null)
    setUserForm({
      fullName: user?.fullName ?? '',
      email: user?.email ?? '',
      phoneNumber: user?.phoneNumber ?? '',
      password: '',
      role: user?.role ?? 'User',
      jobTitle: user?.jobTitle ?? '',
      department: user?.department ?? '',
      isActive: user?.isActive ?? true,
      permissions: user?.permissions ?? emptyPermissions,
      branchIds: user?.branchIds ?? [],
      defaultBranchId: user?.defaultBranchId ?? null,
      hasAllBranchAccess: user?.hasAllBranchAccess ?? false,
      assignmentGroupIds: user?.assignmentGroups.map((group) => group.id) ?? [],
    })
    setUserDrawerOpen(true)
  }

  function closeUserEditor(force = false) {
    if (isSavingUser && !force) return
    setUserDrawerOpen(false)
    setEditingUser(null)
    setUserForm(defaultUserForm())
  }

  function openResetPassword(user: UserRecord) {
    setResetPasswordUser(user)
    setTemporaryPassword('')
  }

  function closeResetPassword(force = false) {
    if (isResettingPassword && !force) return
    setResetPasswordUser(null)
    setTemporaryPassword('')
  }

  function openStatusAction(user: UserRecord, nextIsActive: boolean) {
    setUserActionTarget({ type: 'status', user, nextIsActive })
  }

  function openDeleteAction(user: UserRecord) {
    setUserActionTarget({ type: 'delete', user })
  }

  function closeUserActionModal() {
    if (isSubmittingUserAction) return
    setUserActionTarget(null)
  }

  function isCurrentUser(user?: UserRecord | null) {
    return Boolean(user && session?.userId === user.id)
  }

  function openGroupEditor(group?: AssignmentGroupRecord) {
    setEditingGroup(group ?? null)

    const memberUserIds = group?.members
      .map((member) => member.userId ?? data.users.find((user) => user.linkedTechnicianId === member.technicianId)?.id ?? '')
      .filter(Boolean) ?? []

    const leadUserId =
      group?.members.find((member) => member.isLead)?.userId
      ?? memberUserIds.find((userId) => {
        const user = usersById.get(userId)
        return Boolean(user?.assignmentGroups.some((assignment) => assignment.id === group?.id && assignment.isLead))
      })
      ?? ''

    setGroupForm({
      name: group?.name ?? '',
      description: group?.description ?? '',
      skillArea: group?.skillArea ?? '',
      branchId: group?.branchId ?? '',
      isActive: group?.isActive ?? true,
      memberUserIds,
      teamLeadUserId: leadUserId,
    })
    setGroupDrawerOpen(true)
  }

  async function saveUser() {
    if (isSavingUser) return

    if (!userForm.fullName.trim() || !userForm.email.trim()) {
      pushToast({ title: 'Missing details', description: 'Full name and email are required.', tone: 'warning' })
      return
    }

    if (editingUser && isCurrentUser(editingUser) && session?.role !== 'superadmin' && editingUser.role !== userForm.role) {
      pushToast({ title: 'Role change blocked', description: 'You cannot change your own role from this account.', tone: 'warning' })
      return
    }

    setIsSavingUser(true)
    try {
      if (editingUser) {
        await userService.update(editingUser.id, userForm)
        pushToast({ title: 'User updated', description: 'Workforce settings were saved successfully.', tone: 'success' })
      } else {
        const created = await userService.create(userForm)
        pushToast({
          title: 'User created',
          description: created.credentialDelivery?.success ? 'User created. An account setup link has been sent to their email.' : 'User created, but the account setup email failed. Please resend the invitation.',
          tone: created.credentialDelivery?.success ? 'success' : 'warning',
        })
      }
      closeUserEditor(true)
      await reload()
    } catch (error) {
      pushToast({ title: 'Save failed', description: toErrorMessage(error, 'Unable to save user.'), tone: 'danger' })
    } finally {
      setIsSavingUser(false)
    }
  }

  async function submitUserAction() {
    if (!userActionTarget || isSubmittingUserAction) return

    setIsSubmittingUserAction(true)
    try {
      if (userActionTarget.type === 'status') {
        await userService.updateStatus(userActionTarget.user.id, Boolean(userActionTarget.nextIsActive))
        pushToast({
          title: userActionTarget.nextIsActive ? 'User activated' : 'User deactivated',
          description: `${userActionTarget.user.fullName} has been updated.`,
          tone: 'success',
        })
      } else {
        await userService.remove(userActionTarget.user.id)
        pushToast({
          title: 'User deleted',
          description: `${userActionTarget.user.fullName} has been removed from the active directory.`,
          tone: 'success',
        })
      }

      setUserActionTarget(null)
      await reload()
    } catch (error) {
      pushToast({
        title: 'Action failed',
        description: toErrorMessage(error, 'Unable to complete the user action.'),
        tone: 'danger',
      })
    } finally {
      setIsSubmittingUserAction(false)
    }
  }

  async function submitResetPassword() {
    if (!resetPasswordUser || isResettingPassword) return

    if (!temporaryPassword.trim()) {
      pushToast({ title: 'Temporary password required', description: 'Enter a temporary password before resetting.', tone: 'warning' })
      return
    }

    setIsResettingPassword(true)
    try {
      const response = await userService.resetPassword(resetPasswordUser.id, temporaryPassword)
      pushToast({
        title: 'Password reset',
        description: response.success ? 'Password reset and credential email sent.' : response.message || 'Password reset, but credential email failed. Please resend credentials.',
        tone: response.success ? 'success' : 'warning',
      })
      closeResetPassword(true)
      await reload()
    } catch (error) {
      pushToast({ title: 'Reset failed', description: toErrorMessage(error, 'Unable to reset the user password.'), tone: 'danger' })
    } finally {
      setIsResettingPassword(false)
    }
  }

  async function resendCredentials(user: UserRecord) {
    if (resendingCredentialsUserId) return

    setResendingCredentialsUserId(user.id)
    try {
      const response = await userService.resendCredentials(user.id)
      pushToast({
        title: response.success ? 'Invitation sent' : 'Invitation email failed',
        description: response.success ? `An account setup link was sent to ${user.fullName}.` : response.message || 'Please verify email settings and try again.',
        tone: response.success ? 'success' : 'warning',
      })
      await reload()
    } catch (error) {
      pushToast({ title: 'Resend failed', description: toErrorMessage(error, 'Unable to resend the invitation.'), tone: 'danger' })
    } finally {
      setResendingCredentialsUserId(null)
    }
  }

  async function saveGroup() {
    const input: UpsertAssignmentGroupInput = {
      name: groupForm.name.trim(),
      description: groupForm.description.trim() || null,
      skillArea: groupForm.skillArea.trim() || null,
      branchId: groupForm.branchId || null,
      isActive: groupForm.isActive,
      technicianIds: [],
      members: groupForm.memberUserIds.map((userId) => ({
        userId,
        isLead: userId === groupForm.teamLeadUserId,
      })),
    }

    try {
      if (editingGroup) {
        await settingsService.updateAssignmentGroup(editingGroup.id, input)
        pushToast({ title: 'Group updated', description: 'The assignment group was saved successfully.', tone: 'success' })
      } else {
        await settingsService.createAssignmentGroup(input)
        pushToast({ title: 'Group created', description: 'The workforce group is now available for dispatch.', tone: 'success' })
      }
      setGroupDrawerOpen(false)
      await reload()
    } catch (error) {
      pushToast({ title: 'Save failed', description: toErrorMessage(error, 'Unable to save assignment group.'), tone: 'danger' })
    }
  }

  async function togglePermission(key: keyof ApiPermissions, value: boolean) {
    if (!selectedRoleUser) return

    const nextPermissions = {
      ...selectedRoleUser.permissions,
      [key]: value,
    }

    setData((current) => ({
      ...current,
      users: current.users.map((user) =>
        user.id === selectedRoleUser.id
          ? {
              ...user,
              permissions: nextPermissions,
            }
          : user,
      ),
    }))

    try {
      await userService.updatePermissions(selectedRoleUser.id, nextPermissions)
      pushToast({ title: 'Permissions updated', description: `Saved permission changes for ${selectedRoleUser.fullName}.`, tone: 'success' })
      await reload()
    } catch (error) {
      pushToast({ title: 'Update failed', description: toErrorMessage(error, 'Unable to update permissions.'), tone: 'danger' })
      await reload()
    }
  }

  const roleTemplates = tenantData?.settings.rolePermissions ?? []
  const permissionGroups = tenantData?.settings.permissionGroups ?? []
  const activeUsersCount = data.users.filter((user) => user.isActive).length
  const inactiveUsersCount = data.users.length - activeUsersCount
  const activeGroupsCount = data.groups.filter((group) => group.isActive).length
  const adminsCount = data.users.filter((user) => user.role === 'Admin').length

  return (
    <PageScaffold
        eyebrow="Settings"
        title="Users & roles"
        description="Manage workforce identities, assignment groups, and permission controls from one connected operational module."
        actions={
          activeTab === 'users' ? (
            <button type="button" className="button-primary" onClick={() => openUserEditor()}>
              <Plus className="h-4 w-4" />
              Add user
            </button>
          ) : activeTab === 'groups' ? (
            <button type="button" className="button-primary" onClick={() => openGroupEditor()}>
              <Plus className="h-4 w-4" />
              Add group
            </button>
          ) : null
        }
      >
      <MetricGrid>
        <MetricCard label="Active users" value={activeUsersCount} meta={`${inactiveUsersCount} inactive`} emphasis="accent" />
        <MetricCard label="Assignment groups" value={data.groups.length} meta={`${activeGroupsCount} active groups`} />
        <MetricCard label="Admins" value={adminsCount} meta="Named admin accounts" />
        <MetricCard label="Role templates" value={roleTemplates.length} meta={`${permissionGroups.length} permission groups`} />
      </MetricGrid>

      <SectionCard title="Access control workspace" description="Switch between directory management, assignment groups, and role permission review.">
        <div className="space-y-5">
          <PageTabs
            tabs={[
              { id: 'users', label: 'Users' },
              { id: 'groups', label: 'Assignment Groups' },
              { id: 'roles', label: 'Roles & Permissions' },
            ]}
            activeTab={activeTab}
            onChange={setTab}
          />

          {activeTab === 'users' ? <UsersTab /> : null}
          {activeTab === 'groups' ? <GroupsTab /> : null}
          {activeTab === 'roles' ? <RolesTab /> : null}
        </div>
      </SectionCard>

      <UserEditor />
      <ResetPasswordModal />
      <UserActionModal />
      <GroupEditor />
    </PageScaffold>
  )

  function UsersTab() {
    return (
      <div className="space-y-4">
        <div className="grid gap-3 lg:grid-cols-[1.2fr_220px]">
          <label className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-3">
            <Search className="h-4 w-4 text-muted" />
            <input
              value={userSearch}
              onChange={(event) => setUserSearch(event.target.value)}
              placeholder="Search by name, email, role, branch, or group"
              className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
            />
          </label>
          <select value={userStatusFilter} onChange={(event) => setUserStatusFilter(event.target.value)} className="field-input">
            <option value="All">All statuses</option>
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
          </select>
        </div>

        {loading ? <LoadingState label="Loading users and groups" /> : null}
        {!loading && data.usersError ? <ErrorState title="Unable to load users" description={data.usersError} /> : null}
        {!loading && !data.usersError && filteredUsers.length === 0 ? (
          <EmptyState
            title={data.users.length === 0 ? 'No users yet' : 'No matching users'}
            description={
              data.users.length === 0
                ? 'Create the first workforce user to begin managing branch access and group routing.'
                : 'Adjust the search or status filter to see more workforce users.'
            }
            actionLabel="Refresh"
            onAction={() => void reload()}
          />
        ) : null}
        {!loading && !data.usersError && filteredUsers.length > 0 ? (
          <DataTable
            rows={filteredUsers}
            rowKey={(row) => row.id}
            pageSize={10}
            minTableWidth="min-w-[1120px] w-full"
            columns={[
              {
                key: 'name',
                header: 'Name',
                cell: (row) => (
                  <div className="flex items-center gap-3">
                    <Avatar name={row.fullName} />
                    <div>
                      <p className="font-semibold text-app">{row.fullName}</p>
                      <p className="mt-1 text-xs text-muted">{row.jobTitle || 'Workforce user'}</p>
                    </div>
                  </div>
                ),
              },
              { key: 'email', header: 'Email', cell: (row) => <span>{row.email}</span> },
              { key: 'role', header: 'Role', cell: (row) => <Badge tone={row.role === 'Admin' ? 'info' : 'default'}>{row.role}</Badge> },
              {
                key: 'branch',
                header: 'Branch',
                cell: (row) => <span>{formatBranchLabel(row, data.branches)}</span>,
              },
              { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'danger'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
              { key: 'credentialSent', header: 'Last Credentials Sent', cell: (row) => row.lastCredentialSentAt ? new Date(row.lastCredentialSentAt).toLocaleString() : 'Never' },
              { key: 'mustChangePassword', header: 'Password Change', cell: (row) => <Badge tone={row.mustChangePassword ? 'warning' : 'neutral'}>{row.mustChangePassword ? 'Required' : 'Not required'}</Badge> },
              {
                key: 'groups',
                header: 'Assigned Groups',
                cell: (row) =>
                  row.assignmentGroups.length > 0 ? (
                    <div className="flex flex-wrap gap-2">
                      {row.assignmentGroups.map((group) => (
                        <Badge key={group.id} tone={group.isLead ? 'info' : 'neutral'}>
                          {group.isLead ? `${group.name} Lead` : group.name}
                        </Badge>
                      ))}
                    </div>
                  ) : (
                    <span className="text-muted">No groups</span>
                  ),
              },
              {
                key: 'actions',
                header: 'Actions',
                className: 'min-w-[470px]',
                cell: (row) => (
                  <div className="flex flex-wrap gap-2">
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => openUserEditor(row)}>
                      Edit
                    </button>
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => void resendCredentials(row)} disabled={resendingCredentialsUserId === row.id}>
                      <RefreshCw className="h-4 w-4" />
                      {resendingCredentialsUserId === row.id ? 'Sending...' : 'Resend Invite'}
                    </button>
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => openResetPassword(row)}>
                      <KeyRound className="h-4 w-4" />
                      Reset Password
                    </button>
                    {!isCurrentUser(row) ? (
                      <button
                        type="button"
                        className="button-secondary px-3 py-2"
                        onClick={() => openStatusAction(row, !row.isActive)}
                      >
                        <Power className="h-4 w-4" />
                        {row.isActive ? 'Deactivate' : 'Activate'}
                      </button>
                    ) : null}
                    {canDeleteUsers && !isCurrentUser(row) ? (
                      <button type="button" className="button-secondary px-3 py-2" onClick={() => openDeleteAction(row)}>
                        <Trash2 className="h-4 w-4" />
                        Delete
                      </button>
                    ) : null}
                  </div>
                ),
              },
            ]}
          />
        ) : null}
      </div>
    )
  }

  function GroupsTab() {
    return (
      <div className="space-y-4">
        <div className="grid gap-3 lg:grid-cols-[1.2fr_220px]">
          <label className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-3">
            <Search className="h-4 w-4 text-muted" />
            <input
              value={groupSearch}
              onChange={(event) => setGroupSearch(event.target.value)}
              placeholder="Search by group, type, branch, or member"
              className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
            />
          </label>
          <select value={groupStatusFilter} onChange={(event) => setGroupStatusFilter(event.target.value)} className="field-input">
            <option value="All">All statuses</option>
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
          </select>
        </div>

        {loading ? <LoadingState label="Loading assignment groups" /> : null}
        {!loading && data.groupsError ? <ErrorState title="Unable to load groups" description={data.groupsError} /> : null}
        {!loading && !data.groupsError && filteredGroups.length === 0 ? (
          <EmptyState
            title={data.groups.length === 0 ? 'No groups yet' : 'No matching groups'}
            description={
              data.groups.length === 0
                ? 'Create the first assignment group to organize your workforce.'
                : 'Adjust the search or status filter to find a group.'
            }
            actionLabel="Refresh"
            onAction={() => void reload()}
          />
        ) : null}
        {!loading && !data.groupsError && filteredGroups.length > 0 ? (
          <DataTable
            rows={filteredGroups}
            rowKey={(row) => row.id}
            pageSize={8}
            minTableWidth="min-w-[1080px] w-full"
            columns={[
              {
                key: 'name',
                header: 'Group Name',
                cell: (row) => (
                  <div>
                    <p className="font-semibold text-app">{row.name}</p>
                    <p className="mt-1 text-xs text-muted">{row.description || 'No description provided'}</p>
                  </div>
                ),
              },
              { key: 'type', header: 'Type', cell: (row) => <span>{row.skillArea || 'General'}</span> },
              { key: 'branch', header: 'Branch', cell: (row) => <span>{row.branchName || 'All branches'}</span> },
              {
                key: 'lead',
                header: 'Team Lead',
                cell: (row) => {
                  const lead = row.members.find((member) => member.isLead)
                  return lead?.memberName ? (
                    <div className="flex items-center gap-3">
                      <Avatar name={lead.memberName} size="sm" tone="emerald" />
                      <span>{lead.memberName}</span>
                    </div>
                  ) : (
                    <span className="text-muted">Not set</span>
                  )
                },
              },
              {
                key: 'members',
                header: 'Members Count',
                cell: (row) => (
                  <div className="flex items-center gap-3">
                    <AvatarStack names={row.members.map((member) => member.memberName)} />
                    <span>{row.members.length}</span>
                  </div>
                ),
              },
              { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'danger'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> },
              {
                key: 'actions',
                header: 'Actions',
                cell: (row) => (
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => openGroupEditor(row)}>
                    Edit
                  </button>
                ),
              },
            ]}
          />
        ) : null}
      </div>
    )
  }

  function RolesTab() {
    return (
      <div className="space-y-4">
        <div className="grid gap-4 xl:grid-cols-2">
          <section className="panel-subtle rounded-[28px] p-5">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-xl font-semibold text-app">Role templates</h2>
              <Badge tone="info">{roleTemplates.length}</Badge>
            </div>
            <div className="mt-4 space-y-3">
              {roleTemplates.length === 0 ? (
                <div className="text-sm text-muted">No role template metadata is available for your workspace yet.</div>
              ) : (
                roleTemplates.map((role) => (
                  <article key={role.id} className="panel-subtle-strong rounded-2xl px-4 py-4">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-semibold text-app">{role.role}</p>
                      <Badge tone={role.permissions.includes('*') ? 'success' : 'neutral'}>
                        {role.permissions.includes('*') ? 'Full access' : `${role.permissions.length} grants`}
                      </Badge>
                    </div>
                    <p className="mt-2 text-sm text-muted">{role.permissions.includes('*') ? 'Wildcard permission set for workspace administrators.' : role.permissions.join(', ')}</p>
                  </article>
                ))
              )}
            </div>
          </section>

          <section className="panel-subtle rounded-[28px] p-5">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-xl font-semibold text-app">Permission groups</h2>
              <Badge tone="info">{permissionGroups.length}</Badge>
            </div>
            <div className="mt-4 space-y-3">
              {permissionGroups.length === 0 ? (
                <div className="text-sm text-muted">No permission group metadata is available for your workspace yet.</div>
              ) : (
                permissionGroups.map((group) => (
                  <article key={group.id} className="panel-subtle-strong rounded-2xl px-4 py-4">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-semibold text-app">{group.name}</p>
                      <Badge tone="neutral">{group.permissions.length} permissions</Badge>
                    </div>
                    <p className="mt-2 text-sm text-muted">{group.permissions.join(', ')}</p>
                  </article>
                ))
              )}
            </div>
          </section>
        </div>

        {loading ? <LoadingState label="Loading permission sets" /> : null}
        {!loading && data.usersError ? <ErrorState title="Unable to load permission users" description={data.usersError} /> : null}
        {!loading && !data.usersError && data.users.length === 0 ? (
          <EmptyState title="No users found" description="User records are required before permissions can be adjusted." actionLabel="Refresh" onAction={() => void reload()} />
        ) : null}

        {!loading && !data.usersError && data.users.length > 0 ? (
          <div className="grid gap-4 xl:grid-cols-[0.72fr_1.28fr]">
            <section className="panel-subtle rounded-[28px] p-4">
              <div className="space-y-3">
                {data.users.map((user) => (
                  <button
                    key={user.id}
                    type="button"
                    className={`w-full rounded-2xl border px-4 py-3 text-left transition ${selectedRoleUser?.id === user.id ? 'border-cyan-400/40 bg-cyan-400/10' : 'panel-subtle hover-surface border-app'}`}
                    onClick={() => setSelectedRoleUserId(user.id)}
                  >
                    <div className="flex items-center justify-between gap-3">
                      <div className="flex items-center gap-3">
                        <Avatar name={user.fullName} size="sm" />
                        <div>
                          <p className="font-semibold text-app">{user.fullName}</p>
                          <p className="mt-1 text-xs text-muted">{user.role}</p>
                        </div>
                      </div>
                      <Badge tone={user.isActive ? 'success' : 'danger'}>{user.isActive ? 'Active' : 'Inactive'}</Badge>
                    </div>
                  </button>
                ))}
              </div>
            </section>

            <section className="panel-subtle rounded-[28px] p-5">
              {selectedRoleUser ? (
                <>
                  <h2 className="text-xl font-semibold text-app">{selectedRoleUser.fullName}</h2>
                  <p className="mt-2 text-sm text-muted">
                    Role: {selectedRoleUser.role} | Branch access: {selectedRoleUser.hasAllBranchAccess ? 'All branches' : `${selectedRoleUser.branchIds.length} assigned`} | Groups: {selectedRoleUser.assignmentGroups.length}
                  </p>
                  <div className="mt-6 grid gap-3 md:grid-cols-2">
                    {permissionFields.map((permission) => (
                      <label key={permission.key} className="panel-subtle-strong flex items-center justify-between rounded-2xl px-4 py-3">
                        <span className="text-sm text-app">{permission.label}</span>
                        <input
                          type="checkbox"
                          checked={selectedRoleUser.permissions?.[permission.key] ?? false}
                          onChange={(event) => void togglePermission(permission.key, event.target.checked)}
                        />
                      </label>
                    ))}
                  </div>
                </>
              ) : null}
            </section>
          </div>
        ) : null}
      </div>
    )
  }

  function UserEditor() {
    const editingSelf = isCurrentUser(editingUser)

    return (
      <Drawer
        open={userDrawerOpen}
        title={editingUser ? 'Edit User' : 'Add User'}
        description="Manage user identity, access, branch scope, and assignment groups."
        onClose={closeUserEditor}
      >
        <div className="space-y-5">
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Full name"><input value={userForm.fullName} onChange={(event) => setUserForm((current) => ({ ...current, fullName: event.target.value }))} className="field-input" /></Field>
            <Field label="Email"><input value={userForm.email} onChange={(event) => setUserForm((current) => ({ ...current, email: event.target.value }))} className="field-input" /></Field>
            <Field label="Phone number"><input value={userForm.phoneNumber || ''} onChange={(event) => setUserForm((current) => ({ ...current, phoneNumber: event.target.value }))} className="field-input" placeholder="Optional" /></Field>
            <Field label="Role">
              <select
                value={userForm.role}
                onChange={(event) => setUserForm((current) => ({ ...current, role: event.target.value }))}
                className="field-input"
                disabled={editingSelf && session?.role !== 'superadmin'}
              >
                <option value="Admin">Admin</option>
                <option value="User">User</option>
              </select>
            </Field>
            <Field label="Job title"><input value={userForm.jobTitle || ''} onChange={(event) => setUserForm((current) => ({ ...current, jobTitle: event.target.value }))} className="field-input" /></Field>
            <Field label="Department"><input value={userForm.department || ''} onChange={(event) => setUserForm((current) => ({ ...current, department: event.target.value }))} className="field-input" /></Field>
          </div>

          {!editingUser ? (
            <div className="rounded-2xl border border-cyan-400/20 bg-cyan-500/5 px-4 py-3 text-sm text-muted">
              Creating a user sends a secure account setup link so they can choose their own password before signing in.
            </div>
          ) : null}

          {editingSelf && session?.role !== 'superadmin' ? (
            <div className="rounded-2xl border border-amber-400/20 bg-amber-500/5 px-4 py-3 text-sm text-muted">
              Your own role can only be changed by a platform admin.
            </div>
          ) : null}

          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">User is active</span>
            <input type="checkbox" checked={userForm.isActive ?? true} onChange={(event) => setUserForm((current) => ({ ...current, isActive: event.target.checked }))} />
          </label>

          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">All branch access</span>
            <input
              type="checkbox"
              checked={userForm.hasAllBranchAccess}
              onChange={(event) =>
                setUserForm((current) => ({
                  ...current,
                  hasAllBranchAccess: event.target.checked,
                  branchIds: event.target.checked ? [] : current.branchIds,
                }))
              }
            />
          </label>

          {!userForm.hasAllBranchAccess ? (
            <Field label="Branch access">
              {data.branchesError ? (
                <div className="rounded-2xl border border-rose-400/20 bg-rose-500/5 px-4 py-3 text-sm text-muted">{data.branchesError}</div>
              ) : (
                <div className="grid gap-2 md:grid-cols-2">
                  {data.branches.map((branch) => (
                    <label key={branch.id} className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-3">
                      <input
                        type="checkbox"
                        checked={userForm.branchIds?.includes(branch.id) ?? false}
                        onChange={(event) =>
                          setUserForm((current) => ({
                            ...current,
                            branchIds: event.target.checked
                              ? [...(current.branchIds ?? []), branch.id]
                              : (current.branchIds ?? []).filter((id) => id !== branch.id),
                          }))
                        }
                      />
                      <span className="text-sm text-app">{branch.name}</span>
                    </label>
                  ))}
                </div>
              )}
            </Field>
          ) : null}

          <Field label="Default branch">
            <select value={userForm.defaultBranchId || ''} onChange={(event) => setUserForm((current) => ({ ...current, defaultBranchId: event.target.value || null }))} className="field-input">
              <option value="">No default branch</option>
              {data.branches.map((branch) => (
                <option key={branch.id} value={branch.id}>{branch.name}</option>
              ))}
            </select>
          </Field>

          <Field label="Assigned groups">
            {data.groupsError ? (
              <div className="rounded-2xl border border-rose-400/20 bg-rose-500/5 px-4 py-3 text-sm text-muted">{data.groupsError}</div>
            ) : (
              <div className="grid gap-2">
                {data.groups.map((group) => (
                  <label key={group.id} className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                    <div>
                      <p className="text-sm font-semibold text-app">{group.name}</p>
                      <p className="mt-1 text-xs text-muted">{group.skillArea || 'General'} | {group.branchName || 'All branches'}</p>
                    </div>
                    <input
                      type="checkbox"
                      checked={userForm.assignmentGroupIds?.includes(group.id) ?? false}
                      onChange={(event) =>
                        setUserForm((current) => ({
                          ...current,
                          assignmentGroupIds: event.target.checked
                            ? [...(current.assignmentGroupIds ?? []), group.id]
                            : (current.assignmentGroupIds ?? []).filter((id) => id !== group.id),
                        }))
                      }
                    />
                  </label>
                ))}
                {data.groups.length === 0 ? <div className="text-sm text-muted">No groups are available yet.</div> : null}
              </div>
            )}
          </Field>

          <div>
            <p className="text-sm font-medium text-app">Permissions</p>
            <div className="mt-3 grid gap-2 md:grid-cols-2">
              {permissionFields.map((permission) => (
                <label key={permission.key} className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                  <span className="text-sm text-app">{permission.label}</span>
                  <input
                    type="checkbox"
                    checked={userForm.permissions?.[permission.key] ?? false}
                    onChange={(event) =>
                      setUserForm((current) => ({
                        ...current,
                        permissions: {
                          ...(current.permissions ?? emptyPermissions),
                          [permission.key]: event.target.checked,
                        },
                      }))
                    }
                  />
                </label>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => closeUserEditor()} disabled={isSavingUser}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void saveUser()} disabled={isSavingUser}>
              {isSavingUser ? 'Saving...' : editingUser ? 'Save Changes' : 'Create User'}
            </button>
          </div>
        </div>
      </Drawer>
    )
  }

  function ResetPasswordModal() {
    return (
      <Modal
        open={Boolean(resetPasswordUser)}
        title="Reset Password"
        description="Set a temporary password for this user."
        onClose={closeResetPassword}
        maxWidth="max-w-lg"
      >
        <div className="space-y-4">
          <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">
            User: <span className="font-semibold text-app">{resetPasswordUser?.fullName}</span>
          </div>
          <Field label="Temporary password">
            <input
              type="password"
              value={temporaryPassword}
              onChange={(event) => setTemporaryPassword(event.target.value)}
              className="field-input"
            />
          </Field>
          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => closeResetPassword()} disabled={isResettingPassword}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void submitResetPassword()} disabled={isResettingPassword}>
              {isResettingPassword ? 'Resetting...' : 'Reset Password'}
            </button>
          </div>
        </div>
      </Modal>
    )
  }

  function UserActionModal() {
    const isStatusAction = userActionTarget?.type === 'status'

    return (
      <Modal
        open={Boolean(userActionTarget)}
        title={
          userActionTarget?.type === 'delete'
            ? 'Delete User'
            : userActionTarget?.nextIsActive
              ? 'Activate User'
              : 'Deactivate User'
        }
        description={
          userActionTarget?.type === 'delete'
            ? 'This will remove the user from the active directory.'
            : isStatusAction
              ? 'Confirm this user status change.'
              : ''
        }
        onClose={closeUserActionModal}
        maxWidth="max-w-lg"
      >
        <div className="space-y-4">
          <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">
            User: <span className="font-semibold text-app">{userActionTarget?.user.fullName}</span>
          </div>
          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={closeUserActionModal} disabled={isSubmittingUserAction}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void submitUserAction()} disabled={isSubmittingUserAction}>
              {isSubmittingUserAction
                ? 'Saving...'
                : userActionTarget?.type === 'delete'
                  ? 'Delete User'
                  : userActionTarget?.nextIsActive
                    ? 'Activate User'
                    : 'Deactivate User'}
            </button>
          </div>
        </div>
      </Modal>
    )
  }

  function GroupEditor() {
    return (
      <Drawer
        open={groupDrawerOpen}
        title={editingGroup ? 'Edit group' : 'Add group'}
        description="Keep assignment groups, members, and the team lead connected to real workforce users."
        onClose={() => setGroupDrawerOpen(false)}
      >
        <div className="space-y-5">
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Group name"><input value={groupForm.name} onChange={(event) => setGroupForm((current) => ({ ...current, name: event.target.value }))} className="field-input" /></Field>
            <Field label="Type"><input value={groupForm.skillArea} onChange={(event) => setGroupForm((current) => ({ ...current, skillArea: event.target.value }))} className="field-input" placeholder="e.g. Electrical, HVAC, Civil" /></Field>
          </div>
          <Field label="Branch">
            <select value={groupForm.branchId} onChange={(event) => setGroupForm((current) => ({ ...current, branchId: event.target.value }))} className="field-input">
              <option value="">All branches</option>
              {data.branches.map((branch) => (
                <option key={branch.id} value={branch.id}>{branch.name}</option>
              ))}
            </select>
          </Field>
          <Field label="Description"><textarea value={groupForm.description} onChange={(event) => setGroupForm((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[110px]" /></Field>
          <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
            <span className="text-sm text-app">Group is active</span>
            <input type="checkbox" checked={groupForm.isActive} onChange={(event) => setGroupForm((current) => ({ ...current, isActive: event.target.checked }))} />
          </label>

          <Field label="Members">
            <div className="grid gap-2">
              {data.users.map((user) => (
                <label key={user.id} className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                  <div className="flex items-center gap-3">
                    <Avatar name={user.fullName} size="sm" />
                    <div>
                      <p className="text-sm font-semibold text-app">{user.fullName}</p>
                      <p className="mt-1 text-xs text-muted">{user.defaultBranchName || formatBranchLabel(user, data.branches)}</p>
                    </div>
                  </div>
                  <input
                    type="checkbox"
                    checked={groupForm.memberUserIds.includes(user.id)}
                    onChange={(event) =>
                      setGroupForm((current) => ({
                        ...current,
                        memberUserIds: event.target.checked
                          ? [...current.memberUserIds, user.id]
                          : current.memberUserIds.filter((id) => id !== user.id),
                        teamLeadUserId: !event.target.checked && current.teamLeadUserId === user.id ? '' : current.teamLeadUserId,
                      }))
                    }
                  />
                </label>
              ))}
              {data.users.length === 0 ? <div className="text-sm text-muted">No users are available for group membership yet.</div> : null}
            </div>
          </Field>

          <Field label="Team lead">
            <select value={groupForm.teamLeadUserId} onChange={(event) => setGroupForm((current) => ({ ...current, teamLeadUserId: event.target.value }))} className="field-input">
              <option value="">No team lead selected</option>
              {groupForm.memberUserIds.map((userId) => {
                const user = usersById.get(userId)
                if (!user) return null
                return <option key={user.id} value={user.id}>{user.fullName}</option>
              })}
            </select>
          </Field>

          <div className="flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => setGroupDrawerOpen(false)}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void saveGroup()}>Save group</button>
          </div>
        </div>
      </Drawer>
    )
  }
}

function asTab(value: string | null, pathname?: string): WorkforceTab {
  if (pathname?.endsWith('/assignment-groups')) {
    return 'groups'
  }

  if (value === 'groups' || value === 'roles') {
    return value
  }

  return 'users'
}

function formatBranchLabel(user: UserRecord, branches: BranchRecord[]) {
  if (user.hasAllBranchAccess) {
    return 'All branches'
  }

  if (user.defaultBranchName) {
    return user.defaultBranchName
  }

  if (user.branchIds.length === 1) {
    return branches.find((branch) => branch.id === user.branchIds[0])?.name ?? '1 branch'
  }

  return user.branchIds.length > 0 ? `${user.branchIds.length} branches` : 'No branch assigned'
}

function toErrorMessage(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}
