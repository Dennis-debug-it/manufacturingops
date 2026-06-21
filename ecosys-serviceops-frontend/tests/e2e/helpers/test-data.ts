export const E2E_BASE_URL = process.env.E2E_BASE_URL || 'http://127.0.0.1:4173'
export const E2E_API_URL = process.env.E2E_API_URL || process.env.E2E_API_BASE_URL || process.env.VITE_API_BASE_URL || 'http://localhost:5072'

export const E2E_PLATFORM_EMAIL =
  process.env.E2E_PLATFORM_EMAIL ||
  process.env.E2E_SUPERADMIN_EMAIL ||
  'superadmin@ecosys.local'
export const E2E_PLATFORM_PASSWORD =
  process.env.E2E_PLATFORM_PASSWORD ||
  process.env.E2E_SUPERADMIN_PASSWORD ||
  'SuperAdmin123!'

export const E2E_TENANT_EMAIL = process.env.E2E_TENANT_EMAIL || ''
export const E2E_TENANT_PASSWORD = process.env.E2E_TENANT_PASSWORD || ''

export const E2E_REAL_EMAIL = process.env.E2E_REAL_EMAIL === 'true'
export const E2E_EMAIL_DELIVERY_MODE = process.env.E2E_EMAIL_DELIVERY_MODE || 'SMTP'
export const E2E_SMTP_HOST = process.env.E2E_SMTP_HOST || ''
export const E2E_SMTP_PORT = Number(process.env.E2E_SMTP_PORT || 587)
export const E2E_SMTP_USERNAME = process.env.E2E_SMTP_USERNAME || ''
export const E2E_SMTP_PASSWORD = process.env.E2E_SMTP_PASSWORD || ''
export const E2E_SMTP_SENDER_EMAIL = process.env.E2E_SMTP_SENDER_EMAIL || ''
export const E2E_EMAIL_TEST_RECIPIENT = process.env.E2E_EMAIL_TEST_RECIPIENT || ''
export const E2E_SMTP_SECURE_MODE = process.env.E2E_SMTP_SECURE_MODE || 'Auto'

export const SAMPLE_EMAIL_INTAKE = {
  from: 'customer@example.com',
  subject: 'Generator failure at Main Branch',
  body: [
    'Generator failed during mains outage.',
    'Site: Main Branch',
    'Priority: High',
    'Asset: Generator',
  ].join('\n'),
}
