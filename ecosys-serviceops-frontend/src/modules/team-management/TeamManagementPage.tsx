import { Plus, Power, Search } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { Avatar } from '../../components/ui/Avatar'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { DetailRows, PageTabs, SectionPanel, StatusBadge } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { branchService } from '../../services/branchService'
import { settingsService } from '../../services/settingsService'
import { teamManagementService } from '../../services/teamManagementService'
import { userService } from '../../services/userService'
import type {
  ApiPermissions,
  AssignmentGroupRecord,
  BranchRecord,
  CertificationDefinitionRecord,
  TeamManagementOverviewRecord,
  TeamManagementProfileRecord,
  UpsertAssignmentGroupInput,
  UpsertCertificationDefinitionInput,
  UpsertUserInput,
  UpdateTeamManagementProfileInput,
  UserRecord,
} from '../../types/api'

type TeamManagementTab = 'people' | 'roles' | 'groups' | 'readiness'

type TeamManagementPayload = {
  users: UserRecord[]
  branches: BranchRecord[]
  groups: AssignmentGroupRecord[]
  overview: TeamManagementOverviewRecord
  usersError: string | null
  branchesError: string | null
  groupsError: string | null
  overviewError: string | null
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

type ProfileEditorState = {
  availabilityStatus: string
  availabilityNote: string
  availabilityUntil: string
  certifications: Array<{
    certificationId: string
    certificateNumber: string
    issuedOn: string
    expiresOn: string
    notes: string
  }>
}

type RoleEditorState = {
  role: string
  permissions: ApiPermissions
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

const emptyPayload: TeamManagementPayload = {
  users: [],
  branches: [],
  groups: [],
  overview: {
    profiles: [],
    certifications: [],
  },
  usersError: null,
  branchesError: null,
  groupsError: null,
  overviewError: null,
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

function defaultProfileForm(): ProfileEditorState {
  return {
    availabilityStatus: 'Available',
    availabilityNote: '',
    availabilityUntil: '',
    certifications: [],
  }
}

function defaultRoleForm(): RoleEditorState {
  return {
    role: 'User',
    permissions: emptyPermissions,
  }
}

export function TeamManagementPage() {
  const { pushToast } = useToast()
  const { session } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab = asTab(searchParams.get('tab'))
  const [personSearch, setPersonSearch] = useState('')
  const [personStatusFilter, setPersonStatusFilter] = useState('All')
  const [groupSearch, setGroupSearch] = useState('')
  const [groupStatusFilter, setGroupStatusFilter] = useState('All')
  const [readinessSearch, setReadinessSearch] = useState('')
  const [readinessFilter, setReadinessFilter] = useState('All')
  const [selectedRoleUserId, setSelectedRoleUserId] = useState('')
  const [userDrawerOpen, setUserDrawerOpen] = useState(false)
  const [editingUser, setEditingUser] = useState<UserRecord | null>(null)
  const [userForm, setUserForm] = useState<UpsertUserInput>(defaultUserForm)
  const [isSavingUser, setIsSavingUser] = useState(false)
  const [savingUserStatusId, setSavingUserStatusId] = useState<string | null>(null)
  const [groupDrawerOpen, setGroupDrawerOpen] = useState(false)
  const [editingGroup, setEditingGroup] = useState<AssignmentGroupRecord | null>(null)
  const [groupForm, setGroupForm] = useState<GroupEditorState>(defaultGroupForm)
  const [isSavingGroup, setIsSavingGroup] = useState(false)
  const [profileDrawerOpen, setProfileDrawerOpen] = useState(false)
  const [editingProfile, setEditingProfile] = useState<TeamManagementProfileRecord | null>(null)
  const [profileForm, setProfileForm] = useState<ProfileEditorState>(defaultProfileForm)
  const [isSavingProfile, setIsSavingProfile] = useState(false)
  const [roleForm, setRoleForm] = useState<RoleEditorState>(defaultRoleForm)
  const [isSavingRole, setIsSavingRole] = useState(false)
  const [certificationDrawerOpen, setCertificationDrawerOpen] = useState(false)
  const [editingCertification, setEditingCertification] = useState<CertificationDefinitionRecord | null>(null)
  const [certificationForm, setCertificationForm] = useState<UpsertCertificationDefinitionInput>({ name: '', issuer: '', description: '', renewalPeriodMonths: null, isActive: true })
  const [isSavingCertification, setIsSavingCertification] = useState(false)

  const { data, loading, reload } = useAsyncData<TeamManagementPayload>(
    async (signal) => {
      const [usersResult, branchesResult, groupsResult, overviewResult] = await Promise.allSettled([
        userService.list(signal),
        branchService.list(signal),
        settingsService.listAssignmentGroups(signal),
        teamManagementService.getOverview(signal),
      ])

      return {
        users: usersResult.status === 'fulfilled' ? usersResult.value : [],
        branches: branchesResult.status === 'fulfilled' ? branchesResult.value : [],
        groups: groupsResult.status === 'fulfilled' ? groupsResult.value : [],
        overview: overviewResult.status === 'fulfilled' ? overviewResult.value : emptyPayload.overview,
        usersError: usersResult.status === 'rejected' ? toErrorMessage(usersResult.reason, 'Unable to load users.') : null,
        branchesError: branchesResult.status === 'rejected' ? toErrorMessage(branchesResult.reason, 'Unable to load branches.') : null,
        groupsError: groupsResult.status === 'rejected' ? toErrorMessage(groupsResult.reason, 'Unable to load assignment groups.') : null,
        overviewError: overviewResult.status === 'rejected' ? toErrorMessage(overviewResult.reason, 'Unable to load team readiness.') : null,
      }
    },
    emptyPayload,
    [session?.tenantId],
  )

  const loadWarnings = useMemo(
    () => [data.usersError, data.branchesError, data.groupsError, data.overviewError].filter((value): value is string => Boolean(value)),
    [data.branchesError, data.groupsError, data.overviewError, data.usersError],
  )

  const profilesByUserId = useMemo(() => new Map(data.overview.profiles.map((profile) => [profile.userId, profile])), [data.overview.profiles])

  const peopleRows = useMemo(
    () =>
      data.users.filter((user) => {
        const profile = profilesByUserId.get(user.id)
        const haystack = [
          user.fullName,
          user.email,
          user.role,
          user.defaultBranchName ?? '',
          user.assignmentGroups.map((group) => group.name).join(' '),
          profile?.readinessStatus ?? '',
        ]
          .join(' ')
          .toLowerCase()

        const matchesSearch = !personSearch.trim() || haystack.includes(personSearch.trim().toLowerCase())
        const matchesStatus =
          personStatusFilter === 'All' ||
          (personStatusFilter === 'Active' && user.isActive) ||
          (personStatusFilter === 'Inactive' && !user.isActive)

        return matchesSearch && matchesStatus
      }),
    [data.users, personSearch, personStatusFilter, profilesByUserId],
  )

  const groupRows = useMemo(
    () =>
      data.groups.filter((group) => {
        const haystack = [
          group.name,
          group.description ?? '',
          group.branchName ?? '',
          group.members.map((member) => member.memberName ?? member.email ?? '').join(' '),
        ]
          .join(' ')
          .toLowerCase()

        const matchesSearch = !groupSearch.trim() || haystack.includes(groupSearch.trim().toLowerCase())
        const matchesStatus =
          groupStatusFilter === 'All' ||
          (groupStatusFilter === 'Active' && group.isActive) ||
          (groupStatusFilter === 'Inactive' && !group.isActive)

        return matchesSearch && matchesStatus
      }),
    [data.groups, groupSearch, groupStatusFilter],
  )

  const readinessRows = useMemo(
    () =>
      data.overview.profiles.filter((profile) => {
        const haystack = [
          profile.fullName,
          profile.email,
          profile.role,
          profile.branchName ?? '',
          profile.availabilityStatus,
          profile.readinessStatus,
          profile.assignmentGroups.map((group) => group.name).join(' '),
          profile.certifications.map((certification) => certification.certificationName).join(' '),
        ]
          .join(' ')
          .toLowerCase()

        const matchesSearch = !readinessSearch.trim() || haystack.includes(readinessSearch.trim().toLowerCase())
        const matchesFilter = readinessFilter === 'All' || profile.readinessStatus === readinessFilter
        return matchesSearch && matchesFilter
      }),
    [data.overview.profiles, readinessFilter, readinessSearch],
  )

  const roleOptions = useMemo(() => {
    const defaults = ['Admin', 'Technician', 'User']
    const discovered = data.users.map((user) => user.role).filter(Boolean)
    return Array.from(new Set([...defaults, ...discovered])).sort((left, right) => left.localeCompare(right))
  }, [data.users])

  const selectedRoleUser = useMemo(
    () => data.users.find((user) => user.id === selectedRoleUserId) ?? data.users[0] ?? null,
    [data.users, selectedRoleUserId],
  )

  useEffect(() => {
    if (!selectedRoleUser) {
      setRoleForm(defaultRoleForm())
      return
    }

    setRoleForm({
      role: selectedRoleUser.role,
      permissions: normalizePermissions(selectedRoleUser.permissions),
    })
  }, [selectedRoleUser])

  if (loading) {
    return <LoadingState label="Loading Team Management workspace..." />
  }

  if (!data.users.length && !data.groups.length && !data.overview.profiles.length && loadWarnings.length > 0) {
    return <ErrorState title="Unable to load Team Management" description={loadWarnings[0]} onRetry={() => void reload()} />
  }

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="ServiceOps"
        title="Team Management"
        description="Manage people, access, assignment groups, certifications, availability, and assignment readiness from one operational workspace."
      />

      {loadWarnings.length > 0 ? (
        <SectionPanel title="Load warnings" description="Some supporting records could not be refreshed. The workspace is still usable, but a few sections may be incomplete.">
          <div className="space-y-2">
            {loadWarnings.map((warning) => (
              <p key={warning} className="text-sm text-[var(--app-danger)]">
                {warning}
              </p>
            ))}
          </div>
        </SectionPanel>
      ) : null}

      <SectionPanel title="Workspace" description="Roles stay intact, assignment groups remain the operating team structure, and certifications are managed directly against users.">
        <PageTabs
          tabs={[
            { id: 'people', label: 'People' },
            { id: 'roles', label: 'Roles' },
            { id: 'groups', label: 'Assignment Groups' },
            { id: 'readiness', label: 'Readiness' },
          ]}
          activeTab={activeTab}
          onChange={(tab) => setSearchParams({ tab })}
        />
      </SectionPanel>

      {activeTab === 'people' ? (
        <PeopleSection
          rows={peopleRows}
          profilesByUserId={profilesByUserId}
          personSearch={personSearch}
          personStatusFilter={personStatusFilter}
          onSearchChange={setPersonSearch}
          onStatusFilterChange={setPersonStatusFilter}
          onCreate={() => openUserEditor(null, setEditingUser, setUserForm, setUserDrawerOpen)}
          onEdit={(user) => openUserEditor(user, setEditingUser, setUserForm, setUserDrawerOpen)}
          onToggleStatus={(user) => void toggleUserStatus(user)}
          savingUserStatusId={savingUserStatusId}
        />
      ) : null}

      {activeTab === 'roles' ? (
        <RolesSection
          users={data.users}
          selectedUser={selectedRoleUser}
          roleForm={roleForm}
          roleOptions={roleOptions}
          saving={isSavingRole}
          onSelectUser={setSelectedRoleUserId}
          onRoleChange={(role) => setRoleForm((current) => ({ ...current, role }))}
          onPermissionChange={(key, checked) =>
            setRoleForm((current) => ({
              ...current,
              permissions: {
                ...current.permissions,
                [key]: checked,
              },
            }))
          }
          onSave={() => void saveRoleConfiguration()}
        />
      ) : null}

      {activeTab === 'groups' ? (
        <GroupsSection
          rows={groupRows}
          branches={data.branches}
          groupSearch={groupSearch}
          groupStatusFilter={groupStatusFilter}
          onSearchChange={setGroupSearch}
          onStatusFilterChange={setGroupStatusFilter}
          onCreate={() => openGroupEditor(null, setEditingGroup, setGroupForm, setGroupDrawerOpen)}
          onEdit={(group) => openGroupEditor(group, setEditingGroup, setGroupForm, setGroupDrawerOpen)}
          onDeactivate={(group) => void deleteGroup(group)}
        />
      ) : null}

      {activeTab === 'readiness' ? (
        <ReadinessSection
          profiles={readinessRows}
          certificationCatalog={data.overview.certifications}
          readinessSearch={readinessSearch}
          readinessFilter={readinessFilter}
          onSearchChange={setReadinessSearch}
          onFilterChange={setReadinessFilter}
          onManage={(profile) => openProfileEditor(profile, setEditingProfile, setProfileForm, setProfileDrawerOpen)}
          onCreateCertification={() => openCertificationEditor(null, setEditingCertification, setCertificationForm, setCertificationDrawerOpen)}
          onEditCertification={(certification) => openCertificationEditor(certification, setEditingCertification, setCertificationForm, setCertificationDrawerOpen)}
          onDeleteCertification={(certification) => void deleteCertification(certification)}
        />
      ) : null}

      <Drawer
        open={userDrawerOpen}
        title={editingUser ? 'Edit person' : 'Add person'}
        description="Keep user access, branch reach, and assignment group placement aligned to the operating model."
        onClose={() => setUserDrawerOpen(false)}
      >
        <div className="space-y-5">
          <section className="grid gap-3 md:grid-cols-2">
            <Field label="Full name">
              <input value={userForm.fullName} onChange={(event) => setUserForm((current) => ({ ...current, fullName: event.target.value }))} className="field-input" />
            </Field>
            <Field label="Email">
              <input type="email" value={userForm.email} onChange={(event) => setUserForm((current) => ({ ...current, email: event.target.value }))} className="field-input" />
            </Field>
            <Field label="Phone">
              <input value={userForm.phoneNumber ?? ''} onChange={(event) => setUserForm((current) => ({ ...current, phoneNumber: event.target.value }))} className="field-input" />
            </Field>
            <Field label="Role">
              <select value={userForm.role} onChange={(event) => setUserForm((current) => ({ ...current, role: event.target.value }))} className="field-input">
                {roleOptions.map((role) => (
                  <option key={role} value={role}>
                    {role}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Job title">
              <input value={userForm.jobTitle ?? ''} onChange={(event) => setUserForm((current) => ({ ...current, jobTitle: event.target.value }))} className="field-input" />
            </Field>
            <Field label="Department">
              <input value={userForm.department ?? ''} onChange={(event) => setUserForm((current) => ({ ...current, department: event.target.value }))} className="field-input" />
            </Field>
            {!editingUser ? (
              <Field label="Temporary password">
                <input type="password" value={userForm.password ?? ''} onChange={(event) => setUserForm((current) => ({ ...current, password: event.target.value }))} className="field-input" />
              </Field>
            ) : null}
            <Field label="Default branch">
              <select value={userForm.defaultBranchId ?? ''} onChange={(event) => setUserForm((current) => ({ ...current, defaultBranchId: event.target.value || null }))} className="field-input">
                <option value="">Choose a branch</option>
                {data.branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))}
              </select>
            </Field>
          </section>

          <section className="space-y-3">
            <div>
              <h3 className="text-sm font-semibold text-app">Branch access</h3>
              <p className="mt-1 text-sm text-muted">Choose which branches this person can work across. Assignment group logic remains unchanged.</p>
            </div>
            <Toggle
              label="All branch access"
              checked={userForm.hasAllBranchAccess}
              onChange={(checked) => setUserForm((current) => ({ ...current, hasAllBranchAccess: checked }))}
            />
            <div className="grid gap-2 md:grid-cols-2">
              {data.branches.map((branch) => (
                <Toggle
                  key={branch.id}
                  label={branch.name}
                  helper={branch.code}
                  checked={Boolean(userForm.branchIds?.includes(branch.id))}
                  disabled={userForm.hasAllBranchAccess}
                  onChange={(checked) =>
                    setUserForm((current) => ({
                      ...current,
                      branchIds: checked
                        ? Array.from(new Set([...(current.branchIds ?? []), branch.id]))
                        : (current.branchIds ?? []).filter((value) => value !== branch.id),
                    }))
                  }
                />
              ))}
            </div>
          </section>

          <section className="space-y-3">
            <div>
              <h3 className="text-sm font-semibold text-app">Assignment groups</h3>
              <p className="mt-1 text-sm text-muted">Assignment groups are the operating team structure. Add this person to the queues they should serve.</p>
            </div>
            <div className="grid gap-2 md:grid-cols-2">
              {data.groups.map((group) => (
                <Toggle
                  key={group.id}
                  label={group.name}
                  helper={group.branchName ?? 'No branch'}
                  checked={Boolean(userForm.assignmentGroupIds?.includes(group.id))}
                  onChange={(checked) =>
                    setUserForm((current) => ({
                      ...current,
                      assignmentGroupIds: checked
                        ? Array.from(new Set([...(current.assignmentGroupIds ?? []), group.id]))
                        : (current.assignmentGroupIds ?? []).filter((value) => value !== group.id),
                    }))
                  }
                />
              ))}
            </div>
          </section>

          <Toggle
            label="User account active"
            checked={Boolean(userForm.isActive)}
            onChange={(checked) => setUserForm((current) => ({ ...current, isActive: checked }))}
          />

          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" className="button-secondary" onClick={() => setUserDrawerOpen(false)} disabled={isSavingUser}>
              Cancel
            </button>
            <button type="button" className="button-primary" onClick={() => void saveUser()} disabled={isSavingUser}>
              {isSavingUser ? 'Saving...' : editingUser ? 'Save person' : 'Create person'}
            </button>
          </div>
        </div>
      </Drawer>

      <Drawer
        open={groupDrawerOpen}
        title={editingGroup ? 'Edit assignment group' : 'Add assignment group'}
        description="Assignment groups are the working team structure for dispatch and routing."
        onClose={() => setGroupDrawerOpen(false)}
      >
        <div className="space-y-5">
          <section className="grid gap-3 md:grid-cols-2">
            <Field label="Name">
              <input value={groupForm.name} onChange={(event) => setGroupForm((current) => ({ ...current, name: event.target.value }))} className="field-input" />
            </Field>
            <Field label="Branch">
              <select value={groupForm.branchId} onChange={(event) => setGroupForm((current) => ({ ...current, branchId: event.target.value }))} className="field-input">
                <option value="">No branch</option>
                {data.branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Description" className="md:col-span-2">
              <textarea value={groupForm.description} onChange={(event) => setGroupForm((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[120px]" />
            </Field>
          </section>

          <section className="space-y-3">
            <div>
              <h3 className="text-sm font-semibold text-app">Members</h3>
              <p className="mt-1 text-sm text-muted">Select the people who should operate inside this assignment group.</p>
            </div>
            <div className="grid gap-2 md:grid-cols-2">
              {data.users.map((user) => (
                <Toggle
                  key={user.id}
                  label={user.fullName}
                  helper={user.role}
                  checked={groupForm.memberUserIds.includes(user.id)}
                  onChange={(checked) =>
                    setGroupForm((current) => ({
                      ...current,
                      memberUserIds: checked
                        ? Array.from(new Set([...current.memberUserIds, user.id]))
                        : current.memberUserIds.filter((value) => value !== user.id),
                      teamLeadUserId: !checked && current.teamLeadUserId === user.id ? '' : current.teamLeadUserId,
                    }))
                  }
                />
              ))}
            </div>
          </section>

          <section className="space-y-3">
            <div>
              <h3 className="text-sm font-semibold text-app">Lead</h3>
              <p className="mt-1 text-sm text-muted">Choose one queue lead if the group has an operational owner.</p>
            </div>
            <div className="grid gap-2">
              <label className="flex items-center gap-3 rounded-[var(--radius-control)] border border-app bg-[var(--app-surface)] px-3 py-2.5">
                <input
                  type="radio"
                  checked={!groupForm.teamLeadUserId}
                  onChange={() => setGroupForm((current) => ({ ...current, teamLeadUserId: '' }))}
                />
                <span className="text-sm text-app">No lead assigned</span>
              </label>
              {groupForm.memberUserIds.map((userId) => {
                const user = data.users.find((candidate) => candidate.id === userId)
                if (!user) {
                  return null
                }

                return (
                  <label key={user.id} className="flex items-center gap-3 rounded-[var(--radius-control)] border border-app bg-[var(--app-surface)] px-3 py-2.5">
                    <input
                      type="radio"
                      checked={groupForm.teamLeadUserId === user.id}
                      onChange={() => setGroupForm((current) => ({ ...current, teamLeadUserId: user.id }))}
                    />
                    <span className="text-sm text-app">{user.fullName}</span>
                  </label>
                )
              })}
            </div>
          </section>

          <Toggle
            label="Assignment group active"
            checked={groupForm.isActive}
            onChange={(checked) => setGroupForm((current) => ({ ...current, isActive: checked }))}
          />

          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" className="button-secondary" onClick={() => setGroupDrawerOpen(false)} disabled={isSavingGroup}>
              Cancel
            </button>
            <button type="button" className="button-primary" onClick={() => void saveGroup()} disabled={isSavingGroup}>
              {isSavingGroup ? 'Saving...' : editingGroup ? 'Save group' : 'Create group'}
            </button>
          </div>
        </div>
      </Drawer>

      <Drawer
        open={profileDrawerOpen}
        title={editingProfile ? `Readiness for ${editingProfile.fullName}` : 'Readiness'}
        description="Availability and certifications are managed directly against the user while assignment groups continue to drive dispatch routing."
        onClose={() => setProfileDrawerOpen(false)}
      >
        <div className="space-y-5">
          <section className="grid gap-3 md:grid-cols-2">
            <Field label="Availability status">
              <select value={profileForm.availabilityStatus} onChange={(event) => setProfileForm((current) => ({ ...current, availabilityStatus: event.target.value }))} className="field-input">
                <option value="Available">Available</option>
                <option value="Limited">Limited</option>
                <option value="Unavailable">Unavailable</option>
              </select>
            </Field>
            <Field label="Availability until">
              <input type="datetime-local" value={profileForm.availabilityUntil} onChange={(event) => setProfileForm((current) => ({ ...current, availabilityUntil: event.target.value }))} className="field-input" />
            </Field>
            <Field label="Availability note" className="md:col-span-2">
              <textarea value={profileForm.availabilityNote} onChange={(event) => setProfileForm((current) => ({ ...current, availabilityNote: event.target.value }))} className="field-input min-h-[120px]" />
            </Field>
          </section>

          <section className="space-y-3">
            <div>
              <h3 className="text-sm font-semibold text-app">Certifications</h3>
              <p className="mt-1 text-sm text-muted">Attach certifications to the user wherever assignment readiness depends on them.</p>
            </div>
            <div className="space-y-3">
              {data.overview.certifications.filter((item) => item.isActive).map((certification) => {
                const assignment = profileForm.certifications.find((item) => item.certificationId === certification.id)
                const checked = Boolean(assignment)

                return (
                  <div key={certification.id} className="rounded-[var(--radius-control)] border border-app bg-[var(--app-surface)] p-3">
                    <Toggle
                      label={certification.name}
                      helper={certification.issuer ?? 'No issuer'}
                      checked={checked}
                      onChange={(isChecked) => updateProfileCertification(certification.id, isChecked)}
                    />
                    {checked && assignment ? (
                      <div className="mt-3 grid gap-3 md:grid-cols-2">
                        <Field label="Certificate number">
                          <input
                            value={assignment.certificateNumber}
                            onChange={(event) => patchProfileCertification(certification.id, { certificateNumber: event.target.value })}
                            className="field-input"
                          />
                        </Field>
                        <Field label="Issued on">
                          <input
                            type="date"
                            value={assignment.issuedOn}
                            onChange={(event) => patchProfileCertification(certification.id, { issuedOn: event.target.value })}
                            className="field-input"
                          />
                        </Field>
                        <Field label="Expires on">
                          <input
                            type="date"
                            value={assignment.expiresOn}
                            onChange={(event) => patchProfileCertification(certification.id, { expiresOn: event.target.value })}
                            className="field-input"
                          />
                        </Field>
                        <Field label="Notes" className="md:col-span-2">
                          <textarea
                            value={assignment.notes}
                            onChange={(event) => patchProfileCertification(certification.id, { notes: event.target.value })}
                            className="field-input min-h-[100px]"
                          />
                        </Field>
                      </div>
                    ) : null}
                  </div>
                )
              })}
            </div>
          </section>

          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" className="button-secondary" onClick={() => setProfileDrawerOpen(false)} disabled={isSavingProfile}>
              Cancel
            </button>
            <button type="button" className="button-primary" onClick={() => void saveProfile()} disabled={isSavingProfile}>
              {isSavingProfile ? 'Saving...' : 'Save readiness'}
            </button>
          </div>
        </div>
      </Drawer>

      <Drawer
        open={certificationDrawerOpen}
        title={editingCertification ? 'Edit certification' : 'Add certification'}
        description="Maintain the certification catalog that can be attached to users where readiness depends on proof of competence or compliance."
        onClose={() => setCertificationDrawerOpen(false)}
      >
        <div className="space-y-5">
          <section className="grid gap-3 md:grid-cols-2">
            <Field label="Name">
              <input value={certificationForm.name} onChange={(event) => setCertificationForm((current) => ({ ...current, name: event.target.value }))} className="field-input" />
            </Field>
            <Field label="Issuer">
              <input value={certificationForm.issuer ?? ''} onChange={(event) => setCertificationForm((current) => ({ ...current, issuer: event.target.value }))} className="field-input" />
            </Field>
            <Field label="Renewal period (months)">
              <input
                type="number"
                min={0}
                value={certificationForm.renewalPeriodMonths ?? ''}
                onChange={(event) => setCertificationForm((current) => ({ ...current, renewalPeriodMonths: event.target.value ? Number(event.target.value) : null }))}
                className="field-input"
              />
            </Field>
            <Field label="Description" className="md:col-span-2">
              <textarea value={certificationForm.description ?? ''} onChange={(event) => setCertificationForm((current) => ({ ...current, description: event.target.value }))} className="field-input min-h-[120px]" />
            </Field>
          </section>

          <Toggle
            label="Certification active"
            checked={certificationForm.isActive}
            onChange={(checked) => setCertificationForm((current) => ({ ...current, isActive: checked }))}
          />

          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" className="button-secondary" onClick={() => setCertificationDrawerOpen(false)} disabled={isSavingCertification}>
              Cancel
            </button>
            <button type="button" className="button-primary" onClick={() => void saveCertification()} disabled={isSavingCertification}>
              {isSavingCertification ? 'Saving...' : editingCertification ? 'Save certification' : 'Create certification'}
            </button>
          </div>
        </div>
      </Drawer>
    </div>
  )

  async function saveUser() {
    if (!userForm.fullName.trim() || !userForm.email.trim() || !userForm.role.trim()) {
      pushToast({ title: 'Missing details', description: 'Full name, email, and role are required.', tone: 'warning' })
      return
    }

    if (!editingUser && !(userForm.password ?? '').trim()) {
      pushToast({ title: 'Temporary password required', description: 'New users need a temporary password or invite path.', tone: 'warning' })
      return
    }

    setIsSavingUser(true)
    try {
      if (editingUser) {
        await userService.update(editingUser.id, userForm)
      } else {
        await userService.create(userForm)
      }

      setUserDrawerOpen(false)
      await reload()
      pushToast({ title: editingUser ? 'Person updated' : 'Person created', description: 'User access and assignment group placement were saved.', tone: 'success' })
    } catch (error) {
      pushToast({ title: 'Save failed', description: toErrorMessage(error, 'Unable to save this person.'), tone: 'danger' })
    } finally {
      setIsSavingUser(false)
    }
  }

  async function toggleUserStatus(user: UserRecord) {
    setSavingUserStatusId(user.id)
    try {
      await userService.updateStatus(user.id, !user.isActive)
      await reload()
      pushToast({
        title: user.isActive ? 'User deactivated' : 'User reactivated',
        description: `${user.fullName} was ${user.isActive ? 'removed from active operations' : 'returned to active operations'}.`,
        tone: 'success',
      })
    } catch (error) {
      pushToast({ title: 'Status update failed', description: toErrorMessage(error, 'Unable to change user status.'), tone: 'danger' })
    } finally {
      setSavingUserStatusId(null)
    }
  }

  async function saveRoleConfiguration() {
    if (!selectedRoleUser) {
      return
    }

    setIsSavingRole(true)
    try {
      await userService.update(selectedRoleUser.id, {
        fullName: selectedRoleUser.fullName,
        email: selectedRoleUser.email,
        phoneNumber: selectedRoleUser.phoneNumber,
        role: roleForm.role,
        jobTitle: selectedRoleUser.jobTitle,
        department: selectedRoleUser.department,
        isActive: selectedRoleUser.isActive,
        permissions: roleForm.permissions,
        branchIds: selectedRoleUser.branchIds,
        defaultBranchId: selectedRoleUser.defaultBranchId,
        hasAllBranchAccess: selectedRoleUser.hasAllBranchAccess,
        assignmentGroupIds: selectedRoleUser.assignmentGroups.map((group) => group.id),
      })

      await reload()
      pushToast({ title: 'Role access saved', description: 'Role and permission settings were updated without changing the underlying permission model.', tone: 'success' })
    } catch (error) {
      pushToast({ title: 'Save failed', description: toErrorMessage(error, 'Unable to save role configuration.'), tone: 'danger' })
    } finally {
      setIsSavingRole(false)
    }
  }

  async function saveGroup() {
    if (!groupForm.name.trim()) {
      pushToast({ title: 'Group name required', description: 'Assignment groups need a clear operating name.', tone: 'warning' })
      return
    }

    const input: UpsertAssignmentGroupInput = {
      name: groupForm.name.trim(),
      description: nullable(groupForm.description),
      skillArea: nullable(groupForm.skillArea),
      branchId: groupForm.branchId || null,
      isActive: groupForm.isActive,
      technicianIds: [],
      members: groupForm.memberUserIds.map((userId) => ({
        userId,
        isLead: groupForm.teamLeadUserId === userId,
      })),
    }

    setIsSavingGroup(true)
    try {
      if (editingGroup) {
        await settingsService.updateAssignmentGroup(editingGroup.id, input)
      } else {
        await settingsService.createAssignmentGroup(input)
      }

      setGroupDrawerOpen(false)
      await reload()
      pushToast({ title: editingGroup ? 'Assignment group updated' : 'Assignment group created', description: 'Dispatch routing structure was saved.', tone: 'success' })
    } catch (error) {
      pushToast({ title: 'Save failed', description: toErrorMessage(error, 'Unable to save assignment group.'), tone: 'danger' })
    } finally {
      setIsSavingGroup(false)
    }
  }

  async function deleteGroup(group: AssignmentGroupRecord) {
    try {
      await settingsService.deleteAssignmentGroup(group.id)
      await reload()
      pushToast({ title: 'Assignment group archived', description: `${group.name} is no longer active.`, tone: 'success' })
    } catch (error) {
      pushToast({ title: 'Archive failed', description: toErrorMessage(error, 'Unable to archive assignment group.'), tone: 'danger' })
    }
  }

  async function saveProfile() {
    if (!editingProfile) {
      return
    }

    const input: UpdateTeamManagementProfileInput = {
      availabilityStatus: profileForm.availabilityStatus,
      availabilityNote: nullable(profileForm.availabilityNote),
      availabilityUntil: toIsoDateTime(profileForm.availabilityUntil),
      certifications: profileForm.certifications.map((certification) => ({
        certificationId: certification.certificationId,
        certificateNumber: nullable(certification.certificateNumber),
        issuedOn: certification.issuedOn || null,
        expiresOn: certification.expiresOn || null,
        notes: nullable(certification.notes),
      })),
    }

    setIsSavingProfile(true)
    try {
      await teamManagementService.updateUserProfile(editingProfile.userId, input)
      setProfileDrawerOpen(false)
      await reload()
      pushToast({ title: 'Readiness updated', description: 'Availability and user certifications were saved.', tone: 'success' })
    } catch (error) {
      pushToast({ title: 'Save failed', description: toErrorMessage(error, 'Unable to save readiness profile.'), tone: 'danger' })
    } finally {
      setIsSavingProfile(false)
    }
  }

  async function saveCertification() {
    if (!certificationForm.name.trim()) {
      pushToast({ title: 'Certification name required', description: 'Add a name before saving this certification record.', tone: 'warning' })
      return
    }

    setIsSavingCertification(true)
    try {
      if (editingCertification) {
        await teamManagementService.updateCertification(editingCertification.id, certificationForm)
      } else {
        await teamManagementService.createCertification(certificationForm)
      }

      setCertificationDrawerOpen(false)
      await reload()
      pushToast({ title: editingCertification ? 'Certification updated' : 'Certification created', description: 'The user certification catalog has been saved.', tone: 'success' })
    } catch (error) {
      pushToast({ title: 'Save failed', description: toErrorMessage(error, 'Unable to save certification.'), tone: 'danger' })
    } finally {
      setIsSavingCertification(false)
    }
  }

  async function deleteCertification(certification: CertificationDefinitionRecord) {
    try {
      await teamManagementService.deleteCertification(certification.id)
      await reload()
      pushToast({ title: 'Certification archived', description: `${certification.name} is no longer active in the readiness catalog.`, tone: 'success' })
    } catch (error) {
      pushToast({ title: 'Archive failed', description: toErrorMessage(error, 'Unable to archive certification.'), tone: 'danger' })
    }
  }

  function updateProfileCertification(certificationId: string, checked: boolean) {
    setProfileForm((current) => ({
      ...current,
      certifications: checked
        ? [...current.certifications, { certificationId, certificateNumber: '', issuedOn: '', expiresOn: '', notes: '' }]
        : current.certifications.filter((item) => item.certificationId !== certificationId),
    }))
  }

  function patchProfileCertification(
    certificationId: string,
    updates: Partial<ProfileEditorState['certifications'][number]>,
  ) {
    setProfileForm((current) => ({
      ...current,
      certifications: current.certifications.map((item) => (item.certificationId === certificationId ? { ...item, ...updates } : item)),
    }))
  }
}

function PeopleSection({
  rows,
  profilesByUserId,
  personSearch,
  personStatusFilter,
  onSearchChange,
  onStatusFilterChange,
  onCreate,
  onEdit,
  onToggleStatus,
  savingUserStatusId,
}: {
  rows: UserRecord[]
  profilesByUserId: Map<string, TeamManagementProfileRecord>
  personSearch: string
  personStatusFilter: string
  onSearchChange: (value: string) => void
  onStatusFilterChange: (value: string) => void
  onCreate: () => void
  onEdit: (user: UserRecord) => void
  onToggleStatus: (user: UserRecord) => void
  savingUserStatusId: string | null
}) {
  return (
    <SectionPanel title="People" description="Create users, place them into assignment groups, and keep branch reach aligned to how dispatch really works.">
      <Toolbar
        searchValue={personSearch}
        searchPlaceholder="Search people, roles, branches, or assignment groups"
        onSearchChange={onSearchChange}
        filters={
          <Field label="Status">
            <select value={personStatusFilter} onChange={(event) => onStatusFilterChange(event.target.value)} className="field-input">
              <option value="All">All</option>
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
            </select>
          </Field>
        }
        actions={
          <button type="button" className="button-primary" onClick={onCreate}>
            <Plus className="h-4 w-4" />
            Add person
          </button>
        }
      />

      <div className="mt-4">
        <DataTable
          rows={rows}
          rowKey={(row) => row.id}
          columns={[
            {
              key: 'person',
              header: 'Person',
              cell: (row) => {
                const profile = profilesByUserId.get(row.id)
                return (
                  <div className="flex items-start gap-3">
                    <Avatar name={row.fullName} size="sm" />
                    <div className="min-w-0">
                      <div className="font-semibold text-app">{row.fullName}</div>
                      <div className="text-sm text-muted">{row.email}</div>
                      {profile ? <div className="mt-2"><StatusBadge tone={readinessTone(profile.readinessStatus)}>{profile.readinessStatus}</StatusBadge></div> : null}
                    </div>
                  </div>
                )
              },
            },
            {
              key: 'role',
              header: 'Role',
              cell: (row) => row.role,
            },
            {
              key: 'branch',
              header: 'Branch access',
              cell: (row) => (
                <div className="space-y-1">
                  <div>{row.defaultBranchName ?? 'No default branch'}</div>
                  <div className="text-xs text-muted">{row.hasAllBranchAccess ? 'All branches' : `${row.branchIds.length} assigned branch${row.branchIds.length === 1 ? '' : 'es'}`}</div>
                </div>
              ),
            },
            {
              key: 'groups',
              header: 'Assignment groups',
              cell: (row) => formatNamedMemberships(row.assignmentGroups),
            },
            {
              key: 'status',
              header: 'Access status',
              cell: (row) => <StatusBadge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</StatusBadge>,
            },
            {
              key: 'actions',
              header: 'Actions',
              cell: (row) => (
                <div className="flex flex-wrap gap-2">
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => onEdit(row)}>
                    Edit
                  </button>
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => onToggleStatus(row)} disabled={savingUserStatusId === row.id}>
                    <Power className="h-4 w-4" />
                    {savingUserStatusId === row.id ? 'Saving...' : row.isActive ? 'Deactivate' : 'Reactivate'}
                  </button>
                </div>
              ),
            },
          ]}
          mobileCard={(row) => {
            const profile = profilesByUserId.get(row.id)
            return (
              <div className="space-y-3">
                <div className="flex items-start gap-3">
                  <Avatar name={row.fullName} size="sm" />
                  <div className="min-w-0">
                    <div className="font-semibold text-app">{row.fullName}</div>
                    <div className="text-sm text-muted">{row.email}</div>
                  </div>
                </div>
                <DetailRows
                  items={[
                    { label: 'Role', value: row.role },
                    { label: 'Default branch', value: row.defaultBranchName ?? 'No default branch' },
                    { label: 'Assignment groups', value: formatNamedMemberships(row.assignmentGroups) },
                    { label: 'Readiness', value: profile ? <StatusBadge tone={readinessTone(profile.readinessStatus)}>{profile.readinessStatus}</StatusBadge> : 'No workforce profile' },
                  ]}
                />
              </div>
            )
          }}
        />
      </div>
    </SectionPanel>
  )
}

function RolesSection({
  users,
  selectedUser,
  roleForm,
  roleOptions,
  saving,
  onSelectUser,
  onRoleChange,
  onPermissionChange,
  onSave,
}: {
  users: UserRecord[]
  selectedUser: UserRecord | null
  roleForm: RoleEditorState
  roleOptions: string[]
  saving: boolean
  onSelectUser: (userId: string) => void
  onRoleChange: (role: string) => void
  onPermissionChange: (key: keyof ApiPermissions, checked: boolean) => void
  onSave: () => void
}) {
  return (
    <SectionPanel title="Roles and Permissions" description="Roles were moved here from Settings. You can adjust role labels and permission flags without changing the underlying access model.">
      <div className="grid gap-5 xl:grid-cols-[minmax(0,320px)_minmax(0,1fr)]">
        <div className="space-y-4">
          <Field label="Person">
            <select value={selectedUser?.id ?? ''} onChange={(event) => onSelectUser(event.target.value)} className="field-input">
              {users.map((user) => (
                <option key={user.id} value={user.id}>
                  {user.fullName}
                </option>
              ))}
            </select>
          </Field>
          {selectedUser ? (
            <DetailRows
              items={[
                { label: 'Current role', value: selectedUser.role },
                { label: 'Assignment groups', value: formatNamedMemberships(selectedUser.assignmentGroups) },
                { label: 'Default branch', value: selectedUser.defaultBranchName ?? 'No default branch' },
                { label: 'Account', value: <StatusBadge tone={selectedUser.isActive ? 'success' : 'neutral'}>{selectedUser.isActive ? 'Active' : 'Inactive'}</StatusBadge> },
              ]}
            />
          ) : (
            <p className="text-sm text-muted">No users available.</p>
          )}
        </div>

        <div className="space-y-5">
          <Field label="Role">
            <select value={roleForm.role} onChange={(event) => onRoleChange(event.target.value)} className="field-input">
              {roleOptions.map((role) => (
                <option key={role} value={role}>
                  {role}
                </option>
              ))}
            </select>
          </Field>

          <div className="space-y-3">
            <div>
              <h3 className="text-sm font-semibold text-app">Permission set</h3>
              <p className="mt-1 text-sm text-muted">These toggles update the selected user directly. Existing permission rules stay intact.</p>
            </div>
            <div className="grid gap-2 md:grid-cols-2">
              {permissionFields.map((field) => (
                <Toggle
                  key={field.key}
                  label={field.label}
                  checked={Boolean(roleForm.permissions[field.key])}
                  onChange={(checked) => onPermissionChange(field.key, checked)}
                />
              ))}
            </div>
          </div>

          <div className="flex justify-end">
            <button type="button" className="button-primary" onClick={onSave} disabled={!selectedUser || saving}>
              {saving ? 'Saving...' : 'Save role access'}
            </button>
          </div>
        </div>
      </div>
    </SectionPanel>
  )
}

function GroupsSection({
  rows,
  branches,
  groupSearch,
  groupStatusFilter,
  onSearchChange,
  onStatusFilterChange,
  onCreate,
  onEdit,
  onDeactivate,
}: {
  rows: AssignmentGroupRecord[]
  branches: BranchRecord[]
  groupSearch: string
  groupStatusFilter: string
  onSearchChange: (value: string) => void
  onStatusFilterChange: (value: string) => void
  onCreate: () => void
  onEdit: (group: AssignmentGroupRecord) => void
  onDeactivate: (group: AssignmentGroupRecord) => void
}) {
  return (
    <SectionPanel title="Assignment Groups" description="Assignment groups remain the dispatch and routing backbone. They are the team structure now surfaced inside Team Management.">
      <Toolbar
        searchValue={groupSearch}
        searchPlaceholder="Search assignment groups, branches, or members"
        onSearchChange={onSearchChange}
        filters={
          <>
            <Field label="Status">
              <select value={groupStatusFilter} onChange={(event) => onStatusFilterChange(event.target.value)} className="field-input">
                <option value="All">All</option>
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
              </select>
            </Field>
            <Field label="Branches">
              <input value={`${branches.length} loaded`} readOnly className="field-input" />
            </Field>
          </>
        }
        actions={
          <button type="button" className="button-primary" onClick={onCreate}>
            <Plus className="h-4 w-4" />
            Add assignment group
          </button>
        }
      />

      <div className="mt-4">
        <DataTable
          rows={rows}
          rowKey={(row) => row.id}
          columns={[
            {
              key: 'group',
              header: 'Assignment group',
              cell: (row) => (
                <div className="space-y-1">
                  <div className="font-semibold text-app">{row.name}</div>
                  <div className="text-sm text-muted">{row.description || 'No description'}</div>
                </div>
              ),
            },
            {
              key: 'branch',
              header: 'Branch',
              cell: (row) => row.branchName ?? 'No branch',
            },
            {
              key: 'members',
              header: 'Members',
              cell: (row) => (
                <div className="space-y-1">
                  <div>{row.members.length} member{row.members.length === 1 ? '' : 's'}</div>
                  <div className="text-xs text-muted">{row.members.slice(0, 3).map((member) => member.memberName ?? member.email ?? 'Unnamed').join(', ') || 'No members'}</div>
                </div>
              ),
            },
            {
              key: 'lead',
              header: 'Lead',
              cell: (row) => row.members.find((member) => member.isLead)?.memberName ?? 'No lead',
            },
            {
              key: 'status',
              header: 'Status',
              cell: (row) => <StatusBadge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</StatusBadge>,
            },
            {
              key: 'actions',
              header: 'Actions',
              cell: (row) => (
                <div className="flex flex-wrap gap-2">
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => onEdit(row)}>
                    Edit
                  </button>
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => onDeactivate(row)}>
                    {row.isActive ? 'Archive' : 'Keep inactive'}
                  </button>
                </div>
              ),
            },
          ]}
        />
      </div>
    </SectionPanel>
  )
}

function ReadinessSection({
  profiles,
  certificationCatalog,
  readinessSearch,
  readinessFilter,
  onSearchChange,
  onFilterChange,
  onManage,
  onCreateCertification,
  onEditCertification,
  onDeleteCertification,
}: {
  profiles: TeamManagementProfileRecord[]
  certificationCatalog: CertificationDefinitionRecord[]
  readinessSearch: string
  readinessFilter: string
  onSearchChange: (value: string) => void
  onFilterChange: (value: string) => void
  onManage: (profile: TeamManagementProfileRecord) => void
  onCreateCertification: () => void
  onEditCertification: (certification: CertificationDefinitionRecord) => void
  onDeleteCertification: (certification: CertificationDefinitionRecord) => void
}) {
  return (
    <div className="space-y-4">
      <SectionPanel title="Certification catalog" description="Certifications are attached directly to users wherever assignment readiness depends on them.">
        <div className="flex flex-wrap justify-end gap-2">
          <button type="button" className="button-primary" onClick={onCreateCertification}>
            <Plus className="h-4 w-4" />
            Add certification
          </button>
        </div>
        <div className="mt-4">
          <DataTable
            rows={certificationCatalog}
            rowKey={(row) => row.id}
            columns={[
              {
                key: 'name',
                header: 'Certification',
                cell: (row) => (
                  <div className="space-y-1">
                    <div className="font-semibold text-app">{row.name}</div>
                    <div className="text-sm text-muted">{row.description || 'No description'}</div>
                  </div>
                ),
              },
              {
                key: 'issuer',
                header: 'Issuer',
                cell: (row) => row.issuer ?? 'Not specified',
              },
              {
                key: 'renewal',
                header: 'Renewal',
                cell: (row) => (row.renewalPeriodMonths ? `${row.renewalPeriodMonths} months` : 'Not set'),
              },
              {
                key: 'status',
                header: 'Status',
                cell: (row) => <StatusBadge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</StatusBadge>,
              },
              {
                key: 'actions',
                header: 'Actions',
                cell: (row) => (
                  <div className="flex flex-wrap gap-2">
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => onEditCertification(row)}>
                      Edit
                    </button>
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => onDeleteCertification(row)}>
                      Archive
                    </button>
                  </div>
                ),
              },
            ]}
          />
        </div>
      </SectionPanel>

      <SectionPanel title="Assignment readiness" description="Review availability, workload, assignment-group coverage, and user certifications before dispatching work.">
        <Toolbar
          searchValue={readinessSearch}
          searchPlaceholder="Search people, readiness state, availability, groups, or certifications"
          onSearchChange={onSearchChange}
          filters={
            <Field label="Readiness">
              <select value={readinessFilter} onChange={(event) => onFilterChange(event.target.value)} className="field-input">
                <option value="All">All</option>
                <option value="Ready">Ready</option>
                <option value="Limited">Limited</option>
                <option value="Unavailable">Unavailable</option>
              </select>
            </Field>
          }
        />

        <div className="mt-4">
          <DataTable
            rows={profiles}
            rowKey={(row) => row.userId}
            columns={[
              {
                key: 'person',
                header: 'Person',
                cell: (row) => (
                  <div className="space-y-1">
                    <div className="font-semibold text-app">{row.fullName}</div>
                    <div className="text-sm text-muted">{row.email}</div>
                    <div className="text-xs text-muted">{row.branchName ?? 'No branch'}</div>
                  </div>
                ),
              },
              {
                key: 'readiness',
                header: 'Readiness',
                cell: (row) => (
                  <div className="space-y-2">
                    <StatusBadge tone={readinessTone(row.readinessStatus)}>{row.readinessStatus}</StatusBadge>
                    <div className="text-xs text-muted">{row.readinessReasons[0] ?? 'Clear for assignment'}</div>
                  </div>
                ),
              },
              {
                key: 'availability',
                header: 'Availability',
                cell: (row) => (
                  <div className="space-y-1">
                    <StatusBadge tone={availabilityTone(row.availabilityStatus)}>{row.availabilityStatus}</StatusBadge>
                    <div className="text-xs text-muted">{row.availabilityNote || 'No note'}</div>
                  </div>
                ),
              },
              {
                key: 'groups',
                header: 'Assignment groups',
                cell: (row) => formatNamedMemberships(row.assignmentGroups),
              },
              {
                key: 'certifications',
                header: 'Certifications',
                cell: (row) => (
                  <div className="space-y-1">
                    <div>{row.certifications.length} attached</div>
                    <div className="flex flex-wrap gap-1">
                      {row.certifications.slice(0, 2).map((certification) => (
                        <StatusBadge key={certification.certificationId} tone={certificationTone(certification.status)}>
                          {certification.status}
                        </StatusBadge>
                      ))}
                      {row.certifications.length === 0 ? <span className="text-xs text-muted">None</span> : null}
                    </div>
                  </div>
                ),
              },
              {
                key: 'load',
                header: 'Current load',
                cell: (row) => `${row.activeWorkOrderCount} active / ${row.pendingResponseCount} pending / ${row.onSiteCount} on site`,
              },
              {
                key: 'actions',
                header: 'Actions',
                cell: (row) => (
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => onManage(row)}>
                    Manage readiness
                  </button>
                ),
              },
            ]}
          />
        </div>
      </SectionPanel>
    </div>
  )
}

