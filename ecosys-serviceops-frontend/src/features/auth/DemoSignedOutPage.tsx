import { Link } from 'react-router-dom'
import { Presentation } from 'lucide-react'

/**
 * Where a demo login lands after signing out, instead of a bare sign-in form: the demo password is shown only once,
 * so the way back in is the platform's Demo Workspace page, which opens any role in one click.
 */
export function DemoSignedOutPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-4 text-center">
      <Presentation className="h-10 w-10 text-slate-400" />
      <h1 className="text-xl font-semibold text-slate-900">You've left the ECOSYS demo</h1>
      <p className="max-w-md text-sm text-slate-600">
        To go back in as any role, open it again from <strong>Platform → Demo Workspace</strong>. No password needed there.
      </p>
      <div className="flex flex-wrap justify-center gap-2">
        <Link to="/platform/demo" className="inline-flex h-10 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800">Open Demo Workspace</Link>
        <Link to="/login" className="inline-flex h-10 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Sign in with a demo login</Link>
      </div>
    </div>
  )
}
