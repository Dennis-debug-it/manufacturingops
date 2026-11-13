import type { AppRole } from '../types/auth'
import { WHATSAPP_ENABLED } from '../config/features'
import { LayoutDashboard, Wrench, Users, Building2, Package, FileText, Wallet, BarChart3, Settings2, ShieldCheck, MessageCircle, CheckSquare, CalendarClock, Activity, LifeBuoy, CalendarDays, Boxes, ScanLine, Factory, ClipboardCheck, Gauge, CalendarRange, HardHat, FileCheck2, UserCog, ShieldAlert, Warehouse, HardHat as ContractorIcon, TrendingUp, Landmark, BadgeCheck, Repeat, Truck, DoorOpen } from 'lucide-react'

export interface NavItem {
  label: string
  to: string
  roles: AppRole[]
  permission?: string
  icon: typeof LayoutDashboard
  /** The sidebar section it sits under (tenant side only). */
  group?: NavGroup
}

/** Sidebar sections, in display order. A section with nothing the person may open is not shown. */
export const navGroups = ['Daily work', 'Site security', 'Planning', 'Assets', 'Stores', 'Compliance', 'Insights', 'Team and admin'] as const
export type NavGroup = (typeof navGroups)[number]

const office: AppRole[] = ['admin', 'manager', 'dispatcher', 'viewer']
const floor: AppRole[] = [...office, 'technician']

/**
 * The tenant sidebar: factory maintenance first, grouped so a person sees a handful of sections
 * rather than one long list. Screens kept from the service-company product (the generic work
 * order list, client and contract registers, meetings and action items, finance rate cards) stay
 * reachable by URL and from the screens that link to them, but are not in the sidebar.
 */
export const tenantNavItems: NavItem[] = [
  { group: 'Daily work', label: 'Dashboard', to: '/dashboard', roles: floor, permission: 'dashboard.view', icon: LayoutDashboard },
  { group: 'Daily work', label: 'Factory overview', to: '/maintenance/overview', roles: office, permission: 'work-orders.view', icon: Factory },
  { group: 'Daily work', label: 'Jobs', to: '/maintenance/jobs', roles: floor, permission: 'work-orders.view', icon: Wrench },
  { group: 'Daily work', label: 'Running checks', to: '/maintenance/checks', roles: floor, permission: 'work-orders.view', icon: ClipboardCheck },
  { group: 'Daily work', label: 'Scan', to: '/scan', roles: [...floor, 'materials_controller'], icon: ScanLine },
  { group: 'Daily work', label: 'Approvals', to: '/approvals', roles: ['admin', 'manager', 'finance', 'materials_controller'], icon: CheckSquare },

  // Factory Operations add-ons. Visitors: any staff member hosts; gate staff get the desk. Stores and
  // finance users can still open /visitors to host, but it is kept out of their short sidebars.
  { group: 'Site security', label: 'Visitors', to: '/visitors', roles: floor, icon: DoorOpen },

  { group: 'Planning', label: 'Planned maintenance', to: '/maintenance/pm', roles: office, permission: 'work-orders.view', icon: Gauge },
  { group: 'Planning', label: 'Weekly window', to: '/maintenance/windows', roles: floor, permission: 'work-orders.view', icon: CalendarRange },
  { group: 'Planning', label: 'Overhaul', to: '/maintenance/overhaul', roles: office, permission: 'work-orders.view', icon: HardHat },
  { group: 'Planning', label: 'Calendar', to: '/maintenance/calendar', roles: floor, permission: 'work-orders.view', icon: CalendarDays },

  { group: 'Assets', label: 'Assets', to: '/assets', roles: [...floor, 'materials_controller'], icon: Boxes },
  { group: 'Assets', label: 'Condition', to: '/maintenance/condition', roles: floor, permission: 'work-orders.view', icon: Activity },
  { group: 'Assets', label: 'Reliability', to: '/maintenance/reliability', roles: office, permission: 'work-orders.view', icon: Repeat },
  { group: 'Assets', label: 'Fleet', to: '/maintenance/fleet', roles: floor, permission: 'work-orders.view', icon: Truck },

  { group: 'Stores', label: 'Stores and spares', to: '/maintenance/stores', roles: [...floor, 'materials_controller'], permission: 'materials.view', icon: Warehouse },
  { group: 'Stores', label: 'Purchasing', to: '/inventory/purchase-orders', roles: ['admin', 'manager', 'materials_controller'], permission: 'materials.view', icon: Package },
  { group: 'Stores', label: 'Contractors', to: '/maintenance/contractors', roles: office, permission: 'work-orders.view', icon: ContractorIcon },

  { group: 'Compliance', label: 'Statutory', to: '/maintenance/statutory', roles: office, permission: 'work-orders.view', icon: FileCheck2 },
  { group: 'Compliance', label: 'Safety', to: '/maintenance/safety', roles: floor, permission: 'work-orders.view', icon: ShieldAlert },
  { group: 'Compliance', label: 'Audits and evidence', to: '/maintenance/compliance', roles: office, permission: 'work-orders.view', icon: BadgeCheck },

  { group: 'Insights', label: 'Performance', to: '/maintenance/performance', roles: office, permission: 'work-orders.view', icon: TrendingUp },
  { group: 'Insights', label: 'Costs', to: '/maintenance/costs', roles: ['admin', 'manager', 'finance', 'viewer'], permission: 'reports.view', icon: Landmark },
  { group: 'Insights', label: 'Labour rates', to: '/finance', roles: ['admin', 'manager', 'finance'], permission: 'finance.view', icon: Wallet },
  { group: 'Insights', label: 'Reports', to: '/reports', roles: ['admin', 'manager', 'finance', 'viewer', 'materials_controller'], permission: 'reports.view', icon: BarChart3 },

  { group: 'Team and admin', label: 'Maintenance team', to: '/maintenance/people', roles: office, permission: 'work-orders.view', icon: UserCog },
  // Self-service clock in/out, leave and timesheet.
  { group: 'Team and admin', label: 'My time and leave', to: '/my-time', roles: ['manager', 'dispatcher', 'technician', 'materials_controller'], icon: CalendarClock },
  ...(WHATSAPP_ENABLED ? [{ group: 'Team and admin' as NavGroup, label: 'WhatsApp', to: '/whatsapp', roles: office, permission: 'whatsapp.view', icon: MessageCircle }] : []),
  { group: 'Team and admin', label: 'Users and access', to: '/team', roles: ['admin'], permission: 'team.view', icon: Users },
  { group: 'Team and admin', label: 'Settings', to: '/settings', roles: ['admin'], permission: 'settings.view', icon: Settings2 },
  { group: 'Team and admin', label: 'Help', to: '/help', roles: ['admin', 'manager', 'dispatcher', 'technician', 'materials_controller', 'finance', 'viewer'], icon: LifeBuoy },
]

