import { Link } from 'react-router-dom'
import type { Role } from '../../types/app'
import { normalizeAppRole } from '../../utils/roles'

export function UnsupportedRolePage({ role }: { role: Role | null | undefined }) {
  const rawRole = typeof role === 'string' ? role : ''
  const normalizedRole = rawRole ? normalizeAppRole(rawRole) : 'unknown'

  return (
    <div className="min-h-dvh bg-app p-4 sm:p-6 lg:p-8">
      <div className="surface-card mx-auto max-w-2xl">
        <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Unsupported Role</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight text-app">This account role is not mapped in the frontend yet</h1>
        <p className="mt-3 text-sm text-muted">
          We signed you in, but we could not determine the correct workspace for this role.
        </p>
        <div className="mt-6 rounded-2xl border border-app/60 bg-app/40 px-4 py-3 text-sm text-app">
          <p><span className="font-semibold">Received role:</span> {rawRole || 'Unknown'}</p>
          <p><span className="font-semibold">Normalized role:</span> {normalizedRole}</p>
        </div>
        <p className="mt-4 text-sm text-muted">
          Check the role mapping in <span className="font-mono">src/utils/roles.ts</span> or contact support if this account should already have access.
        </p>
        <div className="mt-6">
          <Link to="/login" className="button-primary">
            Back to login
          </Link>
        </div>
      </div>
    </div>
  )
}
