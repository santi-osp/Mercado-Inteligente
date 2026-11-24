import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.mjs',
  workers: 1,
  retries: 0,
  timeout: 45000,
  use: { baseURL: 'http://localhost:4200', browserName: 'chromium', channel: 'chrome', serviceWorkers: 'block' },
  reporter: [
    ['list'],
    ['json', { outputFile: '../../../release-evidence/playwright/results.json' }]
  ]
});