function Toolbar({
  searchValue,
  searchPlaceholder,
  onSearchChange,
  filters,
  actions,
}: {
  searchValue: string
  searchPlaceholder: string
  onSearchChange: (value: string) => void
  filters?: ReactNode
  actions?: ReactNode
}) {
  return (
    <div className="grid gap-3 xl:grid-cols-[minmax(0,1.7fr)_minmax(0,1fr)_auto] xl:items-end">
      <Field label="Search">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
          <input value={searchValue} onChange={(event) => onSearchChange(event.target.value)} className="field-input pl-10" placeholder={searchPlaceholder} />
        </div>
      </Field>
      <div className="grid gap-3 sm:grid-cols-2">{filters}</div>
      {actions ? <div className="flex flex-wrap justify-end gap-2">{actions}</div> : null}
    </div>
  )
}

function Field({
  label,
  children,
  className = '',
}: {
  label: string
  children: ReactNode
  className?: string
}) {
  return (
    <label className={`block space-y-2 ${className}`.trim()}>
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}

function Toggle({
  label,
  helper,
  checked,
  disabled = false,
  onChange,
}: {
  label: string
  helper?: string
  checked: boolean
  disabled?: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className={`flex items-start gap-3 rounded-[var(--radius-control)] border border-app bg-[var(--app-surface)] px-3 py-2.5 ${disabled ? 'opacity-60' : ''}`}>
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} className="mt-1" />
      <span className="min-w-0">
        <span className="block text-sm font-medium text-app">{label}</span>
        {helper ? <span className="mt-1 block text-xs text-muted">{helper}</span> : null}
      </span>
    </label>
  )
}

