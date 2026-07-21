import { Search, ShieldCheck } from 'lucide-react'
import { useMemo, useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { TeamTabNav } from './components/TeamTabNav'

const groups = {
  'Work Orders': ['View work orders', 'Create work orders', 'Edit work orders', 'Assign work orders', 'Change work-order status', 'Complete work orders', 'Reopen work orders', 'Cancel work orders', 'Approve work completion', 'Delete work orders', 'View all work orders', 'Export work orders'],
  Knowledge: ['View knowledge base', 'Create knowledge articles', 'Edit knowledge articles', 'Publish knowledge articles', 'Archive knowledge articles', 'Manage knowledge categories'],
  'Inventory & Materials': ['View materials', 'Request materials', 'Approve material requests', 'Issue materials', 'Return materials', 'Transfer materials', 'Receive materials', 'Manage materials', 'Adjust stock levels', 'Override stock controls', 'View material costs', 'Manage stores', 'Conduct stock counts', 'Approve stock adjustments'],
  Assets: ['View assets', 'Create assets', 'Edit assets', 'Transfer assets', 'Retire assets', 'Delete assets', 'View asset costs', 'Manage asset categories', 'Manage maintenance plans'],
  Finance: ['View finance dashboard', 'View financial documents', 'Create financial documents', 'Edit financial documents', 'Approve financial documents', 'Issue financial documents', 'Void financial documents', 'Record payments', 'Allocate payments', 'View financial reports', 'Export financial data', 'Manage finance settings'],
  'Rate Cards': ['View rate cards', 'Create rate cards', 'Edit rate cards', 'Approve rate cards', 'Assign rate cards', 'Delete rate cards'],
  'Invoice Approvals': ['View invoice approval queue', 'Submit invoices for approval', 'Approve invoices', 'Reject invoices', 'Override invoice approval', 'Configure invoice approvals'],
  'Billable Work': ['View billable work', 'Mark work as billable', 'Review billable work', 'Approve billable work', 'Edit billable amounts', 'Exclude work from billing', 'Convert billable work to invoice'],
  Reports: ['View operational reports', 'View financial reports', 'Create custom reports', 'Export reports', 'Schedule reports', 'View sensitive reports', 'Manage report templates'],
  Administration: ['View users', 'Create users', 'Edit users', 'Disable users', 'Delete users', 'Manage permission groups', 'Assign permissions', 'Manage clients', 'Manage sites', 'Manage contracts', 'Manage settings', 'View audit logs', 'Export audit logs', 'Manage integrations', 'Manage document templates', 'Manage workflow rules'],
} as const

export function RolesPage() {
  const [search, setSearch] = useState('')
  const visible = useMemo(() => Object.entries(groups).map(([name, items]) => ({ name, items: items.filter((item) => item.toLowerCase().includes(search.toLowerCase())) })).filter((group) => group.items.length), [search])
  return <div className="space-y-6">
    <PageHeader title="Capabilities" subtitle="The actions administrators can grant to users and permission groups." />
    <TeamTabNav />
    <div className="flex items-start gap-3 rounded-2xl border border-blue-200 bg-blue-50 p-4 text-sm text-blue-950"><ShieldCheck className="mt-0.5 h-5 w-5 shrink-0" /><p>Capabilities are independent building blocks, not roles. A user’s effective access combines group grants, direct overrides, scope, and approval limits.</p></div>
    <label className="flex h-12 items-center gap-3 rounded-xl border border-slate-200 bg-white px-4 shadow-sm"><Search className="h-4 w-4 text-slate-400" /><span className="sr-only">Search capabilities</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search capabilities" className="w-full border-0 bg-transparent text-sm outline-none" /></label>
    <div className="grid gap-4 lg:grid-cols-2">{visible.map((group) => <section key={group.name} className="rounded-2xl border border-slate-200 bg-white shadow-sm"><header className="border-b border-slate-100 px-5 py-4"><h2 className="font-semibold text-slate-950">{group.name}</h2><p className="mt-1 text-xs text-slate-500">{group.items.length} capabilities</p></header><ul className="divide-y divide-slate-100">{group.items.map((item) => <li key={item} className="flex items-center gap-3 px-5 py-3 text-sm text-slate-700"><span className="h-2 w-2 rounded-full bg-emerald-500" />{item}</li>)}</ul></section>)}</div>
  </div>
}
