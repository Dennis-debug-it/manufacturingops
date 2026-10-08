import { describe, expect, it } from 'vitest'
import type { AuthUser, PermissionSet } from '../types/auth'
import { isTechnicianWorkspaceUser, techPathFor } from './technicianWorkspace'

const user = (role: string, permissions: Partial<PermissionSet>) => ({ role, permissions }) as unknown as AuthUser

describe('isTechnicianWorkspaceUser', () => {
  it('is true for someone who can only complete work orders', () => {
    expect(isTechnicianWorkspaceUser(user('technician', { canViewWorkOrders: true, canCompleteWorkOrders: true }))).toBe(true)
  })
  it('is false once any management permission is held', () => {
    expect(isTechnicianWorkspaceUser(user('technician', { canCompleteWorkOrders: true, canAssignWorkOrders: true }))).toBe(false)
  })
  it('is false for admins and for users who cannot complete work', () => {
    expect(isTechnicianWorkspaceUser(user('admin', { canCompleteWorkOrders: true }))).toBe(false)
    expect(isTechnicianWorkspaceUser(user('viewer', { canViewWorkOrders: true }))).toBe(false)
    expect(isTechnicianWorkspaceUser(null)).toBe(false)
  })
})

describe('techPathFor', () => {
  it('maps office URLs to the matching technician screen', () => {
    expect(techPathFor('/work-orders/abc')).toBe('/tech/jobs/abc')
    expect(techPathFor('/work-orders/abc/report')).toBe('/tech/jobs/abc')
    expect(techPathFor('/work-orders')).toBe('/tech')
    expect(techPathFor('/my-time')).toBe('/tech/time')
    expect(techPathFor('/team/time-attendance/leave')).toBe('/tech/time')
    expect(techPathFor('/knowledge')).toBe('/tech/guides')
    expect(techPathFor('/knowledge/k1')).toBe('/tech/guides/k1')
    expect(techPathFor('/me')).toBe('/tech/account')
    expect(techPathFor('/settings/account')).toBe('/tech/account')
    expect(techPathFor('/inventory/vans/v1')).toBe('/tech/account')
  })

  it('sends any other office page to My Jobs', () => {
    expect(techPathFor('/finance/invoices')).toBe('/tech')
    expect(techPathFor('/approvals')).toBe('/tech')
  })
})
