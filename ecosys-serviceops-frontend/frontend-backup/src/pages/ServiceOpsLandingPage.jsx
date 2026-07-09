import {
  Activity,
  ArrowRight,
  BarChart3,
  Building2,
  CheckCircle2,
  ClipboardCheck,
  Clock3,
  Factory,
  Gauge,
  HardHat,
  Layers3,
  LockKeyhole,
  PackageCheck,
  ShieldCheck,
  Wrench,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import { EcosysLogo, PoweredByEcosys } from '../components/brand'
import { useThemeMode } from '../context/ThemeContext'

const stats = [
  { label: 'SLA Compliance', value: '96%', icon: Gauge },
  { label: 'Open Work Orders', value: '128', icon: ClipboardCheck },
  { label: 'Active Technicians', value: '42', icon: HardHat },
  { label: 'Downtime Reduced', value: '38%', icon: Activity },
]

const features = [
  {
    icon: ClipboardCheck,
    title: 'Work Order Command Centre',
    text: 'Create, assign, track, escalate, and close jobs with full visibility from request to report.',
  },
  {
    icon: Clock3,
    title: 'SLA & Response Tracking',
    text: 'Monitor response times, overdue jobs, priority levels, and contract commitments in real time.',
  },
  {
    icon: PackageCheck,
    title: 'Materials & Stores Linkage',
    text: 'Book parts against work orders, raise store requisitions, and analyze material usage by asset or client.',
  },
  {
    icon: Wrench,
    title: 'Preventive Maintenance Templates',
    text: 'Preconfigured editable checklists for HVAC, generators, UPS systems, solar plants, and more.',
  },
  {
    icon: ShieldCheck,
    title: 'Audit Trail & Accountability',
    text: 'Know who did what, when, where, and why. Every update is traceable and report-ready.',
  },
  {
    icon: Building2,
    title: 'Multi-Branch Operations',
    text: 'Support tenants with multiple branches, outlets, departments, teams, and numbering rules.',
  },
]

const industries = [
  'Manufacturing Plants',
  'Tea & Sugar Factories',
  'Facilities Management',
  'Hospitals & Clinics',
  'Data Centres',
  'Transport & Logistics',
]

const workflow = [
  'Job Logged',
  'Assigned',
  'Technician Arrives',
  'Diagnosis & Materials',
  'Client Acknowledgement',
  'Report Generated',
]

const heroHighlights = [
  'SLA Tracking',
  'Audit Trail',
  'Tenant Isolation',
  'PM Templates',
  'Store Requisitions',
  'Auto Reports',
]

const problemCards = [
  ['Missed SLAs', 'Jobs exceed response or resolution timelines without early warning.'],
  ['No Technician Visibility', 'Managers cannot see arrivals, departures, progress, or bottlenecks.'],
  ['Weak Reporting', 'Reports are delayed, inconsistent, or impossible to audit.'],
  ['Poor Stock Control', 'Materials are used without proper linkage to jobs, assets, or clients.'],
]

const securityItems = [
  [LockKeyhole, 'Tenant-level data isolation'],
  [ShieldCheck, 'Role-based permissions and audit trails'],
  [BarChart3, 'Executive dashboards and SLA analytics'],
  [Building2, 'Branch, outlet, and department configuration'],
]

function Badge({ children }) {
  return (
    <span
      className="inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium shadow-sm"
      style={{
        border: '1px solid rgba(105, 201, 178, 0.3)',
        background: 'rgba(14, 124, 102, 0.12)',
        color: '#bfeee3',
      }}
    >
      <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ background: '#F5C542' }} />
      {children}
    </span>
  )
}

function StatCard({ item }) {
  const Icon = item.icon

  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.06] p-5 shadow-lg transition-colors hover:bg-white/[0.09]">
      <div className="mb-4 flex h-11 w-11 items-center justify-center rounded-xl bg-emerald-400/15 text-emerald-300">
        <Icon className="h-5 w-5" />
      </div>
      <div className="text-3xl font-black tracking-tight text-white">{item.value}</div>
      <div className="mt-1 text-sm text-slate-400">{item.label}</div>
    </div>
  )
}

function FeatureCard({ feature }) {
  const Icon = feature.icon

  return (
    <div className="group rounded-3xl border border-slate-200 bg-white p-7 shadow-sm transition-colors hover:border-emerald-300/50 dark:border-white/10 dark:bg-white/[0.05] dark:hover:bg-white/[0.08]">
      <div className="mb-5 flex h-12 w-12 items-center justify-center rounded-2xl bg-slate-900 text-emerald-300 dark:bg-emerald-400/15">
        <Icon className="h-6 w-6" />
      </div>
      <h3 className="text-lg font-bold text-slate-950 dark:text-white">{feature.title}</h3>
      <p className="mt-3 leading-7 text-slate-600 dark:text-slate-300">{feature.text}</p>
    </div>
  )
}

