// This account belongs to the test suite, not the app's seed data.
// customer.setup.ts creates it the first time (via the API), and just logs in every time after.
export const CUSTOMER_EMAIL = 'e2e.customer@skinet-test.local';
export const CUSTOMER_PASSWORD = 'Pa$$w0rd12!@';

// Two accounts for the cart-ownership spec (BUG-18) — it needs a second person.
// Kept apart from CUSTOMER_EMAIL: both touch a cart, and the suite runs fullyParallel.
export const CART_A_EMAIL = 'e2e.cart.a@skinet-test.local';
export const CART_B_EMAIL = 'e2e.cart.b@skinet-test.local';
export const CART_PASSWORD = 'Pa$$w0rd12!@';

// The app itself creates this admin account on every startup (StoreContextSeed.cs).
// Password comes from local user-secrets (AdminSettings:Password), not from here.
export const ADMIN_EMAIL = 'admin@test.com';
export const ADMIN_PASSWORD = 'Admin@12345!';

export const API_URL = 'https://localhost:7096/api/';
