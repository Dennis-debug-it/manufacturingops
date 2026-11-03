import { describe, it, expect } from 'vitest'
import { AxiosError, AxiosHeaders } from 'axios'
import { normalizeError, shouldRetryQuery } from './apiError'

/**
 * Locks in the error-classification matrix (Phase 19): a genuine transport failure, a permission
 * failure, a server failure, and a validation failure must each get their own distinct message —
 * "Can't reach the server" is reserved exclusively for the case with no response at all, never for
 * an HTTP 403/500/422 the server actually answered.
 */
function axiosErrorWithResponse(status: number, data: unknown): AxiosError {
  const headers = new AxiosHeaders()
  return new AxiosError('Request failed', String(status), undefined, undefined, {
    status,
    statusText: '',
    headers,
    config: { headers },
    data,
  })
}

function axiosErrorWithNoResponse(code?: string): AxiosError {
  const err = new AxiosError('Network Error', code)
  return err
}

describe('normalizeError', () => {
  it('reserves the offline message for a genuine transport failure (no response at all)', () => {
    const result = normalizeError(axiosErrorWithNoResponse())
    expect(result.isNetworkError).toBe(true)
    expect(result.message).toBe("Can't reach the server. Check your connection and try again.")
  })

  it('gives a timeout its own distinct message rather than the generic offline one', () => {
    const result = normalizeError(axiosErrorWithNoResponse('ECONNABORTED'))
    expect(result.message).toBe('The request timed out. Please try again.')
  })

  it('never reports a 403 as "can\'t reach the server"', () => {
    const result = normalizeError(axiosErrorWithResponse(403, {}))
    expect(result.status).toBe(403)
    expect(result.isNetworkError).toBe(false)
    expect(result.message).not.toContain("can't reach the server")
    expect(result.message).toBe("You don't have permission to do that.")
  })

  it('never reports a 500 as "can\'t reach the server", and distinguishes it from a 403', () => {
    const result = normalizeError(axiosErrorWithResponse(500, {}))
    expect(result.isNetworkError).toBe(false)
    expect(result.message).toBe('Something went wrong on our end. Please try again in a moment.')
    expect(result.message).not.toBe(normalizeError(axiosErrorWithResponse(403, {})).message)
  })

  it('surfaces a validation failure with its own message, distinct from a server error', () => {
    const result = normalizeError(axiosErrorWithResponse(422, {}))
    expect(result.message).toBe("Some of the information provided isn't valid. Please check it and try again.")
  })

  it('prefers the backend-supplied message over the generic fallback when one is present', () => {
    const result = normalizeError(axiosErrorWithResponse(403, { message: 'Tenant license is suspended. Contact Ecosys to restore access.' }))
    expect(result.message).toBe('Tenant license is suspended. Contact Ecosys to restore access.')
  })

  it('reads the errorCode extension so license failures can be classified upstream', () => {
    const result = normalizeError(axiosErrorWithResponse(403, { message: 'Suspended', errorCode: 'license_suspended' }))
    expect(result.errorCode).toBe('license_suspended')
  })

  it('never shows a proxy HTML error page as the message (nginx 413)', () => {
    const html = ['<html>', '<head><title>413 Request Entity Too Large</title></head>', '<body></body></html>'].join('\n')
    const result = normalizeError(axiosErrorWithResponse(413, html))
    expect(result.status).toBe(413)
    expect(result.message).not.toContain('<')
    expect(result.message).toMatch(/too large/i)
  })

  it('still shows a plain-text error body from the API', () => {
    expect(normalizeError(axiosErrorWithResponse(400, 'Asset code must be unique per tenant.')).message).toBe('Asset code must be unique per tenant.')
  })
})

describe('shouldRetryQuery', () => {
  it('retries failures that can clear up on their own, up to three times', () => {
    for (const error of [axiosErrorWithNoResponse(), axiosErrorWithNoResponse('ECONNABORTED'), axiosErrorWithResponse(500, {}), axiosErrorWithResponse(503, {}), axiosErrorWithResponse(429, {}), axiosErrorWithResponse(408, {})]) {
      expect(shouldRetryQuery(0, error)).toBe(true)
      expect(shouldRetryQuery(3, error)).toBe(false)
    }
  })

  it('does not retry an answer that will not change', () => {
    for (const status of [400, 401, 403, 404, 409, 422]) {
      expect(shouldRetryQuery(0, axiosErrorWithResponse(status, { message: 'No' }))).toBe(false)
    }
  })
})
