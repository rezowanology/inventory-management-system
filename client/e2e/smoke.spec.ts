import { expect, test } from '@playwright/test'
import { deliverUnit, login, newBarcode, receiveUnit, trackUnit } from './helpers'

/**
 * One end-to-end run of the sample flow through the REAL UI, with the three operational accounts and the admin:
 * track -> receive -> deliver -> reverse the delivery -> the unit is in stock again.
 */
test('sample flow: track -> receive -> deliver -> reverse', async ({ browser }) => {
  const unit = newBarcode('SMK')
  const part = `${unit}-P1`

  // Each person works in their own browser session (own cookies), like on the shop floor.
  const as = async (who: Parameters<typeof login>[1]) => {
    const page = await (await browser.newContext()).newPage()
    await login(page, who)
    return page
  }

  const tracker = await as('tracker')
  await trackUnit(tracker, unit, 'Refrigerator', { Compressor: part })

  const receiver = await as('receiver')
  await receiveUnit(receiver, unit)
  await expect(receiver.getByRole('status')).toContainText(`Unit ${unit} received`)

  const deliverer = await as('deliverer')
  await deliverUnit(deliverer, unit)
  await expect(deliverer.getByRole('status')).toContainText(`Unit ${unit} delivered`)

  // A Report Viewer sees the 3 rows, and has no Reverse button.
  const viewer = await as('viewer')
  await viewer.goto(`/history?q=${unit}`)
  await expect(viewer.getByRole('row')).toHaveCount(4) // header + 3 rows
  await expect(viewer.getByRole('link', { name: 'Reverse' })).toHaveCount(0)

  // The tracker may not open the history: the Forbidden page.
  await tracker.goto('/history')
  await expect(tracker.getByRole('heading')).toContainText(/forbidden|not allowed/i)

  // The admin reverses the delivery.
  const admin = await as('admin')
  await admin.goto(`/history?q=${unit}`)
  await admin.getByRole('row').filter({ hasText: 'DELIVERED' }).getByRole('link', { name: 'Reverse' }).click()
  await expect(admin.getByText('will return to stock at Refrigerator Receive Point 1')).toBeVisible()
  await admin.locator('#reverse-reason').fill('x')
  await admin.getByRole('button', { name: 'Reverse this row' }).click()
  await expect(admin.getByText('Give a reason of at least 5 characters')).toBeVisible() // short reason is blocked
  await admin.locator('#reverse-reason').fill('Smoke test: delivered by mistake')
  await admin.getByRole('button', { name: 'Reverse this row' }).click()
  await expect(admin.getByText('Reversed.')).toBeVisible()

  // The barcode history now shows 4 rows and the unit is In stock again.
  await admin.goto(`/reports/barcode?barcode=${unit}`)
  await expect(admin.getByTestId('barcode-header')).toContainText('In stock')
  await expect(admin.getByText('All 4 rows')).toBeVisible()
  await expect(admin.getByText('Smoke test: delivered by mistake')).toBeVisible()
})
