# Allowance Manager — Codebase, UX, Brand, and Style Audit

**Date:** 10 August 2026  
**Scope:** Full repository inspection, runtime startup smoke test, accessibility review, UX review, and brand/style pass.  
**Status:** Analysis only. No product source changes were made for this audit.

## Executive summary

The application has a solid small-domain foundation: tenant-scoped services, PostgreSQL persistence, scheduled allowances, validation, and a passing PostgreSQL-backed test suite.

The remaining risk is concentrated in four areas:

1. Production readiness: default configuration can crash startup, framing is open to every origin, and migrations are not future-safe.
2. Financial integrity: hold/delete workflows are not atomic and manual money actions are not idempotent.
3. Product flow: multi-tenant users have no visible tenant switcher, errors are not contained in the UI, and timezone behavior is implicit.
4. Brand and visual quality: the interface mixes several unrelated visual systems and relies on generic animated dashboard decoration, which makes it feel AI-generated rather than intentionally designed.

The last implementation verification completed before this audit was 38/38 tests passing. During this audit, a local runtime smoke test confirmed the app fails with the repository's default Azure Monitor placeholder configuration, then starts when given a valid local-only telemetry value.

## Priority 0 — fix before normal deployment

### 1. Default configuration does not boot

`appsettings.json` contains placeholder connection strings while `Program.cs` enables Azure Monitor unconditionally:

