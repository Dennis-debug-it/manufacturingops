export type WidgetSize = 'small' | 'wide'

export interface DashboardLayoutItem {
  key: string
  size: WidgetSize
}

export interface DashboardWidgetInfo {
  key: string
  title: string
  description: string
  defaultSize: WidgetSize
  modules: string[]
}

/** `GET /api/dashboard/layout`: the caller's widgets in order, and every widget they may add. */
export interface DashboardLayout {
  role: string
  isCustomized: boolean
  items: DashboardLayoutItem[]
  available: DashboardWidgetInfo[]
}

export type DashboardFormat = 'number' | 'money' | 'percent'
export type DashboardTone = 'warning' | 'danger'

export interface DashboardStat {
  label: string
  value: number
  format: DashboardFormat
  currency: string | null
  link: string
  tone: DashboardTone | null
}

export interface DashboardListItem {
  label: string
  detail: string | null
  value: number | null
  format: DashboardFormat | null
  currency: string | null
  link: string
  tone: DashboardTone | null
}

/** `GET /api/dashboard/widgets/{key}`. */
export interface DashboardWidgetData {
  key: string
  title: string
  stats: DashboardStat[]
  items: DashboardListItem[]
  note: string | null
  generatedAt: string
}

export interface DashboardRoleSettings {
  role: string
  defaultKeys: string[]
  hiddenKeys: string[]
}

/** `GET /api/dashboard/settings` (admin). */
export interface DashboardAdminSettings {
  widgets: DashboardWidgetInfo[]
  roles: DashboardRoleSettings[]
}
