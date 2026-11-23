import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test, type Page } from '@playwright/test'
import { deliverUnit, login, newBarcode, receiveUnit, scan, trackUnit, type Account } from './helpers'

// Saves full-page PNGs of every module to docs/user-guide/img/ with stable names (the user guide links to them).
// Demo accounts and demo data only: no real personal data.
const OUT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs/user-guide/img')

async function shot(page: Page, name: string) {
  await page.waitForLoadState('networkidle')
  await page.waitForTimeout(400) // let the chart animation finish
  await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: true })
}

test('screenshots of every module for every role', async ({ browser }) => {
  test.setTimeout(300_000)
  const unit = newBarcode('SHOT')
  const part1 = `${unit}-A`
  const part2 = `${unit}-B`

  const as = async (who: Account) => {
    const page = await (await browser.newContext()).newPage()
    await login(page, who)
    return page
  }

  // ---- anonymous ----
  const anon = await (await browser.newContext()).newPage()
  await anon.goto('/login'); await shot(anon, '01-login')
  await anon.goto('/register'); await shot(anon, '02-register')

  // ---- admin: administration ----
  const admin = await as('admin')
  await admin.goto('/'); await shot(admin, '03-dashboard-admin')
  await admin.goto('/manage/products'); await shot(admin, '04-manage-products')
  await admin.goto('/manage/components'); await shot(admin, '05-manage-components')
  await admin.goto('/manage/points'); await shot(admin, '06-manage-points')
  await admin.goto('/manage/products'); await admin.getByRole('button', { name: 'New product' }).click()
  await shot(admin, '07-product-form')
  await admin.keyboard.press('Escape')
  await admin.goto('/manage/users'); await shot(admin, '08-manage-users')
  await admin.goto('/change-password'); await shot(admin, '09-change-password')

  // ---- tracker ----
  const tracker = await as('tracker')
  await tracker.goto('/'); await shot(tracker, '10-dashboard-tracker')
  await tracker.goto('/ops/track')
  await scan(tracker, unit); await shot(tracker, '11-track-new-unit')
  await tracker.locator('#product').selectOption({ label: 'Refrigerator' })
  await tracker.locator('#point').selectOption({ index: 1 })
  await tracker.locator('#new-component').selectOption({ label: 'Compressor' })
  await tracker.locator('#new-code').fill(part1)
  await tracker.locator('#new-code').press('Enter')
  // The same component scanned twice in this form: a yellow warning, it is not added again.
  await tracker.locator('#new-code').fill(part1)
  await tracker.locator('#new-code').press('Enter')
  await expect(tracker.getByRole('alert')).toContainText(/twice|already/i)
  await shot(tracker, '12-track-duplicate-warning')
  await tracker.locator('#new-component').selectOption({ label: 'Thermostat' })
  await tracker.locator('#new-code').fill(part2)
  await tracker.locator('#new-code').press('Enter')
  await shot(tracker, '13-track-batch')
  await tracker.getByRole('button', { name: 'Save components' }).click()
  await expect(tracker.getByRole('status')).toContainText('tracked')
  await shot(tracker, '14-track-saved')
  await tracker.goto('/history'); await expect(tracker.getByRole('heading', { name: 'Forbidden' })).toBeVisible()
  await shot(tracker, '15-forbidden')

  // ---- receiver ----
  const receiver = await as('receiver')
  await receiver.goto('/'); await shot(receiver, '16-dashboard-receiver')
  await receiveUnit(receiver, unit)
  await expect(receiver.getByRole('status')).toContainText('received')
  await shot(receiver, '17-receive-saved')
  await receiver.goto('/ops/receive'); await scan(receiver, unit)
  await receiver.locator('#point').selectOption({ label: 'Refrigerator Receive Point 1' })
  await receiver.getByRole('button', { name: 'Receive', exact: true }).click()
  await expect(receiver.getByText('Duplicate scan')).toBeVisible()
  await shot(receiver, '18-receive-duplicate-warning')

  // ---- deliverer ----
  const deliverer = await as('deliverer')
  await deliverer.goto('/'); await shot(deliverer, '19-dashboard-deliverer')
  await deliverer.goto('/ops/deliver'); await scan(deliverer, unit); await shot(deliverer, '20-deliver-in-stock')
  await deliverUnit(deliverer, unit)
  await expect(deliverer.getByRole('status')).toContainText('delivered')
  await shot(deliverer, '21-deliver-saved')

  // ---- report viewer ----
  const viewer = await as('viewer')
  await viewer.goto('/'); await shot(viewer, '22-dashboard-viewer')
  await viewer.goto('/history'); await shot(viewer, '23-history-viewer')
  await viewer.goto('/reports/stock'); await shot(viewer, '24-report-stock')
  await viewer.goto('/reports/summary?from=2026-03-27&to=2026-03-30')
  await expect(viewer.locator('canvas')).toBeVisible()
  await shot(viewer, '25-report-summary')
  await viewer.goto('/reports/barcode?barcode=2440701AK2057'); await shot(viewer, '26-report-barcode-sample')
  await viewer.goto('/audit'); await expect(viewer.getByRole('heading', { name: 'Forbidden' })).toBeVisible()
  await shot(viewer, '27-forbidden-viewer')

  // ---- admin: history, reversal, audit ----
  await admin.goto(`/history?q=${unit}`); await shot(admin, '28-history-admin')
  await admin.getByRole('row').filter({ hasText: 'DELIVERED' }).getByRole('link', { name: 'Reverse' }).click()
  await expect(admin.getByText('will return to stock')).toBeVisible()
  await admin.locator('#reverse-reason').fill('Delivered to the wrong customer')
  await shot(admin, '29-reverse-confirm')
  await admin.getByRole('button', { name: 'Reverse this row' }).click()
  await expect(admin.getByText('Reversed.')).toBeVisible()
  await shot(admin, '30-reverse-done')
  await admin.goto(`/reports/barcode?barcode=${unit}`)
  await expect(admin.getByText('Delivered to the wrong customer')).toBeVisible()
  await shot(admin, '31-report-barcode-reversal')
  await admin.goto('/reports/stock'); await shot(admin, '32-report-stock-admin')
  await admin.goto('/audit?action=UPDATE'); await shot(admin, '33-audit-log')
  const show = admin.getByRole('button', { name: /show values/i }).first()
  if (await show.count()) { await show.click(); await expect(admin.getByText('Old value')).toBeVisible() }
  await shot(admin, '34-audit-values')
  await admin.goto('/'); await shot(admin, '35-dashboard-admin-after')
})
