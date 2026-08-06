import { capabilityGroups, type CapabilityKey, type RoleCapabilities } from '../rolePermissionsApi'
import { cn } from '../../../shared/utils/cn'

interface PermissionMatrixProps {
  /** Current capability values (shape-compatible with a user's `permissions`). */
  value: RoleCapabilities
  onChange: (next: RoleCapabilities) => void
  /** When true, checkboxes are shown but locked (e.g. Admin has implicit full access). */
  disabled?: boolean
}

/**
 * The 23 tenant capabilities rendered as grouped checkboxes with a per-group
 * "Select all". Reused for role defaults and per-user overrides so both flows
 * present the same box-beside-each-function editor.
 */
export function PermissionMatrix({ value, onChange, disabled = false }: PermissionMatrixProps) {
  const setKey = (key: CapabilityKey, checked: boolean) => onChange({ ...value, [key]: checked })
  const setGroup = (keys: CapabilityKey[], checked: boolean) => {
    const next = { ...value }
    keys.forEach((key) => {
      next[key] = checked
    })
    onChange(next)
  }

  return (
    <div className="grid gap-4 md:grid-cols-2">
      {capabilityGroups.map((group) => {
        const keys = group.items.map((item) => item.key)
        const allChecked = keys.every((key) => value[key])
        const someChecked = keys.some((key) => value[key])
        return (
          <section key={group.group} className="overflow-hidden rounded-xl border border-slate-200 bg-white">
            <header className="flex items-center justify-between gap-3 border-b border-slate-100 bg-slate-50 px-4 py-3">
              <h3 className="text-sm font-semibold text-slate-900">{group.group}</h3>
              <label className={cn('inline-flex items-center gap-2 text-xs font-medium', disabled ? 'text-slate-400' : 'cursor-pointer text-slate-600')}>
                <input
                  type="checkbox"
                  className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
                  checked={allChecked}
                  ref={(el) => {
                    if (el) el.indeterminate = !allChecked && someChecked
                  }}
                  disabled={disabled}
                  onChange={(event) => setGroup(keys, event.target.checked)}
                />
                Select all
              </label>
            </header>
            <ul className="divide-y divide-slate-100">
              {group.items.map((item) => (
                <li key={item.key}>
                  <label className={cn('flex items-center justify-between gap-4 px-4 py-2.5 text-sm', disabled ? 'cursor-not-allowed text-slate-500' : 'cursor-pointer text-slate-700')}>
                    <span>{item.label}</span>
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
                      checked={value[item.key]}
                      disabled={disabled}
                      onChange={(event) => setKey(item.key, event.target.checked)}
                    />
                  </label>
                </li>
              ))}
            </ul>
          </section>
        )
      })}
    </div>
  )
}
