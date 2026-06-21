export const E2E_API_BASE_URL =
  process.env.E2E_API_BASE_URL ||
  process.env.VITE_API_BASE_URL ||
  'http://localhost:5072'

export const TENANT_EMAIL = process.env.E2E_TENANT_EMAIL
export const TENANT_PASSWORD = process.env.E2E_TENANT_PASSWORD

export const SUPERADMIN_EMAIL = process.env.E2E_SUPERADMIN_EMAIL
export const SUPERADMIN_PASSWORD = process.env.E2E_SUPERADMIN_PASSWORD

export const DEFAULT_SIGNUP_PASSWORD =
  process.env.E2E_SIGNUP_PASSWORD || 'Ecosys!23456'

