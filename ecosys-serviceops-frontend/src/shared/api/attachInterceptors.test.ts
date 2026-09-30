import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { AxiosInstance } from 'axios'
import toast from 'react-hot-toast'
import { attachAuthInterceptors, presentBackgroundFailure } from './attachInterceptors'
import { ApiError } from './apiError'

vi.mock('react-hot-toast', () => ({
  default: { error: vi.fn(), success: vi.fn() },
}))

/** A minimal fake AxiosInstance that just captures the response interceptor's error handler. */
function createFakeClient() {
  let onResponseError: ((error: unknown) => Promise<unknown>) | undefined
  const client = {
    interceptors: {
      request: { use: vi.fn() },
      response: {
        use: vi.fn((_onSuccess: unknown, onError: (error: unknown) => Promise<unknown>) => {
          onResponseError = onError
        }),
      },
    },
  } as unknown as AxiosInstance
  return { client, invokeResponseError: (error: unknown) => onResponseError!(error) }
}

/**
 * Locks in the alert-spam fix (Phases 17-20): a dashboard firing many parallel requests that all
 * fail for the same underlying reason must produce exactly one user-facing signal, not one per
 * request — and license/connectivity failures must never double up with their own banners.
 */
describe('presentBackgroundFailure', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('does not toast a genuine network/connectivity failure — the ConnectivityBanner covers it', () => {
    const error = new ApiError("Can't reach the server. Check your connection and try again.", { isNetworkError: true })

    presentBackgroundFailure(error)

    expect(toast.error).not.toHaveBeenCalled()
  })

  it('does not toast a suspended-license failure — the LicenseWarningBanner already covers it', () => {
    const error = new ApiError('Tenant license is suspended. Contact Ecosys to restore access.', {
      status: 403,
      errorCode: 'license_suspended',
    })

    presentBackgroundFailure(error)

    expect(toast.error).not.toHaveBeenCalled()
  })

  it('does not toast a read-only-license failure', () => {
    const error = new ApiError('Tenant license has expired and the workspace is now read-only.', {
      status: 403,
      errorCode: 'license_read_only',
    })

    presentBackgroundFailure(error)

    expect(toast.error).not.toHaveBeenCalled()
  })

  it('toasts a generic server error, keyed by status so it can be deduped', () => {
    const error = new ApiError('Something went wrong on our end. Please try again in a moment.', { status: 599 })

    presentBackgroundFailure(error)

    expect(toast.error).toHaveBeenCalledTimes(1)
    expect(toast.error).toHaveBeenCalledWith(error.message, { id: '599' })
  })

  it('collapses many concurrent identical failures (e.g. a dashboard firing parallel queries) into one toast', () => {
    const error = new ApiError('Something went wrong.', { status: 598, errorCode: 'dashboard_parallel_failure' })

    presentBackgroundFailure(error)
    presentBackgroundFailure(error)
    presentBackgroundFailure(error)
    presentBackgroundFailure(error)
    presentBackgroundFailure(error)

    expect(toast.error).toHaveBeenCalledTimes(1)
  })
})

/**
 * Phase 22: an explicit user-action request (a PDF download, a save) that already shows its own
 * contextual message must not ALSO get the generic background-failure toast for the same failure.
 */
describe('attachAuthInterceptors — suppressGlobalErrorToast', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('skips the background toast when the request opted out', async () => {
    const { client, invokeResponseError } = createFakeClient()
    attachAuthInterceptors(client, {
      getToken: () => null,
      clearSession: vi.fn(),
      triggerLogout: vi.fn(),
      loginPath: '/login',
    })

    await expect(
      invokeResponseError({
        isAxiosError: true,
        response: { status: 500, data: { message: 'Could not download the PDF' } },
        config: { url: '/api/invoices/123/pdf', suppressGlobalErrorToast: true },
      }),
    ).rejects.toBeInstanceOf(ApiError)

    expect(toast.error).not.toHaveBeenCalled()
  })

  it('still shows the background toast when the request did not opt out', async () => {
    const { client, invokeResponseError } = createFakeClient()
    attachAuthInterceptors(client, {
      getToken: () => null,
      clearSession: vi.fn(),
      triggerLogout: vi.fn(),
      loginPath: '/login',
    })

    await expect(
      invokeResponseError({
        isAxiosError: true,
        response: { status: 500, data: { message: 'Server error' } },
        config: { url: '/api/work-orders' },
      }),
    ).rejects.toBeInstanceOf(ApiError)

    expect(toast.error).toHaveBeenCalledTimes(1)
  })
})

describe('attachAuthInterceptors — a 401 after another tab switched user', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('reloads as the new user instead of refreshing or clearing their session', async () => {
    const reload = vi.fn()
    vi.stubGlobal('window', { location: { reload, pathname: '/work-orders', assign: vi.fn() } })
    const { client, invokeResponseError } = createFakeClient()
    const clearSession = vi.fn()
    const refresh = vi.fn()
    attachAuthInterceptors(client, {
      getToken: () => 'admin-token',
      clearSession,
      triggerLogout: vi.fn(),
      loginPath: '/login',
      refresh,
      syncWithOtherTabs: () => 'switched',
    })

    await expect(
      invokeResponseError({
        isAxiosError: true,
        response: { status: 401, data: {} },
        config: { url: '/api/work-orders', headers: { Authorization: 'Bearer tech-token' } },
      }),
    ).rejects.toBeInstanceOf(ApiError)

    expect(reload).toHaveBeenCalledTimes(1)
    expect(refresh).not.toHaveBeenCalled()
    expect(clearSession).not.toHaveBeenCalled()
    vi.unstubAllGlobals()
  })
})