function openUserEditor(
  user: UserRecord | null,
  setEditingUser: (value: UserRecord | null) => void,
  setUserForm: (value: UpsertUserInput) => void,
  setUserDrawerOpen: (value: boolean) => void,
) {
  setEditingUser(user)
  setUserForm(
    user
      ? {
          fullName: user.fullName,
          email: user.email,
          phoneNumber: user.phoneNumber ?? '',
          password: '',
          role: user.role,
          jobTitle: user.jobTitle ?? '',
          department: user.department ?? '',
          isActive: user.isActive,
          permissions: normalizePermissions(user.permissions),
          branchIds: user.branchIds,
          defaultBranchId: user.defaultBranchId ?? null,
          hasAllBranchAccess: user.hasAllBranchAccess,
          assignmentGroupIds: user.assignmentGroups.map((group) => group.id),
        }
      : defaultUserForm(),
  )
  setUserDrawerOpen(true)
}

function openGroupEditor(
  group: AssignmentGroupRecord | null,
  setEditingGroup: (value: AssignmentGroupRecord | null) => void,
  setGroupForm: (value: GroupEditorState) => void,
  setGroupDrawerOpen: (value: boolean) => void,
) {
  setEditingGroup(group)
  setGroupForm(
    group
      ? {
          name: group.name,
          description: group.description ?? '',
          skillArea: group.skillArea ?? '',
          branchId: group.branchId ?? '',
          isActive: group.isActive,
          memberUserIds: group.members.map((member) => member.userId).filter((value): value is string => Boolean(value)),
          teamLeadUserId: group.members.find((member) => member.isLead)?.userId ?? '',
        }
      : defaultGroupForm(),
  )
  setGroupDrawerOpen(true)
}