export default function ServiceOpsLandingPage() {
  const { theme } = useThemeMode()

  return (
    <div className={theme === 'dark' ? 'dark' : undefined}>
      <main className="min-h-screen bg-slate-950 text-white">
        <header className="sticky top-0 z-50 border-b border-white/10 bg-slate-950/92">
          <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4 lg:px-8">
            <EcosysLogo variant="light" size="lg" subtitle="Field Operations Command Centre" />

            <nav className="hidden items-center gap-8 text-sm font-medium text-slate-300 md:flex">
              <a href="#features" className="hover:text-white">Features</a>
              <a href="#workflow" className="hover:text-white">Workflow</a>
              <a href="#industries" className="hover:text-white">Industries</a>
              <a href="#security" className="hover:text-white">Security</a>
            </nav>

            <div className="flex items-center gap-3">
              <Link to="/login" className="rounded-full border border-white/15 px-5 py-2.5 text-sm font-bold text-white transition hover:bg-white/10">
                Login
              </Link>
              <Link to="/get-started" className="rounded-full px-5 py-2.5 text-sm font-bold text-slate-950 transition hover:brightness-105" style={{ background: '#F5C542' }}>
                Get Started
              </Link>
            </div>
          </div>
        </header>

        <section className="relative overflow-hidden">
          <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(16,185,129,0.14),transparent_30%),linear-gradient(225deg,rgba(59,130,246,0.12),transparent_28%)]" />

          <div className="relative mx-auto grid max-w-7xl items-center gap-12 px-6 py-20 lg:grid-cols-2 lg:px-8 lg:py-28">
            <div>
              <Badge>Built for factories, facilities, field teams, and enterprise maintenance</Badge>

              <h1 className="mt-8 max-w-3xl text-5xl font-black leading-tight tracking-tight md:text-6xl lg:text-7xl">
                End Downtime. Control Every Job. Run Operations Like a Command Centre.
              </h1>

              <p className="mt-6 max-w-2xl text-lg leading-8 text-slate-300 md:text-xl">
                ServiceOps helps maintenance teams manage work orders, technicians, SLAs, preventive maintenance, materials, reports, and client accountability from one operational platform.
              </p>

              <div className="mt-9 flex flex-col gap-4 sm:flex-row">
                <Link to="/login" className="inline-flex items-center justify-center gap-2 rounded-full px-7 py-4 font-bold text-slate-950 shadow-lg transition hover:brightness-105" style={{ background: '#F5C542', boxShadow: '0 18px 40px rgba(14, 124, 102, 0.18)' }}>
                  Login <ArrowRight className="h-5 w-5" />
                </Link>
                <Link to="/get-started" className="inline-flex items-center justify-center gap-2 rounded-full border border-white/15 px-7 py-4 font-bold text-white transition hover:bg-white/10">
                  Get Started
                </Link>
              </div>

              <div className="mt-10 grid max-w-xl grid-cols-2 gap-4 text-sm text-slate-300 sm:grid-cols-3">
                {heroHighlights.map((item) => (
                  <div key={item} className="flex items-center gap-2">
                    <CheckCircle2 className="h-4 w-4" style={{ color: '#F5C542' }} />
                    {item}
                  </div>
                ))}
              </div>
            </div>

            <div className="relative">
              <div className="absolute inset-0 rounded-[2rem] border border-emerald-400/20" />
              <div className="relative overflow-hidden rounded-[2rem] border border-white/10 bg-slate-900 p-5 shadow-xl">
                <div className="mb-5 flex items-center justify-between border-b border-white/10 pb-4">
                  <div>
                    <div className="text-sm text-slate-400">Command Centre</div>
                    <div className="text-xl font-black">Live Operations</div>
                  </div>
                  <div className="rounded-full px-3 py-1 text-xs font-bold" style={{ background: 'rgba(14, 124, 102, 0.15)', color: '#9EE2D4' }}>LIVE</div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  {stats.map((item) => (
                    <StatCard key={item.label} item={item} />
                  ))}
                </div>

                <div className="mt-5 rounded-2xl border border-white/10 bg-white/[0.04] p-5">
                  <div className="mb-4 flex items-center justify-between">
                    <div className="font-bold">Work Order Timeline</div>
                    <div className="text-xs text-slate-400">WO-2026-00128</div>
                  </div>

                  <div className="space-y-4">
                    {workflow.map((step, index) => (
                      <div key={step} className="flex items-center gap-4">
                        <div className={`flex h-8 w-8 items-center justify-center rounded-full text-xs font-black ${index < 4 ? 'bg-emerald-400 text-slate-950' : 'bg-white/10 text-slate-400'}`}>
                          {index + 1}
                        </div>
                        <div className="flex-1">
                          <div className="flex items-center justify-between">
                            <span className={index < 4 ? 'text-white' : 'text-slate-500'}>{step}</span>
                            <span className="text-xs text-slate-500">{index < 4 ? 'Done' : 'Pending'}</span>
                          </div>
                          <div className="mt-2 h-1.5 rounded-full bg-white/10">
                            <div className={`h-1.5 rounded-full ${index < 4 ? 'w-full bg-emerald-400' : 'w-1/4 bg-white/20'}`} />
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="bg-white py-20 text-slate-950 dark:bg-slate-950 dark:text-white">
          <div className="mx-auto max-w-7xl px-6 lg:px-8">
            <div className="grid gap-10 lg:grid-cols-[0.9fr_1.1fr] lg:items-center">
              <div>
                <p className="font-bold uppercase tracking-[0.2em] text-emerald-600 dark:text-emerald-300">The Problem</p>
                <h2 className="mt-4 text-4xl font-black tracking-tight md:text-5xl">
                  Most field operations fail quietly before they fail publicly.
                </h2>
                <p className="mt-5 text-lg leading-8 text-slate-600 dark:text-slate-300">
                  Manual job cards, WhatsApp updates, Excel trackers, missing signatures, uncontrolled spares, and delayed reports create blind spots. ServiceOps turns every job into a traceable workflow.
                </p>
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                {problemCards.map(([title, text]) => (
                  <div key={title} className="rounded-3xl border border-slate-200 bg-slate-50 p-6 dark:border-white/10 dark:bg-white/[0.05]">
                    <h3 className="font-black">{title}</h3>
                    <p className="mt-2 leading-7 text-slate-600 dark:text-slate-300">{text}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section id="features" className="scroll-mt-24 bg-slate-50 py-20 text-slate-950 dark:bg-slate-900 dark:text-white">
          <div className="mx-auto max-w-7xl px-6 lg:px-8">
            <div className="mx-auto max-w-3xl text-center">
              <p className="font-bold uppercase tracking-[0.2em] text-emerald-600 dark:text-emerald-300">Core Platform</p>
              <h2 className="mt-4 text-4xl font-black tracking-tight md:text-5xl">
                Everything needed to run technical operations professionally.
              </h2>
              <p className="mt-5 text-lg leading-8 text-slate-600 dark:text-slate-300">
                Built for accountability, visibility, reporting, and operational control across clients, branches, teams, and assets.
              </p>
            </div>

            <div className="mt-14 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
              {features.map((feature) => (
                <FeatureCard key={feature.title} feature={feature} />
              ))}
            </div>
          </div>
        </section>

        <section id="workflow" className="scroll-mt-24 bg-white py-20 text-slate-950 dark:bg-slate-950 dark:text-white">
          <div className="mx-auto max-w-7xl px-6 lg:px-8">
            <div className="grid gap-12 lg:grid-cols-2 lg:items-center">
              <div>
                <p className="font-bold uppercase tracking-[0.2em] text-emerald-600 dark:text-emerald-300">Workflow Engine</p>
                <h2 className="mt-4 text-4xl font-black tracking-tight md:text-5xl">
                  From request to report, every step is controlled.
                </h2>
                <p className="mt-5 text-lg leading-8 text-slate-600 dark:text-slate-300">
                  ServiceOps gives teams a repeatable operating model: job logging, assignment, arrival, diagnosis, material usage, completion, acknowledgement, and report generation.
                </p>
              </div>

              <div className="rounded-[2rem] border border-slate-200 bg-slate-50 p-6 shadow-sm dark:border-white/10 dark:bg-white/[0.05]">
                <div className="space-y-5">
                  {workflow.map((step, index) => (
                    <div key={step} className="flex gap-4">
                      <div className="flex flex-col items-center">
                        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-slate-950 font-black text-emerald-300 dark:bg-emerald-400/15">
                          {index + 1}
                        </div>
                        {index !== workflow.length - 1 && <div className="h-10 w-px bg-slate-300 dark:bg-white/15" />}
                      </div>
                      <div className="pt-2">
                        <div className="font-black">{step}</div>
                        <p className="mt-1 text-sm leading-6 text-slate-600 dark:text-slate-300">
                          {index === 0 && 'Capture client, asset, priority, issue type, branch, and SLA.'}
                          {index === 1 && 'Assign to technician, team, department, or assignment group.'}
                          {index === 2 && 'Track arrival time, departure time, and site attendance.'}
                          {index === 3 && 'Record diagnosis, parts used, requisitions, and photos.'}
                          {index === 4 && 'Capture client confirmation and completion notes.'}
                          {index === 5 && 'Generate professional job reports and maintenance history.'}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </section>

        <section id="industries" className="scroll-mt-24 bg-slate-950 py-20 text-white">
          <div className="mx-auto max-w-7xl px-6 lg:px-8">
            <div className="grid gap-12 lg:grid-cols-[0.85fr_1.15fr] lg:items-center">
              <div>
                <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-400 text-slate-950">
                  <Factory className="h-7 w-7" />
                </div>
                <h2 className="mt-6 text-4xl font-black tracking-tight md:text-5xl">
                  Designed for serious operations, not generic task management.
                </h2>
                <p className="mt-5 text-lg leading-8 text-slate-300">
                  The platform fits environments where downtime, delays, missed reports, and uncontrolled maintenance costs have real business impact.
                </p>
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                {industries.map((industry) => (
                  <div key={industry} className="flex items-center gap-4 rounded-2xl border border-white/10 bg-white/[0.06] p-5">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-400/15 text-emerald-300">
                      <Layers3 className="h-5 w-5" />
                    </div>
                    <span className="font-bold">{industry}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section id="security" className="scroll-mt-24 bg-white py-20 text-slate-950 dark:bg-slate-950 dark:text-white">
          <div className="mx-auto max-w-7xl px-6 lg:px-8">
            <div className="rounded-[2rem] border border-slate-200 bg-slate-50 p-8 shadow-sm dark:border-white/10 dark:bg-white/[0.05] md:p-12">
              <div className="grid gap-10 lg:grid-cols-2 lg:items-center">
                <div>
                  <p className="font-bold uppercase tracking-[0.2em] text-emerald-600 dark:text-emerald-300">Enterprise Ready</p>
                  <h2 className="mt-4 text-4xl font-black tracking-tight md:text-5xl">
                    Built with SaaS control, client isolation, and operational trust.
                  </h2>
                  <p className="mt-5 text-lg leading-8 text-slate-600 dark:text-slate-300">
                    Platform owners manage tenants from the command centre, while each organization runs its own users, branches, settings, workflows, and data boundaries.
                  </p>
                </div>

                <div className="grid gap-4">
                  {securityItems.map(([Icon, text]) => (
                    <div key={text} className="flex items-center gap-4 rounded-2xl bg-white p-5 shadow-sm dark:bg-slate-900">
                      <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-slate-950 text-emerald-300 dark:bg-emerald-400/15">
                        <Icon className="h-5 w-5" />
                      </div>
                      <span className="font-bold">{text}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="relative overflow-hidden bg-slate-950 py-20 text-white">
          <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(16,185,129,0.08),transparent_35%)]" />
          <div className="relative mx-auto max-w-4xl px-6 text-center lg:px-8">
            <h2 className="text-4xl font-black tracking-tight md:text-6xl">
              Stop managing operations blindly.
            </h2>
            <p className="mx-auto mt-6 max-w-2xl text-lg leading-8 text-slate-300">
              Give your clients a modern field operations system that improves accountability, reduces downtime, and produces professional reports automatically.
            </p>
            <div className="mt-9 flex flex-col justify-center gap-4 sm:flex-row">
              <Link to="/login" className="inline-flex items-center justify-center gap-2 rounded-full bg-emerald-400 px-8 py-4 font-black text-slate-950 shadow-lg shadow-emerald-400/15 transition hover:bg-emerald-300">
                Login <ArrowRight className="h-5 w-5" />
              </Link>
              <Link to="/get-started" className="inline-flex items-center justify-center gap-2 rounded-full border border-white/15 px-8 py-4 font-black text-white transition hover:bg-white/10">
                Get Started
              </Link>
            </div>
          </div>
        </section>

        <footer className="border-t border-white/10 bg-slate-950 px-6 py-8 text-center text-sm text-slate-500">
          <div className="flex flex-col items-center justify-center gap-4">
            <PoweredByEcosys tone="dark" />
            <span>(c) 2026 Ecosys ServiceOps. Built for operational control.</span>
          </div>
        </footer>
      </main>
    </div>
  )
}
