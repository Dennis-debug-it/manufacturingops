import { expect, type APIRequestContext, type APIResponse } from '@playwright/test'

export function authHeaders(token: string) {
  return {
    Authorization: `Bearer ${token}`,
  }
}

export async function authenticatedApiCall(
  request: APIRequestContext,
  apiBaseUrl: string,
  token: string,
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE',
  path: string,
  data?: unknown,
) {
  const url = `${apiBaseUrl}${path.startsWith('/') ? path : `/${path}`}`
  const response = await request.fetch(url, {
    method,
    headers: {
      ...authHeaders(token),
      Accept: 'application/json',
      ...(data ? { 'Content-Type': 'application/json' } : {}),
    },
    data,
  })
  return response
}

export async function expectNoServerError(
  response: APIResponse,
  context: string,
) {
  expect(
    response.status(),
    `${context} returned ${response.status()} (${response.url()})`,
  ).toBeLessThan(500)
}

