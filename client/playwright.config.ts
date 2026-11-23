import { defineConfig } from '@playwright/test'

// End-to-end tests run against DEMO MODE: the API serves the built React app on http://localhost:5080
// (start it first: npm run build, then dotnet run --project src/Ims.Api --launch-profile http).
// They use the Microsoft Edge that is already installed (channel "msedge"), so no browser download is needed.
export default defineConfig({
  testDir: './e2e',
  testMatch: /.*\.spec\.ts/,
  timeout: 120_000,
  workers: 1,            // the tests share one database and take turns
  reporter: [['list']],
  outputDir: './test-results',
  use: {
    baseURL: 'http://localhost:5080',
    channel: 'msedge',
    viewport: { width: 1280, height: 900 },
    trace: 'off',
  },
})
