import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Copy, ExternalLink, KeyRound, RotateCcw } from 'lucide-react'
import { api } from '../shared/api/apiClient'
import { PageHeader } from '../shared/components/PageHeader'
import { StatCard } from '../shared/components/StatCard'
import { Spinner } from '../shared/components/Spinner'
import { ConfirmDialog } from '../shared/components/ConfirmDialog'
import { formatDateTime } from '../shared/utils/dates'

interface DemoWorkspaceLogin {
  username: string
  fullName: string
  role: string
  shows: string
}

interface DemoWorkspaceStatus {
  exists: boolean
  tenantId: string
  companyName: string
  workspace: string
  lastResetAt: string | null
  lastResetBy: string | null
  clients: number
  workOrders: number
  quotations: number
  invoices: number
  logins: DemoWorkspaceLogin[]
  portalContact: { id: string; fullName: string; clientName: string } | null
}

interface DemoWorkspaceResetResult {
  status: DemoWorkspaceStatus
  password: string
}

const endpoint = '/api/platform/demo-workspace'
const cardClass = 'rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)]'

async function copy(text: string, label: string) {
  try {
    await navigator.clipboard.writeText(text)
    toast.success(`${label} copied`)
  } catch {
    toast.error('Copy failed — select the text and copy it manually.')
  }
}

