import type { AppRole } from '../types/auth'
import { LayoutDashboard, ClipboardList, Wrench, Users, Building2, Package, FileText, Wallet, BarChart3, Settings2, ShieldCheck, Megaphone, MessageCircle } from 'lucide-react'

export interface NavItem {
  label: string
  to: string
  roles: AppRole[]
  permission?: string
  icon: typeof LayoutDashboard
}

export const tenantNavItems: NavItem[] = [
  { label: 'Dashboard', to: '/dashboard', roles: ['admin', 'manager', 'dispatcher', 'technician', 'finance', 'viewer'], permission: 'dashboard.view', icon: LayoutDashboard },
  { label: 'Work Orders', to: '/work-orders', roles: ['admin', 'manager', 'dispatcher', 'technician', 'viewer'], permission: 'work-orders.view', icon: ClipboardList },
  { label: 'Maintenance', to: '/maintenance/calendar', roles: ['admin', 'manager', 'dispatcher', 'technician', 'viewer'], permission: 'work-orders.view', icon: Wrench },
  { label: 'Clients', to: '/clients', roles: ['admin', 'manager', 'dispatcher', 'finance', 'viewer'], permission: 'clients.view', icon: Building2 },
  { label: 'Inventory', to: '/inventory', roles: ['admin', 'manager', 'dispatcher', 'technician', 'finance', 'viewer'], permission: 'inventory.view', icon: Package },
  { label: 'Team', to: '/team', roles: ['admin', 'manager', 'viewer'], permission: 'team.view', icon: Users },
  { label: 'Contracts', to: '/contracts', roles: ['admin', 'manager', 'finance', 'viewer'], permission: 'contracts.view', icon: FileText },
  { label: 'Finance', to: '/finance', roles: ['admin', 'manager', 'finance', 'viewer'], permission: 'finance.view', icon: Wallet },
  { label: 'Reports', to: '/reports', roles: ['admin', 'manager', 'finance', 'viewer'], permission: 'reports.view', icon: BarChart3 },
  { label: 'WhatsApp', to: '/whatsapp', roles: ['admin', 'manager', 'dispatcher', 'viewer'], permission: 'whatsapp.view', icon: MessageCircle },
  { label: 'Settings', to: '/settings', roles: ['admin'], permission: 'settings.view', icon: Settings2 },
]

export const platformNavItems: NavItem[] = [
  { label: 'Overview', to: '/platform', roles: ['super_admin', 'platform_sales'], permission: 'platform.dashboard.view', icon: ShieldCheck },
  { label: 'Tenants', to: '/platform/tenants', roles: ['super_admin', 'platform_sales'], permission: 'platform.tenants.view', icon: Building2 },
  { label: 'Leads', to: '/platform/leads', roles: ['super_admin', 'platform_sales'], permission: 'platform.crm.view', icon: Megaphone },
  { label: 'Finance', to: '/platform/finance', roles: ['super_admin'], permission: 'platform.finance.view', icon: Wallet },
  { label: 'Users', to: '/platform/users', roles: ['super_admin'], permission: 'platform.users.view', icon: Users },
  { label: 'Licensing', to: '/platform/licensing', roles: ['super_admin'], permission: 'platform.licensing.view', icon: FileText },
  { label: 'Reports', to: '/platform/reports', roles: ['super_admin'], permission: 'platform.reports.view', icon: BarChart3 },
  { label: 'Audit Logs', to: '/platform/audit', roles: ['super_admin'], permission: 'platform.audit.view', icon: Settings2 },
  { label: 'Settings', to: '/platform/settings', roles: ['super_admin'], permission: 'platform.settings.view', icon: Settings2 },
]
