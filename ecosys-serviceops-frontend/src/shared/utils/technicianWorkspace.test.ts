import { describe, expect, it } from 'vitest'
import type { AuthUser, PermissionSet } from '../types/auth'
import { isTechnicianWorkspaceUser } from './technicianWorkspace'

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