/**
 * The platform (command-centre) sections and, per section, the roles allowed to open it.
 *
 * These `roles` are load-bearing in two places — `PlatformShell` renders only the items the
 * current role may reach, and `PlatformApp` refuses direct navigation to the rest — so they
 * must stay aligned with the server-side policies each section's controller declares
 * (`AddPlatformPolicy` in `backend/src/Ecosys.Api/Program.cs`). Hiding a link is presentation;
 * the server remains the authority.
 */
export const platformNavItems: NavItem[] = [
  // `/platform/overview` rather than `/platform`: the shell's active-state test treats `to` as a
  // path prefix, so a bare `/platform` matched every platform route and kept Overview highlighted.
  { label: 'Overview', to: '/platform/overview', roles: ['super_admin', 'platform_sales'], permission: 'platform.dashboard.view', icon: ShieldCheck },
  { label: 'Tenants', to: '/platform/tenants', roles: ['super_admin', 'platform_sales'], permission: 'platform.tenants.view', icon: Building2 },
  // The demo endpoints are platform-admin only (PlatformAdminAccess).
  { label: 'Demo workspace', to: '/platform/demo', roles: ['super_admin'], permission: 'platform.tenants.view', icon: Factory },
  { label: 'Finance', to: '/platform/finance', roles: ['super_admin'], permission: 'platform.finance.view', icon: Wallet },
  { label: 'Users', to: '/platform/users', roles: ['super_admin'], permission: 'platform.users.view', icon: Users },
  { label: 'Licensing', to: '/platform/licensing', roles: ['super_admin'], permission: 'platform.licensing.view', icon: FileText },
  { label: 'Reports', to: '/platform/reports', roles: ['super_admin'], permission: 'platform.reports.view', icon: BarChart3 },
  { label: 'Audit Logs', to: '/platform/audit', roles: ['super_admin'], permission: 'platform.audit.view', icon: Settings2 },
  { label: 'System Health', to: '/platform/health', roles: ['super_admin'], permission: 'platform.settings.view', icon: Activity },
  { label: 'Settings', to: '/platform/settings', roles: ['super_admin'], permission: 'platform.settings.view', icon: Settings2 },
]
