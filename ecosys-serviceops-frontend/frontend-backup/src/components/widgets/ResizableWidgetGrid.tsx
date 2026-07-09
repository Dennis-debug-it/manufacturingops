import 'react-grid-layout/css/styles.css'
import 'react-resizable/css/styles.css'
import { Responsive } from 'react-grid-layout'
import type { Layout, LayoutItem, ResponsiveLayouts } from 'react-grid-layout'
import type { ReactNode } from 'react'
import { useEffect, useMemo, useRef } from 'react'
import { useContainerWidth } from 'react-grid-layout'
import { usePersistentState } from '../../hooks/usePersistentState'
import { WidgetCard } from './WidgetCard'

const ResponsiveGrid = Responsive

const GRID_BREAKPOINTS = {
  lg: 1280,
  md: 1024,
  sm: 768,
  xs: 0,
} as const

const GRID_COLUMNS = {
  lg: 12,
  md: 12,
  sm: 6,
  xs: 1,
} as const

type WidgetBreakpoint = keyof typeof GRID_BREAKPOINTS
type GridLayout = LayoutItem[]
type GridLayouts = Partial<Record<WidgetBreakpoint, GridLayout>>

export type ResizableWidgetLayout = LayoutItem

export type ResizableWidgetItem = {
  id: string
  title: string
  description?: string
  actions?: ReactNode
  content: ReactNode
  className?: string
  showDragHandle?: boolean
  minW?: number
  minH?: number
  maxW?: number
  maxH?: number
  defaultLayout: ResizableWidgetLayout
}

function isLayoutItem(value: unknown): value is LayoutItem {
  if (!value || typeof value !== 'object') return false
  const candidate = value as Partial<LayoutItem>
  return typeof candidate.i === 'string'
    && typeof candidate.x === 'number'
    && typeof candidate.y === 'number'
    && typeof candidate.w === 'number'
    && typeof candidate.h === 'number'
}

function isWidgetLayouts(value: unknown): value is GridLayouts {
  if (!value || typeof value !== 'object') return false
  const candidate = value as Record<string, unknown>
  return Object.keys(candidate).every((key) => {
    const entries = candidate[key]
    return Array.isArray(entries) && entries.every(isLayoutItem)
  })
}

function clampLayout(item: LayoutItem, cols: number): LayoutItem {
  const width = Math.min(Math.max(Math.round(item.w || 1), 1), cols)
  const height = Math.max(Math.round(item.h || 1), 1)
  const maxX = Math.max(cols - width, 0)
  const x = cols === 1 ? 0 : Math.min(Math.max(Math.round(item.x || 0), 0), maxX)
  const y = Math.max(Math.round(item.y || 0), 0)

  return {
    ...item,
    x,
    y,
    w: width,
    h: height,
  }
}

function applyItemConstraints(layout: LayoutItem, item?: ResizableWidgetItem): LayoutItem {
  if (!item) return layout
  return {
    ...layout,
    minW: item.minW,
    minH: item.minH,
    maxW: item.maxW,
    maxH: item.maxH,
  }
}

function sortLayouts(layouts: GridLayout) {
  return [...layouts].sort((left, right) => (left.y - right.y) || (left.x - right.x) || left.i.localeCompare(right.i))
}

function buildDefaultLayouts(items: ResizableWidgetItem[]): GridLayouts {
  return {
    lg: sortLayouts(items.map((item) => applyItemConstraints(clampLayout({ ...item.defaultLayout, i: item.id }, GRID_COLUMNS.lg), item))),
  }
}

function sanitizeLayouts(value: unknown, items: ResizableWidgetItem[], fallback: GridLayouts): GridLayouts {
  const itemById = new Map(items.map((item) => [item.id, item]))
  const sanitized: GridLayouts = {}
  const source = isWidgetLayouts(value) ? value : {}

  ;(['lg', 'md', 'sm', 'xs'] as const).forEach((breakpoint) => {
    const cols = GRID_COLUMNS[breakpoint]
    const fallbackLayouts = fallback[breakpoint] ?? fallback.lg ?? []
    const nextLayouts = [...(source[breakpoint] ?? fallbackLayouts)]
      .filter((layout) => typeof layout?.i === 'string' && itemById.has(layout.i))
      .map((layout) => applyItemConstraints(
        clampLayout(
          {
            ...itemById.get(layout.i)?.defaultLayout,
            ...layout,
            i: layout.i,
          },
          cols,
        ),
        itemById.get(layout.i),
      ))

    items.forEach((item) => {
      if (!nextLayouts.some((layout) => layout.i === item.id)) {
        nextLayouts.push(applyItemConstraints(clampLayout({ ...item.defaultLayout, i: item.id }, cols), item))
      }
    })

    sanitized[breakpoint] = sortLayouts(nextLayouts)
  })

  return sanitized
}

