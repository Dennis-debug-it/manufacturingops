import { expect, test, type Page } from '@playwright/test'
import { primeTenantSession } from './helpers/auth'
import { gotoEmailIntake } from './helpers/navigation'

test.describe('Email Intake', () => {
  test.describe.configure({ mode: 'serial' })
  let page: Page

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage()
    await primeTenantSession(page)
    await gotoEmailIntake(page)
  })

  test.afterAll(async () => {
    await page.close()
  })

  test.beforeEach(async () => {
    const subnav = page.getByTestId('email-intake-subnav')
    await subnav.getByRole('button', { name: 'Overview' }).click()
    await expect(page.getByTestId('email-intake-overview-panel')).toBeVisible()
  })

  test('email intake page loads cleanly with overview-first workflow structure', async () => {
    await expect(page.getByTestId('email-intake-page')).toBeVisible()
    await expect(page.getByTestId('email-intake-header')).toBeVisible()
    await expect(page.getByTestId('email-intake-status-badge')).toBeVisible()
    await expect(page.getByTestId('email-intake-summary-cards')).toBeVisible()
    await expect(page.getByTestId('email-intake-subnav')).toBeVisible()
    await expect(page.getByTestId('email-intake-overview-panel')).toBeVisible()
    await expect(page.getByTestId('email-intake-workflow-summary')).toBeVisible()

    await expect(page.getByText('Connect Mailbox')).toBeVisible()
    await expect(page.getByText('Define Intake Rules')).toBeVisible()
    await expect(page.getByText('Parse Incoming Email')).toBeVisible()
    await expect(page.getByText('Match Client / Site / Asset')).toBeVisible()
    await expect(page.getByText('Decide Action')).toBeVisible()
    await expect(page.getByText('Create or Update Work Order')).toBeVisible()
    await expect(page.getByText('Route to Dispatch / Assignment Group')).toBeVisible()
    await expect(page.getByText('Notify Team')).toBeVisible()
    await expect(page.getByText('Track Audit & Timeline')).toBeVisible()

    await expect(page.getByTestId('email-intake-mailbox-panel')).toHaveCount(0)
    await expect(page.getByTestId('email-intake-rules-panel')).toHaveCount(0)
    await expect(page.getByTestId('email-intake-parser-panel')).toHaveCount(0)
    await expect(page.getByTestId('email-intake-simulation-panel')).toHaveCount(0)

    const emailIntakePage = page.getByTestId('email-intake-page')
    await expect(emailIntakePage.getByText(/API not available yet/i)).toHaveCount(0)
    await expect(emailIntakePage.getByText(/finance|quotation|invoice|payment/i)).toHaveCount(0)
  })

  test('can navigate all intake panels from sub-navigation', async () => {
    const subnav = page.getByTestId('email-intake-subnav')

    await subnav.getByRole('button', { name: 'Mailbox Connection' }).click()
    await expect(page.getByTestId('email-intake-mailbox-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Intake Rules' }).click()
    await expect(page.getByTestId('email-intake-rules-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Parser & Keywords' }).click()
    await expect(page.getByTestId('email-intake-parser-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Matching' }).click()
    await expect(page.getByTestId('email-intake-matching-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Decisions' }).click()
    await expect(page.getByTestId('email-intake-decisions-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Work Order Creation' }).click()
    await expect(page.getByTestId('email-intake-workorder-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Routing' }).click()
    await expect(page.getByTestId('email-intake-routing-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Notifications' }).click()
    await expect(page.getByTestId('email-intake-notifications-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Manual Review' }).click()
    await expect(page.getByTestId('email-intake-manual-review-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Activity Log' }).click()
    await expect(page.getByTestId('email-intake-activity-log-panel')).toBeVisible()

    await subnav.getByRole('button', { name: 'Simulation' }).click()
    await expect(page.getByTestId('email-intake-simulation-panel')).toBeVisible()
  })

  test('simulation panel runs and shows staged output', async () => {
    await page.getByTestId('email-intake-simulate-button').click()
    await expect(page.getByTestId('email-intake-simulation-panel')).toBeVisible()

    await page.getByRole('button', { name: /run simulation/i }).click()
    await expect(page.getByText('1. Rule result')).toBeVisible()
    await expect(page.getByText('2. Parsed fields')).toBeVisible()
    await expect(page.getByText('8. Audit preview')).toBeVisible()
  })
})
