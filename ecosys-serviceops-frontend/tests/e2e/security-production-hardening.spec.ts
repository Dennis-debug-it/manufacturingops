import { expect, test } from '@playwright/test'
import { json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'

const API = 'http://localhost:5072'

test.describe('security and production hardening', () => {
  test('tenant admin is blocked from platform pages', async ({ page }) => {
    await registerApiCatchAll(page)
    await loginWithMockSession(page, {
      role: 'tenantAdmin',
      email: 'admin@acme.test',
      fullName: 'Alice Admin',
      tenantId: 'tenant-1',
      tenantName: 'Acme Facilities',
    })

    await page.goto('/platform/tenants')
    await page.waitForURL('**/dashboard')
  })

  test('technician is blocked from admin-only pages', async ({ page }) => {
    await registerApiCatchAll(page)
    await loginWithMockSession(page, {
      role: 'technician',
      email: 'tech@acme.test',
      fullName: 'Moses Otieno',
      tenantId: 'tenant-1',
      tenantName: 'Acme Facilities',
    })

    await page.goto('/settings')
    await page.waitForURL('**/tech')
  })

  test('technician does not receive admin-only knowledge articles', async ({ page }) => {
    await registerApiCatchAll(page)

    await page.route(`${API}/api/knowledge/categories`, async (route) => {
      await route.fulfill(json([{ id: 'cat-1', name: 'Operations', description: 'Operational guides', displayOrder: 1, isActive: true }]))
    })

    await page.route(`${API}/api/knowledge/articles**`, async (route) => {
      await route.fulfill(json([
        {
          id: 'article-2',
          title: 'Technician field guide',
          slug: 'technician-field-guide',
          summary: 'Technician-safe guide.',
          categoryId: 'cat-1',
          categoryName: 'Operations',
          status: 'Published',
          visibility: 'TechnicianOnly',
          publishedAt: '2026-05-16T10:00:00.000Z',
          updatedAt: '2026-05-16T10:00:00.000Z',
          tags: ['Technician'],
        },
      ]))
    })

    await loginWithMockSession(page, {
      role: 'technician',
      email: 'tech@acme.test',
      fullName: 'Moses Otieno',
      tenantId: 'tenant-1',
      tenantName: 'Acme Facilities',
    })

    await page.goto('/knowledge')
    await page.waitForURL('**/tech')
    await expect(page.getByText('Admin escalation guide')).toHaveCount(0)
  })

  test('tenant admin can receive admin-only knowledge articles', async ({ page }) => {
    await registerApiCatchAll(page)

    await page.route(`${API}/api/knowledge/categories`, async (route) => {
      await route.fulfill(json([{ id: 'cat-1', name: 'Operations', description: 'Operational guides', displayOrder: 1, isActive: true }]))
    })

    await page.route(`${API}/api/knowledge/articles**`, async (route) => {
      await route.fulfill(json([
        {
          id: 'article-1',
          title: 'Admin escalation guide',
          slug: 'admin-escalation-guide',
          summary: 'Admin-only guide.',
          categoryId: 'cat-1',
          categoryName: 'Operations',
          status: 'Published',
          visibility: 'AdminOnly',
          publishedAt: '2026-05-16T10:00:00.000Z',
          updatedAt: '2026-05-16T10:00:00.000Z',
          tags: ['Admin'],
        },
        {
          id: 'article-2',
          title: 'Technician field guide',
          slug: 'technician-field-guide',
          summary: 'Technician-safe guide.',
          categoryId: 'cat-1',
          categoryName: 'Operations',
          status: 'Published',
          visibility: 'TechnicianOnly',
          publishedAt: '2026-05-16T10:00:00.000Z',
          updatedAt: '2026-05-16T10:00:00.000Z',
          tags: ['Technician'],
        },
      ]))
    })

    await loginWithMockSession(page, {
      role: 'tenantAdmin',
      email: 'admin@acme.test',
      fullName: 'Alice Admin',
      tenantId: 'tenant-1',
      tenantName: 'Acme Facilities',
    })

    await page.goto('/knowledge')
    await expect(page.getByText('Technician field guide')).toBeVisible()
    await expect(page.getByText('Admin escalation guide')).toBeVisible()
  })
})
