import { expect, test } from '@playwright/test'
import { CORS_HEADERS, json } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'
import { makeScenario, mockOfficeWorkOrder } from './helpers/workOrderMocks'

test('an annotated phone photo is saved small enough to upload, so it reaches the report', async ({ page }) => {
  const state = makeScenario({ status: 'In Progress', workStartedAt: '2026-05-13T08:45:00.000Z' })
  await mockOfficeWorkOrder(page, state)
  let upload: { size: number; type: string | null; category: string | null } | null = null
  await page.route(`${E2E_API_URL}/api/workorders/${state.workOrder.id}/photos`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    const body = route.request().postDataBuffer() ?? Buffer.alloc(0)
    const text = body.toString('latin1')
    upload = {
      size: body.length,
      type: /Content-Type: (image\/[a-z]+)/.exec(text)?.[1] ?? null,
      category: /name="?category"?\r\n\r\n([^\r]+)/.exec(text)?.[1] ?? null,
    }
    return route.fulfill(json(state.execution))
  })

  await page.goto(`/work-orders/${state.workOrder.id}?tab=work`)
  await page.getByRole('button', { name: /^Photos/ }).first().click()

  // A 12-megapixel photo full of detail, like one from a phone camera.
  const photo = await page.evaluate(async () => {
    const canvas = document.createElement('canvas')
    canvas.width = 4032
    canvas.height = 3024
    const ctx = canvas.getContext('2d')!
    const image = ctx.createImageData(canvas.width, canvas.height)
    for (let i = 0; i < image.data.length; i += 4) {
      image.data[i] = Math.random() * 255
      image.data[i + 1] = (i / 4 / canvas.width) % 255
      image.data[i + 2] = Math.random() * 255
      image.data[i + 3] = 255
    }
    ctx.putImageData(image, 0, 0)
    const blob = await new Promise<Blob>((resolve) => canvas.toBlob((b) => resolve(b!), 'image/jpeg', 0.6))
    const bytes = new Uint8Array(await blob.arrayBuffer())
    let binary = ''
    for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000))
    return btoa(binary)
  })
  await page.locator('input[type=file]').setInputFiles({ name: 'site.jpg', mimeType: 'image/jpeg', buffer: Buffer.from(photo, 'base64') })

  await expect(page.getByText('Annotate Photo')).toBeVisible()
  await expect(page.getByText('Loading photo…')).toHaveCount(0)
  await page.getByRole('button', { name: 'Circle' }).click()
  const box = (await page.locator('canvas').boundingBox())!
  await page.mouse.move(box.x + box.width * 0.3, box.y + box.height * 0.3)
  await page.mouse.down()
  await page.mouse.move(box.x + box.width * 0.6, box.y + box.height * 0.6, { steps: 6 })
  await page.mouse.up()
  await page.getByRole('button', { name: 'Done' }).click()

  await expect(page.getByText('Photo uploaded')).toBeVisible()
  expect(upload).not.toBeNull()
  // The server rejects anything over 5 MB; a full-size PNG of this photo would be far bigger.
  expect(upload!.size).toBeLessThan(5 * 1024 * 1024)
  expect(upload!.type).toBe('image/jpeg')
  expect(upload!.category).toBe('Other')
})