export function ResizableWidgetGrid({
  items,
  storageKey,
  legacyStorageKeys = [],
  className = '',
  resetSignal = 0,
}: {
  items: ResizableWidgetItem[]
  storageKey: string
  legacyStorageKeys?: string[]
  className?: string
  resetSignal?: number
}) {
  const { width, containerRef, mounted } = useContainerWidth({ measureBeforeMount: true, initialWidth: 1440 })
  const defaultLayouts = useMemo(() => buildDefaultLayouts(items), [items])
  const storageFallback = useMemo(() => readStoredLayouts(storageKey, legacyStorageKeys, defaultLayouts), [defaultLayouts, legacyStorageKeys, storageKey])
  const { value: storedLayouts, setValue: setStoredLayouts } = usePersistentState<GridLayouts>(
    storageKey,
    storageFallback,
    { validate: isWidgetLayouts },
  )
  const layouts = useMemo(() => sanitizeLayouts(storedLayouts, items, defaultLayouts), [defaultLayouts, items, storedLayouts])
  const previousResetSignal = useRef(resetSignal)

  useEffect(() => {
    if (previousResetSignal.current !== resetSignal) {
      previousResetSignal.current = resetSignal
      setStoredLayouts(defaultLayouts)
    }
  }, [defaultLayouts, resetSignal, setStoredLayouts])

  return (
    <div ref={containerRef} className={`widget-grid-shell min-w-0 ${className}`.trim()}>
      {mounted ? (
        <ResponsiveGrid
          className="widget-grid"
          width={width}
          breakpoints={GRID_BREAKPOINTS}
          cols={GRID_COLUMNS}
          layouts={layouts as ResponsiveLayouts}
          margin={[16, 16]}
          containerPadding={[0, 0]}
          rowHeight={28}
          dragConfig={{
            enabled: width >= GRID_BREAKPOINTS.md,
            bounded: false,
            handle: '.widget-drag-handle',
            cancel: '.widget-no-drag,button,a,input,textarea,select,label',
          }}
          resizeConfig={{ enabled: width >= GRID_BREAKPOINTS.md, handles: ['se'] }}
          onLayoutChange={(_currentLayout: Layout, allLayouts: ResponsiveLayouts) => {
            setStoredLayouts(sanitizeLayouts(allLayouts, items, defaultLayouts))
          }}
        >
          {items.map((item) => (
            <div key={item.id} className="min-w-0">
              <WidgetCard
                title={item.title}
                description={item.description}
                actions={item.actions}
                className={item.className}
                showDragHandle={width >= GRID_BREAKPOINTS.md && item.showDragHandle !== false}
              >
                <div className="widget-card-content h-full min-h-0 overflow-hidden">
                  {item.content}
                </div>
              </WidgetCard>
            </div>
          ))}
        </ResponsiveGrid>
      ) : (
        <div className="min-h-[240px]" />
      )}
    </div>
  )
}

function readStoredLayouts(storageKey: string, legacyStorageKeys: string[], fallback: GridLayouts): GridLayouts {
  if (typeof window === 'undefined') {
    return fallback
  }

  const candidateKeys = [storageKey, ...legacyStorageKeys]
  for (const key of candidateKeys) {
    try {
      const raw = window.localStorage.getItem(key)
      if (!raw) {
        continue
      }

      const parsed = JSON.parse(raw) as unknown
      if (isWidgetLayouts(parsed)) {
        return parsed
      }
    } catch {
      // Ignore malformed layout payloads and fall back to the current/default layout.
    }
  }

  return fallback
}