- [appsettings.json](../ChildAllowanceManager/appsettings.json#L9)
- [Program.cs](../ChildAllowanceManager/Program.cs#L33)

The app fails before serving requests when the placeholder telemetry value is used.

**Fix:** Make telemetry optional outside production, add a safe development configuration, and validate missing production configuration with an actionable message.

### 2. Authenticated pages can be framed by any origin

The application removes `X-Frame-Options` and sets `frame-ancestors` to `"'self' *"`:

- [ResponseHeaderMiddleware.cs](../ChildAllowanceManager/Middleware/ResponseHeaderMiddleware.cs#L7)
- [Program.cs](../ChildAllowanceManager/Program.cs#L198)

This creates a clickjacking risk around household financial data.

**Fix:** Default to `frame-ancestors 'self'`. If embedding is required, allow only explicit trusted origins.

## Priority 1 — important correctness and product gaps

### 3. EF migration lifecycle is incomplete

There is one manually authored migration, but its target model is empty and no model snapshot exists:

- [20260810180000_Initial.cs](../ChildAllowanceManager/Migrations/20260810180000_Initial.cs#L102)
- [Program.cs](../ChildAllowanceManager/Program.cs#L166)

Observed problems:

- Future model changes cannot be reliably detected or scaffolded.
- Existing databases created with `EnsureCreated` need a one-time compatibility baseline.
- Migrations run during application startup, so database issues prevent the web app from starting.

**Fix:** Generate a proper baseline migration and model snapshot, add a compatibility migration for existing schemas, and run migrations as a deployment step rather than as the application boot path.

### 4. Hold workflows are not atomic

The child is updated before the hold transaction is written:

- [AddHoldDialogue.razor.cs](../ChildAllowanceManager/Components/Pages/AddHoldDialogue.razor.cs#L40)

If the transaction fails, the allowance remains paused without an audit record.

Tenant deletion similarly saves the tenant, children, and user memberships in separate operations:

- [TenantService.cs](../ChildAllowanceManager/Services/TenantService.cs#L55)

**Fix:** Wrap each multi-record workflow in one database transaction.

### 5. Manual money actions are not idempotent

Deposit and withdrawal actions have no request ID, busy state, or disabled submit state:

- [AddFundsDialogue.razor.cs](../ChildAllowanceManager/Components/Pages/AddFundsDialogue.razor.cs#L43)
- [WithdrawFundsDialogue.razor.cs](../ChildAllowanceManager/Components/Pages/WithdrawFundsDialogue.razor.cs#L32)

A double-click or reconnect can create duplicate money movements.

**Fix:** Add a transaction/request ID, disable submit while saving, and make manual transaction creation idempotent in the service layer.

### 6. Service errors are not contained in the UI

Management pages and dialogs call services directly without a shared error boundary or user-facing error state:

- [AdministrationPage.razor.cs](../ChildAllowanceManager/Components/Pages/AdministrationPage.razor.cs#L26)
- [ChildManagementPage.razor.cs](../ChildAllowanceManager/Components/Pages/ChildManagementPage.razor.cs#L58)
- [AddFundsDialogue.razor.cs](../ChildAllowanceManager/Components/Pages/AddFundsDialogue.razor.cs#L43)

Validation errors, duplicate suffixes, database failures, or transient disconnects can escape into the Blazor circuit with no useful recovery path.

**Fix:** Add a shared operation wrapper, snackbar error handling, retry states, preserved form values, and an application-level error boundary.

### 7. There is no tenant switcher

The current tenant is inferred from local storage and a cookie:

- [Home.razor.cs](../ChildAllowanceManager/Components/Pages/Home.razor.cs#L38)
- [NavMenu.razor](../ChildAllowanceManager/Components/Layout/NavMenu.razor#L3)

Users with multiple households have no visible way to switch. A renamed or stale tenant leaves them on the home page with no recovery flow.

**Fix:** Add a tenant/family switcher to the account menu or navigation drawer, plus a dedicated “Choose a family” state.

### 8. Timezone behavior is implicit

The worker runs at UTC midnight:

- [Program.cs](../ChildAllowanceManager/Program.cs#L130)

The UI converts timestamps using the server's local timezone:

- [ChildrenListPage.razor](../ChildAllowanceManager/Components/Pages/ChildrenListPage.razor#L76)

There is no tenant timezone setting. The “next allowance” calculation also always starts from tomorrow, and birthday allowance logic can display a birthday amount with a tomorrow date:

- [ChildService.cs](../ChildAllowanceManager/Services/ChildService.cs#L46)

**Fix:** Store a timezone per family, calculate scheduled dates in that timezone, and display exact dates alongside relative labels.

### 9. Balance history loses opening balances for empty ranges

If a selected range has no transactions, the service returns an empty history before calculating the balance immediately before the range:

- [TransactionService.cs](../ChildAllowanceManager/Services/TransactionService.cs#L58)

This can make the chart appear empty even when the child has an existing balance.

**Fix:** Return an opening-balance point for empty ranges.

### 10. No audit identity or correction workflow for money

Transactions do not record who performed an action. There is no user-facing correction, export, restore, or immutable audit view.

For a household finance product, the history should answer: what changed, when, by whom, and why?

**Fix:** Add actor metadata, immutable transaction rules, export, and a correction workflow that creates reversing entries rather than editing history.

## Priority 2 — remaining engineering and product issues

### Data integrity

- No database foreign keys enforce child/tenant and transaction/child relationships.
- Tenant membership and roles are stored as arrays, which makes per-tenant role management and auditing difficult.
- Deleted tenant suffixes cannot be reused because the unique index includes deleted rows.
- Deletion says “This cannot be undone,” but the data is soft-deleted and there is no restore path.
- `Transfer`, `Interest`, and `Other` transaction types exist without corresponding product flows or clear business rules.
- Withdrawals are allowed to create negative balances. This may be intentional, but the product should state the rule explicitly.

### User and access management

- Parent email fields are required but not validated as email addresses: [AddParentDialogue.razor](../ChildAllowanceManager/Components/Pages/AddParentDialogue.razor#L7).
- There is no invitation or confirmation flow, so a typo silently creates inaccessible access.
- There is no visible user-management screen for reviewing or revoking access.
- Roles are global rather than tenant-scoped. Page-level tenant checks currently compensate, but the model is fragile.
- Authorization is primarily enforced in pages instead of through a central authorization service.

### Development and deployment

- The development seeder re-enables deleted demo data and rewrites transaction timestamps every startup: [DevelopmentDataSeeder.cs](../ChildAllowanceManager/Services/DevelopmentDataSeeder.cs#L30), [DevelopmentDataSeeder.cs](../ChildAllowanceManager/Services/DevelopmentDataSeeder.cs#L136).
- `AllowedHosts` is `*`; production should use an explicit host allowlist.
- The deployment workflow has no post-deploy health check or migration verification.
- `MainLayout` imports a JavaScript module into an unused local variable and never disposes it: [MainLayout.razor.cs](../ChildAllowanceManager/Components/Layout/MainLayout.razor.cs#L33).
- Several async UI paths use `CancellationToken.None`, which can allow stale work after a component is disposed.
- Component names use `Dialogue` where `Dialog` is the conventional spelling.

## Accessibility and interaction findings

- The Plotly chart is marked as `role="img"` even though it is interactive and has no accessible data-table alternative: [ChildrenListPage.razor](../ChildAllowanceManager/Components/Pages/ChildrenListPage.razor#L127).
- Chart series rely on color alone to distinguish children.
- Some decorative icons lack `aria-hidden`.
- Authenticated users without access receive the same “Sign in” message as unauthenticated users: [Routes.razor](../ChildAllowanceManager/Components/Routes.razor#L8).
- “Add” and “Take out” are ambiguous financial labels.
- There are no success confirmations after deposits, withdrawals, or holds.
- Add, withdraw, hold, and delete actions use inconsistent confirmation behavior.
- Forms validate on every field change, which can create noisy validation and unnecessary rerenders.
- Long transaction descriptions may make the mobile table difficult to scan.
- Warning and secondary colors should be checked against WCAG AA for normal-size text and button states.

## Brand and style pass

### Current brand problem

There is no brand source of truth:

- No brand guidelines.
- No design tokens.
- No asset library.
- No logo, favicon, app icon, or social preview assets.

The visual system is split across MudBlazor palette values, Plotly colors, hard-coded CSS colors, and separate transaction-dialog colors.

### Why the app feels AI-generated

The dashboard uses a familiar generated-dashboard formula:

- Animated gradient blobs.
- Floating ribbons.
- Organic orbit shapes.
- Sparks and glow.
- Large rounded cards.
- Gradient dialog headers.
- Kicker labels such as “Pocket money” and “Money trail.”
- Generic copy such as “See the money move” and “Compare balances.”

Relevant implementation:

- [app.css](../ChildAllowanceManager/wwwroot/app.css#L107)
- [app.css](../ChildAllowanceManager/wwwroot/app.css#L142)
- [ChildrenListPage.razor](../ChildAllowanceManager/Components/Pages/ChildrenListPage.razor#L25)

The decoration has no relationship to the product's actual concept and competes with the balances and actions.

### Color consistency

The app combines:

- Indigo primary.
- Orange secondary.
- Teal tertiary.
- Navy app bar.
- Pink/yellow/teal dashboard decoration.
- Purple/amber/teal/pink chart colors.
- Coral negative values.

These values appear in [ThemeConfiguration.cs](../ChildAllowanceManager/ThemeConfiguration.cs#L10), [app.css](../ChildAllowanceManager/wwwroot/app.css#L51), and [ChildrenListPage.razor.cs](../ChildAllowanceManager/Components/Pages/ChildrenListPage.razor.cs#L26).

There is no semantic color model shared across light mode, dark mode, charts, dialogs, and actions. Several hard-coded light colors remain in custom CSS, so dark mode is not fully coherent.

### Typography

DM Sans is used for body text and Nunito for headings:

- [App.razor](../ChildAllowanceManager/Components/App.razor#L9)
- [app.css](../ChildAllowanceManager/wwwroot/app.css#L16)
- [app.css](../ChildAllowanceManager/wwwroot/app.css#L62)

Issues:

- No typography tokens.
- Font choices are repeated manually.
- Nunito reinforces the generic rounded children's-app aesthetic.
- Dashboard, dialogs, and settings do not share a strong hierarchy.
- External Google Fonts add a network and privacy dependency.

### Voice and copy

The current voice shifts between playful, promotional, and administrative:

- “See the money move.”
- “Money trail.”
- “Pocket money.”
- “System access.”
- “Tenant Management.”
- “Take out.”
- “Birthday bonus day.”

Recommended voice traits:

- Calm.
- Clear.
- Encouraging.
- Trustworthy.

Prefer:

- “Balances.”
- “Activity.”
- “Family settings.”
- “Add money.”
- “Withdraw.”
- “Allowance paused for 2 days.”
- “Next allowance: £5 on Tuesday, 15 July.”

Avoid metaphorical slogans, unnecessary kicker labels, vague status copy, inconsistent title casing, and technical terms such as “tenant” in family-facing UI.

### Component and layout consistency

- The dashboard has a custom visual treatment while settings pages remain close to default MudBlazor.
- The transaction dialog has a separate dark-gradient identity.
- Admin pages display a full absolute URL as visible link text: [AdministrationPage.razor](../ChildAllowanceManager/Components/Pages/AdministrationPage.razor#L60).
- Settings use large `ma-5` expansion-panel margins, creating uneven density: [AdministrationPage.razor](../ChildAllowanceManager/Components/Pages/AdministrationPage.razor#L51).
- Save, edit, delete, cancel, and clear do not have a consistent action hierarchy.
- Secondary color is used for cancellation and adjacent actions instead of a clear neutral/destructive system.
- Dialog sizing, headers, padding, and footer behavior are not standardized.

## Recommended visual direction

Use a distinctive **calm family ledger** identity.

### Brand traits

- Warm, not childish.
- Practical, not corporate.
- Encouraging, not sugary.
- Clear, not clever.

### Palette direction

Use fewer colors:

| Role | Suggested value |
|---|---|
| Paper | `#FCFAF5` |
| Ink | `#20242D` |
| Primary plum | `#675184` |
| Positive moss | `#32735F` |
| Negative clay | `#B95E4D` |
| Warm accent | `#E9A36A` |
| Border | `#E4E0D8` |

Use color for meaning, not decoration. Remove the pink/yellow/teal animated background system.

### Typography direction

Use Fraunces 600 sparingly for page titles and high-value balances, with DM Sans for UI and body text. If external fonts are undesirable, use DM Sans consistently and distinguish hierarchy through size, weight, and spacing.

### Visual language

- Replace animated blobs with a subtle ruled-paper or ledger-line motif.
- Use 10–12px radii instead of rounding every surface heavily.
- Use shadows only for dialogs, menus, and genuinely elevated surfaces.
- Keep the dashboard background quiet so balances dominate.
- Standardize spacing on an 8px scale.
- Create shared page-heading, card, form-section, status, and dialog patterns.
- Use one consistent outlined icon style with one stroke weight.
- Add a small custom mark based on a pocket, ledger tab, or allowance envelope.

### Interaction polish

- Add a visible tenant switcher.
- Add exact dates, not only relative “Humanized” dates.
- Show a success state after every money action.
- Preserve entered values when an operation fails.
- Replace “Today at a glance” with the actual date.
- Make the chart secondary to balances and upcoming actions.

## Recommended delivery order

1. Fix startup configuration, framing policy, and migration lifecycle.
2. Add transactional workflows, idempotency, error handling, and timezone support.
3. Add tenant switching, invitations, audit history, and restore/export flows.
4. Create brand guidelines and design tokens.
5. Redesign the shared shell, dashboard, dialogs, and settings pages around the calm ledger direction.
6. Add accessibility tests for chart data, focus flow, color contrast, and mobile layouts.

## Feature opportunities

Features that fit the product after the foundations are stable:

- Tenant switcher and invitation lifecycle.
- Configurable allowance schedule and timezone.
- Transaction audit, export, correction, and restore flows.
- Savings goals or labelled allowance categories.
- Optional child-facing read-only view.

The biggest visual improvement is deletion: remove the generic animated dashboard decoration, reduce the palette, simplify the copy, and make the family-ledger concept the distinctive element.
