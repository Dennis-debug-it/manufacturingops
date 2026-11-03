import { Lock } from 'lucide-react'

/**
 * Shown where an add button would be, to someone without the permission behind it. Without this the
 * button simply wasn't there — managers and dispatchers don't manage stock by default — and pages
 * like Tools still said "add tools with New Tool", leaving people hunting for a button they can't see.
 */
export function ManageAccessHint({ what, orIssue = false }: { what: string; orIssue?: boolean }) {
  return (
    <p className="flex items-start gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-600">
      <Lock className="mt-0.5 h-4 w-4 shrink-0 text-slate-400" />
      <span>
        Adding {what} needs the <strong className="font-medium text-slate-800">Manage materials</strong>
        {orIssue ? <> or <strong className="font-medium text-slate-800">Issue materials</strong></> : null} permission, which admins and materials controllers have.{' '}
        Ask a workspace admin to add them, or to give your role that permission under Users &amp; Access → Permission groups.
      </span>
    </p>
  )
}
