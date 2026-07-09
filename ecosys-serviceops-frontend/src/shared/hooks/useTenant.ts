import { useTenantContext } from '../contexts/TenantContext'

export function useTenant() {
  return useTenantContext()
}