export function DemoWorkspacePage() {
  const queryClient = useQueryClient()
  const { data, isLoading, isError } = useQuery({ queryKey: ['platform', 'demo-workspace'], queryFn: () => api.get<DemoWorkspaceStatus>(endpoint) })
  const [confirmReset, setConfirmReset] = useState(false)
  const [password, setPassword] = useState<string | null>(null)
  const [portalUrl, setPortalUrl] = useState<string | null>(null)

  const reset = useMutation({
    mutationFn: () => api.post<DemoWorkspaceResetResult>(`${endpoint}/reset`),
    onSuccess: (result) => {
      queryClient.setQueryData(['platform', 'demo-workspace'], result.status)
      setPassword(result.password)
      setConfirmReset(false)
      toast.success('Demo workspace is ready')
    },
    onError: () => setConfirmReset(false),
  })

  const portalLink = useMutation({
    mutationFn: () => api.post<{ url: string }>(`${endpoint}/portal-link`),
  })

  const openPortal = () => {
    // Browsers only allow a new tab straight from the click, not after the request resolves, so open it now
    // and point it at the link when it arrives. The link stays on the page too, in case the tab was blocked.
    const tab = window.open('', '_blank')
    portalLink.mutate(undefined, {
      onSuccess: ({ url }) => {
        setPortalUrl(url)
        if (tab) {
          tab.opener = null
          tab.location.href = url
        }
      },
      onError: () => tab?.close(),
    })
  }

  if (isLoading) {
    return <Spinner label="Loading demo workspace" />
  }

  if (isError || !data) {
    return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Demo workspace status could not be loaded.</div>
  }

  const loginUrl = `${window.location.origin}/login`

  return (
    <div className="space-y-4">
      <PageHeader
        title="Demo Workspace"
        subtitle="A ready-made workspace with realistic data for showing ECOSYS to prospects. Email is switched off for it, and it is never billed or expired."
        primaryAction={
          <button
            type="button"
            onClick={() => (data.exists ? setConfirmReset(true) : reset.mutate())}
            disabled={reset.isPending}
            className="inline-flex h-11 shrink-0 items-center gap-2 whitespace-nowrap rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            <RotateCcw className="h-4 w-4" />
            {reset.isPending ? 'Preparing…' : data.exists ? 'Reset Demo Data' : 'Create Demo Workspace'}
          </button>
        }
      />

      {password ? (
        <div className="rounded-[var(--radius-card)] border border-amber-300 bg-amber-50 p-4">
          <div className="flex items-start gap-3">
            <KeyRound className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
            <div className="min-w-0 flex-1">
              <h2 className="text-sm font-semibold text-slate-900">New demo password</h2>
              <p className="mt-1 text-sm text-slate-700">Shared by all demo logins. It is shown only now and changes on every reset.</p>
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <code className="rounded-lg border border-amber-200 bg-white px-3 py-2 font-mono text-base text-slate-900">{password}</code>
                <button type="button" onClick={() => void copy(password, 'Password')} className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
                  <Copy className="h-4 w-4" />Copy
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {!data.exists ? (
        <div className={cardClass}>
          <p className="text-sm text-slate-700">The demo workspace has not been created yet. Creating it adds a tenant called <strong>{data.companyName}</strong> with four clients, sites, assets, jobs in every status, maintenance plans, contracts, quotes, invoices and a client portal contact.</p>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <StatCard label="Clients" value={data.clients} />
            <StatCard label="Jobs" value={data.workOrders} />
            <StatCard label="Quotes" value={data.quotations} />
            <StatCard label="Invoices" value={data.invoices} />
          </div>

          <div className={cardClass}>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 className="text-sm font-semibold text-slate-900">Staff sign-in</h2>
                <p className="mt-1 text-sm text-slate-600">
                  At <a href={loginUrl} target="_blank" rel="noreferrer" className="font-medium text-[var(--app-primary)] underline underline-offset-4">{loginUrl}</a>, sign in with a username below and company name{' '}
                  <strong>{data.companyName}</strong>.{password ? '' : ' Reset the demo to get a new password.'}
                </p>
              </div>
              <p className="text-xs text-slate-500">
                {data.lastResetAt ? `Last reset ${formatDateTime(data.lastResetAt)}${data.lastResetBy ? ` by ${data.lastResetBy}` : ''}` : null}
              </p>
            </div>
            <div className="mt-4 overflow-x-auto">
              <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                <thead>
                  <tr className="text-xs uppercase tracking-wide text-slate-500">
                    <th className="py-2 pr-4 font-semibold">Username</th>
                    <th className="py-2 pr-4 font-semibold">Name</th>
                    <th className="py-2 pr-4 font-semibold">Role</th>
                    <th className="py-2 font-semibold">What it shows</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {data.logins.map((login) => (
                    <tr key={login.username}>
                      <td className="py-2.5 pr-4">
                        <button type="button" onClick={() => void copy(login.username, 'Username')} className="inline-flex items-center gap-1.5 font-mono text-slate-900 hover:text-[var(--app-primary)]" title="Copy username">
                          {login.username}<Copy className="h-3.5 w-3.5 text-slate-400" />
                        </button>
                      </td>
                      <td className="py-2.5 pr-4 text-slate-700">{login.fullName}</td>
                      <td className="py-2.5 pr-4 text-slate-700">{login.role}</td>
                      <td className="py-2.5 text-slate-600">{login.shows}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className={cardClass}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="text-sm font-semibold text-slate-900">Client portal</h2>
                <p className="mt-1 text-sm text-slate-600">
                  {data.portalContact
                    ? <>Opens the portal as <strong>{data.portalContact.fullName}</strong> ({data.portalContact.clientName}). Each link works once and expires after 30 minutes.</>
                    : 'No portal contact yet — reset the demo workspace to create one.'}
                </p>
              </div>
              <button
                type="button"
                onClick={openPortal}
                disabled={!data.portalContact || portalLink.isPending}
                className="inline-flex h-11 items-center gap-2 rounded-lg border border-[var(--app-border)] bg-white px-4 text-sm font-medium text-slate-700 hover:bg-[var(--app-primary-soft)] disabled:opacity-60"
              >
                <ExternalLink className="h-4 w-4" />
                {portalLink.isPending ? 'Opening…' : 'Open Client Portal'}
              </button>
            </div>
            {portalUrl ? (
              <div className="mt-3 flex flex-wrap items-center gap-2 rounded-lg bg-slate-50 p-3 text-sm">
                <span className="text-slate-600">Latest sign-in link (single use):</span>
                <a href={portalUrl} target="_blank" rel="noreferrer" className="font-medium text-[var(--app-primary)] underline underline-offset-4">Open</a>
                <button type="button" onClick={() => void copy(portalUrl, 'Portal link')} className="inline-flex items-center gap-1 font-medium text-slate-700 hover:text-[var(--app-primary)]">
                  <Copy className="h-3.5 w-3.5" />Copy
                </button>
                <span className="text-xs text-slate-500">Paste into a private window to show the portal without leaving this session.</span>
              </div>
            ) : null}
          </div>
        </>
      )}

      <ConfirmDialog
        open={confirmReset}
        title="Reset demo data?"
        message="This deletes everything in the demo workspace — including anything prospects created — and loads fresh demo data. Anyone signed in to the demo is signed out, and the demo password changes."
        confirmLabel={reset.isPending ? 'Resetting…' : 'Reset Demo Data'}
        confirmDisabled={reset.isPending}
        danger
        onCancel={() => setConfirmReset(false)}
        onConfirm={() => reset.mutate()}
      />
    </div>
  )
}
