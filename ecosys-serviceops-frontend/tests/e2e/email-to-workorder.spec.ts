import { expect, test } from '@playwright/test'
import { primeTenantSession } from './helpers/auth'
import { gotoEmailIntake } from './helpers/navigation'
import { mockEmailIntakeSimulationSuccess } from './helpers/api-mocks'
import { SAMPLE_EMAIL_INTAKE } from './helpers/test-data'

test.describe('Email to work-order simulation', () => {
  test('simulates inbound email parsing and shows staged preview output', async ({ page }) => {
    await primeTenantSession(page)
    await gotoEmailIntake(page)
    await mockEmailIntakeSimulationSuccess(page)

    await page.getByTestId('email-intake-rule-name-input').fill(`Email WO ${Date.now()}`)
    await page.getByTestId('email-intake-save-button').click()
    await expect(page.getByText(/protocol saved/i)).toBeVisible()

    await page.getByTestId('email-intake-simulate-button').click()
    const simulationPanel = page.getByTestId('email-intake-simulation-panel')
    await expect(simulationPanel).toBeVisible()

    await page.getByRole('button', { name: /run simulation/i }).click()

    await expect(page.getByText('1. Rule result')).toBeVisible()
    await expect(page.getByText('2. Parsed fields')).toBeVisible()
    await expect(page.getByText('3. Matching result')).toBeVisible()
    await expect(page.getByText('8. Audit preview')).toBeVisible()

    await expect(simulationPanel.getByText(SAMPLE_EMAIL_INTAKE.subject).first()).toBeVisible()
    await expect(simulationPanel.getByText('customer@example.com').first()).toBeVisible()
    await expect(page.getByText(/protocol test passed|simulation preview ready/i).first()).toBeVisible()

    await expect(page.getByTestId('email-intake-overview-panel')).toHaveCount(0)
    await expect(page.getByTestId('email-intake-mailbox-panel')).toHaveCount(0)
  })
})
