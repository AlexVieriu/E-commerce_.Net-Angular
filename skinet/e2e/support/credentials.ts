// The suite's own account, not seed data: customer.setup.ts registers it once, then logs in.
export const CUSTOMER_EMAIL = 'e2e.customer@skinet-test.local';
export const CUSTOMER_PASSWORD = 'Pa$$w0rd12!@';

// Two accounts for the cart-ownership spec (BUG-18) — it needs a second person.
// Kept apart from CUSTOMER_EMAIL: both touch a cart, and the suite runs fullyParallel.
export const CART_A_EMAIL = 'e2e.cart.a@skinet-test.local';
export const CART_B_EMAIL = 'e2e.cart.b@skinet-test.local';
export const CART_PASSWORD = 'Pa$$w0rd12!@';

// The app resets this admin's password from config on startup (StoreContextSeed.cs),
// so ADMIN_PASSWORD must match your local user-secret AdminSettings:Password.
export const ADMIN_EMAIL = 'admin@test.com';
export const ADMIN_PASSWORD = 'Admin@12345!';

export const API_URL = 'https://localhost:7096/api/';
