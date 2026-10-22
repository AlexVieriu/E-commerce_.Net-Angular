import { defineConfig, devices } from '@playwright/test';

// Why it is set up this way: mindset/spikes/sprint-b.md, T37.
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
    // 'on-first-retry' would never fire: retries are 0 outside CI, and this suite is local-only.
    trace: 'retain-on-failure'
  },

  // Playwright starts these servers before the tests and stops them after.
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

  // A project = test files sharing settings (here: which login they start with).
  // `dependencies` run first; `storageState` loads a saved login (cookies) instead of logging in.
  projects: [
    // These two only log in and save the session to a file.
    { name: 'setup-customer', testMatch: /setup\/customer\.setup\.ts/ },
    { name: 'setup-admin', testMatch: /setup\/admin\.setup\.ts/ },

    {
      name: 'checkout',
      testMatch: /checkout\.spec\.ts/,
      dependencies: ['setup-customer'], // log in as customer first
      use: { ...devices['Desktop Chrome'], storageState: 'playwright/.auth/customer.json' }
    },
    {
      // Depends on `checkout`, so the order it looks for already exists.
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
