import { expect, type Page } from '@playwright/test'

/** Seeded demo accounts (documented in the README). Demo-only passwords. */
export const ACCOUNTS = {
  admin: { user: 'admin', password: 'Admin@12345' },
  tracker: { user: 'tracker', password: 'Demo@12345' },
  receiver: { user: 'receiver', password: 'Demo@12345' },
  deliverer: { user: 'deliverer', password: 'Demo@12345' },
  viewer: { user: 'viewer', password: 'Demo@12345' },
} as const

export type Account = keyof typeof ACCOUNTS

/** A barcode that is valid (A-Z, 0-9, "-") and unique per run. */
export function newBarcode(prefix: string): string {
  return `${prefix}${Date.now().toString(36).toUpperCase()}`.slice(0, 20)
}

/** Signs in through the real login form. */
export async function login(page: Page, account: Account) {
  await page.goto('/login')
  await page.locator('#userName').fill(ACCOUNTS[account].user)
  await page.locator('#password').fill(ACCOUNTS[account].password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).not.toHaveURL(/\/login/)
}

/** Types a barcode in the scan box and presses Enter (what a USB scanner does). */
export async function scan(page: Page, barcode: string) {
  await page.locator('#scan-barcode').fill(barcode)
  await page.locator('#scan-barcode').press('Enter')
  await expect(page.getByTestId('unit-card')).toContainText(barcode)
}

/** Track page: scan a NEW unit and attach the given components (name -> barcode). Ends after the success message. */
export async function trackUnit(page: Page, unit: string, product: string, components: Record<string, string>) {
  await page.goto('/ops/track')
  await scan(page, unit)
  await page.locator('#product').selectOption({ label: product })
  await page.locator('#point').selectOption({ index: 1 })
  for (const [name, code] of Object.entries(components)) {
    await page.locator('#new-component').selectOption({ label: name })
    await page.locator('#new-code').fill(code)
    await page.locator('#new-code').press('Enter')
  }
  await page.getByRole('button', { name: 'Save components' }).click()
  await expect(page.getByRole('status')).toContainText('tracked')
}

export async function receiveUnit(page: Page, unit: string, point = 'Refrigerator Receive Point 1') {
  await page.goto('/ops/receive')
  await scan(page, unit)
  await page.locator('#point').selectOption({ label: point })
  await page.getByRole('button', { name: 'Receive', exact: true }).click()
}

export async function deliverUnit(page: Page, unit: string, point = 'Refrigerator Delivery Point 1') {
  await page.goto('/ops/deliver')
  await scan(page, unit)
  await page.locator('#point').selectOption({ label: point })
  await page.getByRole('button', { name: 'Deliver', exact: true }).click()
}