function openProfileEditor(
  profile: TeamManagementProfileRecord,
  setEditingProfile: (value: TeamManagementProfileRecord | null) => void,
  setProfileForm: (value: ProfileEditorState) => void,
  setProfileDrawerOpen: (value: boolean) => void,
) {
  setEditingProfile(profile)
  setProfileForm({
    availabilityStatus: profile.availabilityStatus === 'NotReady' ? 'Available' : profile.availabilityStatus,
    availabilityNote: profile.availabilityNote ?? '',
    availabilityUntil: toDateTimeLocal(profile.availabilityUntil),
    certifications: profile.certifications.map((certification) => ({
      certificationId: certification.certificationId,
      certificateNumber: certification.certificateNumber ?? '',
      issuedOn: toDateInput(certification.issuedOn),
      expiresOn: toDateInput(certification.expiresOn),
      notes: certification.notes ?? '',
    })),
  })
  setProfileDrawerOpen(true)
}

function openCertificationEditor(
  certification: CertificationDefinitionRecord | null,
  setEditingCertification: (value: CertificationDefinitionRecord | null) => void,
  setCertificationForm: (value: UpsertCertificationDefinitionInput) => void,
  setCertificationDrawerOpen: (value: boolean) => void,
) {
  setEditingCertification(certification)
  setCertificationForm(
    certification
      ? {
          name: certification.name,
          issuer: certification.issuer ?? '',
          description: certification.description ?? '',
          renewalPeriodMonths: certification.renewalPeriodMonths ?? null,
          isActive: certification.isActive,
        }
      : { name: '', issuer: '', description: '', renewalPeriodMonths: null, isActive: true },
  )
  setCertificationDrawerOpen(true)
}

