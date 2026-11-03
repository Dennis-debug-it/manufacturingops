import { describe, expect, it } from 'vitest'
import { normalizeRole } from './normalizeRole'

describe('normalizeRole', () => {
  it('maps the stores role the API sends', () => {
    expect(normalizeRole('MaterialsController')).toBe('materials_controller')
    expect(normalizeRole('materials controller')).toBe('materials_controller')
  })

  it('keeps the other tenant roles', () => {
    expect(normalizeRole('Dispatcher')).toBe('dispatcher')
    expect(normalizeRole('Technician')).toBe('technician')
    expect(normalizeRole('finance')).toBe('finance')
  })

  it('treats an unknown role as read-only', () => {
    expect(normalizeRole('Storeroom wizard')).toBe('viewer')
  })
})
