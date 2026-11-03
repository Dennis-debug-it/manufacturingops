import { expect, type Page } from '@playwright/test'

type GuardOptions = {
  appBaseUrl?: string
  apiBaseUrl: string
}

type Violation = {
  kind: 'console-error' | 'http-404' | 'http-401-after-login' | 'http-429' | 'api-500'
  message: string
}

function normalizeBase(url: string) {
  return url.replace(/\/+$/, '')
}

function isTrackedResponse(url: string, appBaseUrl?: string, apiBaseUrl?: string) {
  if (appBaseUrl && url.startsWith(normalizeBase(appBaseUrl))) return true
  if (apiBaseUrl && url.startsWith(normalizeBase(apiBaseUrl))) return true
  return false
}

function isApiRequest(url: string) {
  return /\/api(\/|$)/i.test(url)
}

export function installPageGuards(page: Page, options: GuardOptions) {
  const violations: Violation[] = []
  let authenticated = false

  const onConsole = (message: { type: () => string; text: () => string }) => {
    if (message.type() !== 'error') return
    violations.push({
      kind: 'console-error',
      message: message.text(),
    })
  }

  const onResponse = (response: { url: () => string; status: () => number }) => {
    const url = response.url()
    const status = response.status()

    if (!isTrackedResponse(url, options.appBaseUrl, options.apiBaseUrl)) return

    if (status === 429) {
      violations.push({
        kind: 'http-429',
        message: `${status} from ${url}`,
      })
      return
    }

    if (status === 404) {
      violations.push({
        kind: 'http-404',
        message: `${status} from ${url}`,
      })
      return
    }

    if (isApiRequest(url) && status >= 500) {
      violations.push({
        kind: 'api-500',
        message: `${status} from ${url}`,
      })
      return
    }

    if (authenticated && isApiRequest(url) && status === 401) {
      violations.push({
        kind: 'http-401-after-login',
        message: `${status} from ${url}`,
      })
    }
  }

  page.on('console', onConsole)
  page.on('response', onResponse)

  return {
    markAuthenticated() {
      authenticated = true
    },
    assertNoViolations() {
      const details = violations
        .map((violation, index) => `${index + 1}. [${violation.kind}] ${violation.message}`)
        .join('\n')
      expect(
        violations,
        details || 'No guard violations',
      ).toHaveLength(0)
    },
    dispose() {
      page.off('console', onConsole)
      page.off('response', onResponse)
    },
  }
}
