# Design — Allowance Manager

A locked design system for the family finance app. Page redesigns use this
system for consistency; visual variety comes from the content and the density
of each surface, not from swapping palettes per route.

## Genre

playful, calm and exact. The family-facing voice stays practical, warm and
clear rather than childish or sugary.

## Macrostructure family

- Marketing pages: Workbench — product-led, task-first, no invented proof.
- App pages: Workbench — asymmetric ledger workspace; balances lead, history
  supports, settings use dense sheets.
- Content pages: Long Document — continuous reading flow with quiet rules.

## Theme

The existing warm-paper ledger palette is preserved and expressed in OKLCH.

- `--color-paper`   `oklch(98.5% 0.007 88.6)`
- `--color-paper-2` `oklch(96.1% 0.011 89.7)`
- `--color-ink`     `oklch(26.0% 0.018 266.3)`
- `--color-ink-2`   `oklch(48.7% 0.020 274.5)`
- `--color-rule`    `oklch(90.8% 0.012 84.6)`
- `--color-accent`  `oklch(77.1% 0.111 59.3)`
- `--color-focus`   `oklch(41.3% 0.078 303.2)`

The app keeps its existing `--al-*` semantic aliases so MudBlazor and custom
tables continue to share one source of truth. Dark mode is the paired palette
in `tokens.css`.

## Typography

- Display: Fraunces, weight 600, roman only.
- Body: DM Sans, weight 400.
- Mono: system monospace, reserved for data labels when needed.
- Display tracking: `-0.025em`.
- Type scale anchor: balance values use `clamp(2.25rem, 4vw, 3.5rem)`.

## Spacing

4-point named scale. Existing `--al-space-1` through `--al-space-8` remain the
implementation tokens. New rules use those names instead of one-off spacing.

## Motion

- Easings: `--al-ease-out`, `--al-ease-in`, `--al-ease-in-out`.
- Reveal pattern: the children's dashboard cards settle in once on load
  (transform only, so a stalled animation never hides a balance). Other pages
  have no load reveal.
- Money motion, dashboard only: coins drop into a card when its balance rises
  and lift out when it falls; the child's buddy hops or shakes with them. A
  birthday card scatters confetti once per load. All of it is decoration added
  by `balance-motion.js` and removed after two seconds; the text is the record.
- Reduced-motion fallback: opacity-only, no more than 150ms.

## Microinteractions stance

Silent success, visible focus, no celebratory toasts. Hover may shift colour or
lift a card by 2px on fine pointers. Loading keeps the user's context visible.
All touch targets remain at least 44px.

## CTA voice

- Primary: filled, compact, sentence-case verb that names the money or record
  operation.
- Secondary: neutral text or outlined action.
- Destructive: error colour, explicit result, restore path where available.

## Per-page allowances

- App pages may use existing MudBlazor components and tables.
- App pages do not use decorative hero enrichment, gradients, blobs or fake
  device/browser chrome.
- The children's dashboard is the one illustrated surface. It uses the
  generated cut-paper set in `wwwroot/art/`: one buddy per child colour
  (`buddy-0` to `buddy-4`, matching `--al-chart-1` to `-5`), a coin, a jar for
  the empty chart, and a paper grain whose strength is `--al-grain-opacity`.
  New art for it must match that set: flat layered paper, no text, transparent
  background.
- Shared display pages keep the same dashboard layout and simply enlarge the
  data surfaces for distance viewing.

## What pages must share

The wordmark, warm-paper palette, Fraunces + DM Sans pairing, 4-point spacing,
flat bordered surfaces, outlined Material icon set, action vocabulary, and
accessible focus/empty/loading/error states.

## What pages may differ on

Dashboard pages may lead with balances or history. Settings pages may become
denser and more table-led. Dialogs may use a narrower measure than full-page
surfaces.

## Exports

### tokens.css

The source of truth is `ChildAllowanceManager/wwwroot/tokens.css`.

### Tailwind v4 `@theme`

```css
@theme {
  --color-paper: oklch(98.5% 0.007 88.6);
  --color-ink: oklch(26.0% 0.018 266.3);
  --color-accent: oklch(77.1% 0.111 59.3);
  --font-display: "Fraunces", Georgia, serif;
  --font-body: "DM Sans", system-ui, sans-serif;
  --spacing-md: 1.5rem;
  --text-md: 1.125rem;
  --ease-out: cubic-bezier(0.16, 1, 0.3, 1);
}
```

### DTCG `tokens.json`

```json
{
  "color": {
    "paper": { "$value": "oklch(98.5% 0.007 88.6)", "$type": "color" },
    "ink": { "$value": "oklch(26.0% 0.018 266.3)", "$type": "color" },
    "accent": { "$value": "oklch(77.1% 0.111 59.3)", "$type": "color" }
  },
  "font": {
    "display": { "$value": "Fraunces, Georgia, serif", "$type": "fontFamily" },
    "body": { "$value": "DM Sans, system-ui, sans-serif", "$type": "fontFamily" }
  },
  "space": {
    "md": { "$value": "1.5rem", "$type": "dimension" }
  }
}
```

### shadcn/ui CSS variables

```css
:root {
  --background: 98.5% 0.007 88.6;
  --foreground: 26.0% 0.018 266.3;
  --primary: 47.9% 0.084 303.2;
  --primary-foreground: 99.8% 0.003 89.9;
  --muted: 90.8% 0.012 84.6;
  --muted-foreground: 48.7% 0.020 274.5;
  --border: 90.8% 0.012 84.6;
  --input: 90.8% 0.012 84.6;
  --ring: 41.3% 0.078 303.2;
  --radius: 10px;
}
```
