import { describe, expect, it } from 'vitest'
import { toWorkOrderListFilters, type WorkOrderFilterValues } from './listModel'

const none: WorkOrderFilterValues = {
  search: '', status: '', technicianId: '', clientId: '', siteId: '', priority: '', slaStatus: '',
  unassignedOnly: false, fromDate: '', toDate: '', showAll: false,
}

describe('work order list scope', () => {
  it('shows open and recently closed jobs by default', () => {
    expect(toWorkOrderListFilters(none)).toEqual({ view: 'current' })
  })

  it('keeps the default scope for filters about open work', () => {
    expect(toWorkOrderListFilters({ ...none, priority: 'High', unassignedOnly: true, slaStatus: 'breached' }).view).toBe('current')
  })

  it.each<[string, Partial<WorkOrderFilterValues>]>([
    ['a search', { search: 'WO-000123' }],
    ['a status', { status: 'Closed' }],
    ['a client', { clientId: 'c1' }],
    ['a site', { siteId: 's1' }],
    ['a technician', { technicianId: 't1' }],
    ['a date range', { fromDate: '2025-01-01' }],
    ['show all', { showAll: true }],
  ])('shows the full history for %s', (_, patch) => {
    expect(toWorkOrderListFilters({ ...none, ...patch }).view).toBeUndefined()
  })

  it('does not treat a blank search as a search', () => {
    expect(toWorkOrderListFilters({ ...none, search: '   ' }).view).toBe('current')
  })
})
