import { E2E_API_BASE_URL } from './helpers/env'

export default async function globalSetup() {
  if (process.env.PLAYWRIGHT_SKIP_BACKEND_PRECHECK === '1') {
    return
  }

  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), 5000)

  try {
    const response = await fetch(`${E2E_API_BASE_URL.replace(/\/+$/, '')}/health`, {
      signal: controller.signal,
    })
    if (!response.ok) {
      throw new Error(`health check returned HTTP ${response.status}`)
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : 'unknown error'
    throw new Error(
      `Playwright backend precheck failed for ${E2E_API_BASE_URL}. Start the API before running the full suite, or set PLAYWRIGHT_SKIP_BACKEND_PRECHECK=1 for mock-only runs. Original error: ${message}`,
      { cause: error },
    )
  } finally {
    clearTimeout(timeout)
  }
}