function asTab(value: string | null): TeamManagementTab {
  if (value === 'roles' || value === 'groups' || value === 'readiness') {
    return value
  }

  return 'people'
}

function normalizePermissions(permissions?: ApiPermissions | null): ApiPermissions {
  return {
    ...emptyPermissions,
    ...(permissions ?? {}),
  }
}

function formatNamedMemberships(items: Array<{ name: string; isLead: boolean }>) {
  if (!items.length) {
    return 'None'
  }

  return items.map((item) => (item.isLead ? `${item.name} (Lead)` : item.name)).join(', ')
}

function readinessTone(status: string) {
  if (status === 'Ready') return 'success'
  if (status === 'Unavailable') return 'warning'
  return 'info'
}

function availabilityTone(status: string) {
  if (status === 'Available') return 'success'
  if (status === 'Unavailable') return 'warning'
  if (status === 'Limited') return 'info'
  return 'neutral'
}

function certificationTone(status: string) {
  if (status === 'Valid') return 'success'
  if (status === 'ExpiringSoon') return 'warning'
  if (status === 'Expired') return 'danger'
  return 'neutral'
}

function toErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message
  }

  return fallback
}

function nullable(value: string | null | undefined) {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

function toDateInput(value?: string | null) {
  if (!value) return ''
  return value.slice(0, 10)
}

function toDateTimeLocal(value?: string | null) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return ''
  }

  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  const hours = `${date.getHours()}`.padStart(2, '0')
  const minutes = `${date.getMinutes()}`.padStart(2, '0')
  return `${year}-${month}-${day}T${hours}:${minutes}`
}

function toIsoDateTime(value?: string | null) {
  if (!value) return null
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString()
}
