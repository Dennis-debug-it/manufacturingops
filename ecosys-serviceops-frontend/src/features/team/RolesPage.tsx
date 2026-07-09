import { useState } from 'react'
import { ChevronDown } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { cn } from '../../shared/utils/cn'

const permissionLabels: Array<{ key: string; label: string }> = [
  { key: 'canViewWorkOrders', label: 'View Work Orders' },
  { key: 'canCreateWorkOrders', label: 'Create Work Orders' },
  { key: 'canAssignWorkOrders', label: 'Assign Work Orders' },
  { key: 'canCompleteWorkOrders', label: 'Complete Work Orders' },
  { key: 'canViewMaterials', label: 'View Materials' },
  { key: 'canRequestMaterials', label: 'Request Materials' },
  { key: 'canManageMaterials', label: 'Manage Materials' },
  { key: 'canApproveMaterials', label: 'Approve Material Requests' },
  { key: 'canIssueMaterials', label: 'Issue Materials' },
  { key: 'canManageAssets', label: 'Manage Assets' },
  { key: 'canManageSettings', label: 'Manage Settings' },
  { key: 'canViewReports', label: 'View Reports' },
  { key: 'canViewFinance', label: 'View Finance' },
  { key: 'canManageFinance', label: 'Manage Finance' },
]

const roles = [
  { name: 'Admin', description: 'Full workspace access — all permissions granted implicitly.', allGranted: true },
  { name: 'User', description: 'Baseline tenant staff account. Granular capability is assigned per-user via the Permissions panel on the New/Edit User form, not by a role name.', allGranted: false },
]

export function RolesPage() {
  const [expanded, setExpanded] = useState<string | null>('Admin')

  return (
    <div className="space-y-6">
      <PageHeader title="Roles" subtitle="System-defined roles and permissions summary." />

      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
        The backend's tenant user model is binary — a user's <code className="rounded bg-white/60 px-1 py-0.5 text-xs">Role</code> field
        is only ever <strong>Admin</strong> or <strong>User</strong>. Fine-grained capability (the list below) is granted per-user via the
        Permissions panel when creating or editing a user, not by assigning a named role. Roles are system-defined and cannot be
        created or edited here.
      </div>

      <div className="space-y-3">
        {roles.map((role) => (
          <div key={role.name} className="rounded-2xl border border-slate-200 bg-white shadow-sm">
            <button type="button" onClick={() => setExpanded((current) => (current === role.name ? null : role.name))} className="flex w-full items-center justify-between px-5 py-4 text-left">
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-sm font-semibold text-slate-900">{role.name}</span>
                  {role.allGranted ? <StatusBadge status="All Permissions" /> : null}
                </div>
                <p className="mt-1 text-sm text-slate-600">{role.description}</p>
              </div>
              <ChevronDown className={cn('h-4 w-4 shrink-0 text-slate-400 transition-transform', expanded === role.name && 'rotate-180')} />
            </button>
            {expanded === role.name ? (
              <div className="border-t border-slate-100 px-5 py-4">
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  {permissionLabels.map((permission) => (
                    <div key={permission.key} className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 text-sm">
                      <span className="text-slate-700">{permission.label}</span>
                      <StatusBadge status={role.allGranted ? 'Granted' : 'Per User'} />
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
          </div>
        ))}
      </div>
    </div>
  )
}
