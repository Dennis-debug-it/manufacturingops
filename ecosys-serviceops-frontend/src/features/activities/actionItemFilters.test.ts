import { describe, expect, it } from 'vitest'
import { filterActionItemsByDue } from './actionItemFilters'
import type { ActionItemStatus } from './types'

const today = new Date(2026, 9, 21)
const items: { id: string; dueDate: string | null; status: ActionItemStatus }[] = [
  { id: 'late', dueDate: '2026-10-20', status: 'Open' },
  { id: 'today', dueDate: '2026-10-21', status: 'Open' },
  { id: 'sixth', dueDate: '2026-10-27', status: 'Open' },
  { id: 'eighth', dueDate: '2026-10-28', status: 'Open' },
  { id: 'none', dueDate: null, status: 'Open' },
  { id: 'done-late', dueDate: '2026-10-01', status: 'Done' },
]

describe('filterActionItemsByDue', () => {
  it('keeps everything when no filter is set', () => {
    expect(filterActionItemsByDue(items, '', today)).toHaveLength(items.length)
  })

  it('overdue is open items due before today', () => {
    expect(filterActionItemsByDue(items, 'overdue', today).map((x) => x.id)).toEqual(['late'])
  })

  it('week is open items due today through six days ahead', () => {
    expect(filterActionItemsByDue(items, 'week', today).map((x) => x.id)).toEqual(['today', 'sixth'])
  })
})
