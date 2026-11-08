import { describe, expect, it } from 'vitest'
import { addItem, addableWidgets, moveItem, normalizeLayout, removeItem, sameLayout, setItemSize, widgetKeysForAreas } from './layoutModel'
import type { DashboardLayoutItem, DashboardWidgetInfo } from './roleDashboardTypes'

const widget = (key: string, defaultSize: 'small' | 'wide' = 'small', areas: string[] = ['work-orders']): DashboardWidgetInfo => ({
  key,
  title: key,
  description: '',
  defaultSize,
  modules: ['operations'],
  areas,
})

const available = [widget('a'), widget('b', 'wide'), widget('c')]
const layout: DashboardLayoutItem[] = [
  { key: 'a', size: 'small' },
  { key: 'b', size: 'wide' },
  { key: 'c', size: 'small' },
]

describe('dashboard layout model', () => {
  it('drops widgets the user may not see, duplicates and bad sizes', () => {
    const messy = [
      { key: 'b', size: 'huge' },
      { key: 'gone', size: 'small' },
      { key: 'a', size: 'wide' },
      { key: 'b', size: 'small' },
    ] as unknown as DashboardLayoutItem[]
    expect(normalizeLayout(messy, available)).toEqual([
      { key: 'b', size: 'wide' },
      { key: 'a', size: 'wide' },
    ])
  })

  it('moves a widget up and down, and not past either end', () => {
    expect(moveItem(layout, 'b', -1).map((x) => x.key)).toEqual(['b', 'a', 'c'])
    expect(moveItem(layout, 'b', 1).map((x) => x.key)).toEqual(['a', 'c', 'b'])
    expect(moveItem(layout, 'a', -1)).toBe(layout)
    expect(moveItem(layout, 'c', 1)).toBe(layout)
    expect(moveItem(layout, 'missing', 1)).toBe(layout)
  })

  it('hides, adds once at the end and resizes', () => {
    const hidden = removeItem(layout, 'b')
    expect(hidden.map((x) => x.key)).toEqual(['a', 'c'])
    expect(addableWidgets(hidden, available).map((x) => x.key)).toEqual(['b'])

    const added = addItem(hidden, widget('b', 'wide'))
    expect(added).toEqual([...hidden, { key: 'b', size: 'wide' }])
    expect(addItem(added, widget('b', 'wide'))).toBe(added)
    expect(addableWidgets(added, available)).toEqual([])

    expect(setItemSize(added, 'a', 'wide')[0]).toEqual({ key: 'a', size: 'wide' })
  })

  it('compares layouts by order and size', () => {
    expect(sameLayout(layout, [...layout])).toBe(true)
    expect(sameLayout(layout, moveItem(layout, 'b', 1))).toBe(false)
    expect(sameLayout(layout, setItemSize(layout, 'c', 'wide'))).toBe(false)
    expect(sameLayout(layout, removeItem(layout, 'c'))).toBe(false)
  })

  it('picks the widgets that read a changed data area', () => {
    const catalogue = [widget('wo'), widget('pm', 'small', ['maintenance', 'work-orders']), widget('cash', 'small', ['finance']), widget('billing', 'wide', ['contracts', 'finance'])]
    expect(widgetKeysForAreas(catalogue, ['work-orders'])).toEqual(['wo', 'pm'])
    expect(widgetKeysForAreas(catalogue, ['finance', 'contracts'])).toEqual(['cash', 'billing'])
    expect(widgetKeysForAreas(catalogue, [])).toEqual([])
  })
})
