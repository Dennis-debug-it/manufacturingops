import type { DashboardLayoutItem, DashboardWidgetInfo, WidgetSize } from './roleDashboardTypes'

/**
 * Pure edit operations for the dashboard editor. The server is the authority (it drops anything the
 * user may not see when the layout is saved); these keep the draft tidy in the meantime.
 */

/** Keeps only widgets the user may see, each once, with a valid size. */
export function normalizeLayout(items: DashboardLayoutItem[], available: DashboardWidgetInfo[]): DashboardLayoutItem[] {
  const byKey = new Map(available.map((widget) => [widget.key, widget]))
  const seen = new Set<string>()
  const result: DashboardLayoutItem[] = []
  for (const item of items) {
    const widget = byKey.get(item.key)
    if (!widget || seen.has(item.key)) continue
    seen.add(item.key)
    result.push({ key: item.key, size: item.size === 'small' || item.size === 'wide' ? item.size : widget.defaultSize })
  }
  return result
}

/** Moves a widget one place earlier (-1) or later (+1). At either end it stays put. */
export function moveItem(items: DashboardLayoutItem[], key: string, direction: -1 | 1): DashboardLayoutItem[] {
  const index = items.findIndex((item) => item.key === key)
  const target = index + direction
  if (index < 0 || target < 0 || target >= items.length) return items
  const next = [...items]
  const [moved] = next.splice(index, 1)
  next.splice(target, 0, moved!)
  return next
}

export function removeItem(items: DashboardLayoutItem[], key: string): DashboardLayoutItem[] {
  return items.filter((item) => item.key !== key)
}

/** Adds a widget at the end in its default size, unless it is already on the dashboard. */
export function addItem(items: DashboardLayoutItem[], widget: DashboardWidgetInfo): DashboardLayoutItem[] {
  if (items.some((item) => item.key === widget.key)) return items
  return [...items, { key: widget.key, size: widget.defaultSize }]
}

export function setItemSize(items: DashboardLayoutItem[], key: string, size: WidgetSize): DashboardLayoutItem[] {
  return items.map((item) => (item.key === key ? { ...item, size } : item))
}

/** Widgets the user may add: those they can see that aren't on the dashboard yet, in catalogue order. */
export function addableWidgets(items: DashboardLayoutItem[], available: DashboardWidgetInfo[]): DashboardWidgetInfo[] {
  const used = new Set(items.map((item) => item.key))
  return available.filter((widget) => !used.has(widget.key))
}

export function sameLayout(a: DashboardLayoutItem[], b: DashboardLayoutItem[]): boolean {
  return a.length === b.length && a.every((item, index) => item.key === b[index]?.key && item.size === b[index]?.size)
}
