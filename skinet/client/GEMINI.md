# Gemini Mandates - Skinet Client (Angular)

This file contains foundational mandates that take precedence over all other instructions for Gemini CLI when working within the `skinet/client/` directory.

## Frontend Architectural Mandates
- **Directory Structure:** Adhere to the following organization:
  - `core/`: Singleton services, interceptors, guards, and domain models.
  - `features/`: Feature-specific modules, components, and logic.
  - `layout/`: App-wide structural components (Header, Footer, Nav).
  - `shared/`: Reusable components, directives, and pipes.
- **Signals-First:** Prioritize Angular **Signals** for state management and change detection. Avoid legacy RXJS `BehaviorSubject` for simple state whenever Signals are applicable.
- **Standalone Components:** All new components, directives, and pipes MUST be **standalone**.

## Styling & UI
- **Tailwind CSS 4:** Use Tailwind utility classes for all styling. Maintain the configuration in `tailwind.config.js`.
- **Responsive Design:** Ensure all features are mobile-first and responsive using Tailwind's breakpoint prefixes.
- **Angular Material:** Utilize Angular Material components for complex UI elements (modals, tables, etc.) while customizing them with Tailwind.

## Services & Data Handling
- **API Communication:** All HTTP requests must go through dedicated services. Do not inject `HttpClient` directly into components for business logic.
- **SignalR:** Manage real-time notifications via a dedicated service in `core/services`.
- **Stripe Integration:** Isolate Stripe logic and element handling within specific services in the `features/` or `core/` folders.

## Testing Standards
- **Framework:** Use **Jasmine** and **Karma** for unit and integration tests.
- **Coverage:** Add unit tests for all new services and complex component logic in `*.spec.ts` files.

## Workflow & Development
- **Verification:** Before considering a task complete, verify with `npm run build` or `ng build` and ensure there are no linting or type errors.
- **Routing:** Define all routes in `app.routes.ts` and use lazy loading for feature modules.
