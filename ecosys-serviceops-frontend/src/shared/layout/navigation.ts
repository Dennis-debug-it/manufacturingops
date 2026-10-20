import type { AppRole } from '../types/auth'
import { WHATSAPP_ENABLED } from '../config/features'
import { LayoutDashboard, ClipboardList, Wrench, Users, Building2, Package, FileText, Wallet, BarChart3, Settings2, ShieldCheck, Megaphone, MessageCircle, CheckSquare, MapPin, CalendarClock, Handshake, BookOpen, Activity, Presentation, LifeBuoy } from 'lucide-react'

export interface NavItem {
  label: string
  to: string
  roles: AppRole[]
  permission?: string
  icon: typeof LayoutDashboard
}

export const tenantNavItems: NavItem[] = [
  { label: 'Dashboard', to: '/dashboard', roles: ['admin', 'manager', 'dispatcher', 'technician', 'viewer'], permission: 'dashboard.view', icon: LayoutDashboard },
  { label: 'Work Orders', to: '/work-orders', roles: ['admin', 'manager', 'dispatcher', 'technician', 'viewer', 'materials_controller'], permission: 'work-orders.view', icon: ClipboardList },
  { label: 'Scheduling Board', to: '/dispatch/board', roles: ['admin', 'manager', 'dispatcher'], permission: 'dispatch.view', icon: CalendarClock },
  { label: 'Live Map', to: '/dispatch/map', roles: ['admin', 'manager', 'dispatcher'], permission: 'dispatch.view', icon: MapPin },
  { label: 'Maintenance', to: '/maintenance/calendar', roles: ['admin', 'manager', 'dispatcher', 'technician', 'viewer'], permission: 'work-orders.view', icon: Wrench },
  // Any office role can be granted the CRM in the role matrix, so the capability (not the role) decides.
  { label: 'Sales', to: '/crm', roles: ['admin', 'manager', 'dispatcher', 'finance', 'viewer', 'materials_controller'], permission: 'crm.view', icon: Handshake },
  { label: 'Clients', to: '/clients', roles: ['admin', 'manager', 'dispatcher', 'finance', 'viewer'], permission: 'clients.view', icon: Building2 },
  { label: 'Inventory', to: '/inventory', roles: ['admin', 'manager', 'dispatcher', 'technician', 'viewer', 'materials_controller'], permission: 'materials.view', icon: Package },
  { label: 'Users & Access', to: '/team', roles: ['admin'], permission: 'team.view', icon: Users },
  { label: 'Knowledge', to: '/knowledge', roles: ['admin', 'manager', 'dispatcher', 'technician', 'finance', 'viewer', 'materials_controller'], permission: 'knowledge.view', icon: BookOpen },
  { label: 'Contracts', to: '/contracts', roles: ['admin', 'manager', 'finance', 'viewer'], permission: 'contracts.view', icon: FileText },
  { label: 'Finance', to: '/finance', roles: ['admin', 'manager', 'finance', 'viewer'], permission: 'finance.view', icon: Wallet },
  // Self-service clock in/out, leave and timesheet for office staff who also go out on jobs.
  { label: 'My Time & Leave', to: '/my-time', roles: ['manager', 'dispatcher', 'technician', 'materials_controller'], icon: CalendarClock },
  { label: 'Approvals', to: '/approvals', roles: ['admin', 'manager', 'finance', 'materials_controller'], icon: CheckSquare },
  { label: 'Reports', to: '/reports', roles: ['admin', 'manager', 'finance', 'viewer', 'materials_controller'], permission: 'reports.view', icon: BarChart3 },
  { label: 'Help', to: '/help', roles: ['admin', 'manager', 'dispatcher', 'technician', 'materials_controller', 'finance', 'viewer'], icon: LifeBuoy },
  ...(WHATSAPP_ENABLED ? [{ label: 'WhatsApp', to: '/whatsapp', roles: ['admin', 'manager', 'dispatcher', 'viewer'] as AppRole[], permission: 'whatsapp.view', icon: MessageCircle }] : []),
  { label: 'Settings', to: '/settings', roles: ['admin'], permission: 'settings.view', icon: Settings2 },
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
  { label: 'Leads', to: '/platform/leads', roles: ['super_admin', 'platform_sales'], permission: 'platform.crm.view', icon: Megaphone },
  { label: 'Finance', to: '/platform/finance', roles: ['super_admin'], permission: 'platform.finance.view', icon: Wallet },
  { label: 'Users', to: '/platform/users', roles: ['super_admin'], permission: 'platform.users.view', icon: Users },
  { label: 'Licensing', to: '/platform/licensing', roles: ['super_admin'], permission: 'platform.licensing.view', icon: FileText },
  { label: 'Reports', to: '/platform/reports', roles: ['super_admin'], permission: 'platform.reports.view', icon: BarChart3 },
  { label: 'Audit Logs', to: '/platform/audit', roles: ['super_admin'], permission: 'platform.audit.view', icon: Settings2 },
  { label: 'System Health', to: '/platform/health', roles: ['super_admin'], permission: 'platform.settings.view', icon: Activity },
  { label: 'Demo Workspace', to: '/platform/demo', roles: ['super_admin'], permission: 'platform.settings.view', icon: Presentation },
  { label: 'Settings', to: '/platform/settings', roles: ['super_admin'], permission: 'platform.settings.view', icon: Settings2 },
]
