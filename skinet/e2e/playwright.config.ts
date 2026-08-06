import { defineConfig, devices } from '@playwright/test';

// Playwright config: where tests live, which browsers/servers to use, how tests are grouped.
// The reasoning behind these choices is in mindset/spikes.md, T37.
export default defineConfig({
  testDir: './tests',
  globalSetup: require.resolve('./global-setup'),
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',

  use: {
    baseURL: 'https://localhost:4200',
    ignoreHTTPSErrors: true, // both dev servers use self-signed local dev certs
    // Keep a trace whenever a test fails. 'on-first-retry' would never fire here:
    // retries are 0 outside CI, and this suite is local-only.
    trace: 'retain-on-failure'
  },

  // webServer: Playwright starts these itself before testing, and stops them after.
  // No more "did you forget to run the app" -- it's automatic.
  webServer: [
    {
      command: 'dotnet run --project ../src/API/API.csproj',
      url: 'https://localhost:7096/api/products',
      ignoreHTTPSErrors: true,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000
    },
    {
      command: 'npm start',
      cwd: '../client',
      url: 'https://localhost:4200',
      ignoreHTTPSErrors: true,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000
    }
  ],

  // A "project" is a group of test files that share settings (here: which login to start with).
  // `dependencies` means "run this project first" -- Playwright waits for it to finish before
  // starting this one. `storageState` is a saved login (cookies) so a test doesn't have to log
  // in itself -- it just loads an already-logged-in browser session.
  projects: [
    // These two only log in and save that login to a file -- 
    // they don't test anything themselves.
    { name: 'setup-customer', testMatch: /setup\/customer\.setup\.ts/ },
    { name: 'setup-admin', testMatch: /setup\/admin\.setup\.ts/ },

    {
      name: 'checkout',
      testMatch: /checkout\.spec\.ts/,
      dependencies: ['setup-customer'], // log in as customer first
      use: { ...devices['Desktop Chrome'], storageState: 'playwright/.auth/customer.json' }
    },
    {
      // Also depends on `checkout`: the order this test looks for is the one `checkout` just
      // placed. Running `checkout` first guarantees that order exists -- see spikes.md T37.
      name: 'customer-orders',
      testMatch: /auth-orders\.spec\.ts/,
      dependencies: ['setup-customer', 'checkout'],
      use: { ...devices['Desktop Chrome'], storageState: 'playwright/.auth/customer.json' }
    },
    {
      name: 'admin',
      testMatch: /admin-orders\.spec\.ts/,
      dependencies: ['setup-admin'], // log in as admin first
      use: { ...devices['Desktop Chrome'], storageState: 'playwright/.auth/admin.json' }
    },
    {
      // No storageState, no dependencies: this spec does its own logins and logout,
      // because the logout is the step under test (BUG-18).
      name: 'cart-ownership',
      testMatch: /cart-ownership\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
