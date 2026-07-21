import { PageHeader } from '../../shared/components/PageHeader'
import { TeamTabNav } from './components/TeamTabNav'

const content = {
  groups: { title: 'Permission Groups', subtitle: 'Reusable capability bundles created by your administrators.', heading: 'Create access around how your organization works', body: 'Combine capabilities in groups such as Nairobi Operations Team, Finance Approvers, or Read-Only Auditors. A user can belong to multiple groups.' },
  scopes: { title: 'Access Scopes', subtitle: 'Control where granted capabilities apply.', heading: 'Keep access within the right boundaries', body: 'Scope access to the organization, business units, regions, clients, sites, contracts, or a user’s assigned and created work orders.' },
  limits: { title: 'Approval Limits', subtitle: 'Set monetary thresholds for sensitive actions.', heading: 'Put clear limits around approvals', body: 'Configure maximum values for material requests, invoices, quotations, stock overrides, and other sensitive approvals.' },
  audit: { title: 'Audit History', subtitle: 'Trace changes to users, groups, permissions, scopes, and limits.', heading: 'Every access change, explained', body: 'Permission changes are recorded with the administrator, time, affected user, previous value, and new value.' },
} as const

export function AccessSectionPage({ section }: { section: keyof typeof content }) {
  const item = content[section]
  return <div className="space-y-6"><PageHeader title={item.title} subtitle={item.subtitle} /><TeamTabNav /><section className="rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-16 text-center"><h2 className="text-xl font-semibold text-slate-950">{item.heading}</h2><p className="mx-auto mt-3 max-w-2xl text-sm leading-6 text-slate-600">{item.body}</p><p className="mt-6 text-xs font-semibold uppercase tracking-[0.16em] text-amber-700">Backend configuration required</p></section></div>
}
