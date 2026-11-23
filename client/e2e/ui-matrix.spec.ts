import { expect, test } from '@playwright/test'
import { login, type Account } from './helpers'

// The permission matrix as the USER sees it: for every role and every route, the page opens or the Forbidden page shows.
// (The same matrix is checked on the API side by PolicyMatrixTests.) Source of truth: CLAUDE.md section 8.
const ROUTES: { capability: string; path: string }[] = [
  { capability: 'dashboard', path: '/' },
  { capability: 'change own password', path: '/change-password' },
  { capability: 'ops.track', path: '/ops/track' },
  { capability: 'ops.receive', path: '/ops/receive' },
  { capability: 'ops.deliver', path: '/ops/deliver' },
  { capability: 'history.view', path: '/history' },
  { capability: 'history.reverse', path: '/history/999999999/reverse' },
  { capability: 'reports.view', path: '/reports/stock' },
  { capability: 'reports.view', path: '/reports/summary' },
  { capability: 'reports.view', path: '/reports/barcode' },
  { capability: 'masterdata.manage', path: '/manage/products' },
  { capability: 'masterdata.manage', path: '/manage/components' },
  { capability: 'masterdata.manage', path: '/manage/points' },
  { capability: 'users.manage', path: '/manage/users' },
  { capability: 'audit.view', path: '/audit' },
]

const ALLOWED: Record<Account, string[]> = {
  admin: ROUTES.map((r) => r.capability),
  tracker: ['dashboard', 'change own password', 'ops.track'],
  receiver: ['dashboard', 'change own password', 'ops.receive'],
  deliverer: ['dashboard', 'change own password', 'ops.deliver'],
  viewer: ['dashboard', 'change own password', 'history.view', 'reports.view'],
}

for (const role of Object.keys(ALLOWED) as Account[]) {
  test(`ui matrix: ${role}`, async ({ page }) => {
    await login(page, role)
    for (const route of ROUTES) {
      await page.goto(route.path)
      await page.waitForLoadState('networkidle')
      const forbidden = page.getByRole('heading', { name: 'Forbidden' })
      if (ALLOWED[role].includes(route.capability)) {
        await expect(forbidden, `${role} should open ${route.path}`).toHaveCount(0)
      } else {
        await expect(forbidden, `${role} should get Forbidden on ${route.path}`).toBeVisible()
      }
    }
  })
}
