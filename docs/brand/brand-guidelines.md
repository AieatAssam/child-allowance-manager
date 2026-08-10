# Allowance Manager brand guidelines

This is the human-readable mirror of `ChildAllowanceManager/wwwroot/tokens.css`. If they ever disagree, `tokens.css` wins and this document is wrong.

## Identity

Allowance Manager is the calm family ledger. It is warm, not childish; practical, not corporate; encouraging, not sugary; and clear, not clever.

## Palette

Use the semantic token, not a raw colour. The light palette is the default; the dark palette is its accessible counterpart.

| Mode | Token | Value | Role |
| --- | --- | --- | --- |
| Light | `--al-paper` | `#FCFAF5` | Page background |
| Light | `--al-surface` | `#FFFFFF` | Cards, dialogs and drawers |
| Light | `--al-surface-sunken` | `#F5F2EA` | Table headers, toolbars and inset panels |
| Light | `--al-ink` | `#20242D` | Primary text |
| Light | `--al-ink-muted` | `#5C5F6B` | Secondary text |
| Light | `--al-primary` | `#675184` | Primary actions and links |
| Light | `--al-primary-strong` | `#54406E` | Hover and active states |
| Light | `--al-positive` | `#32735F` | Positive state and balance growth |
| Light | `--al-negative` | `#B95E4D` | Negative fills, borders and icons |
| Light | `--al-negative-text` | `#A34F40` | Normal-size negative text |
| Light | `--al-accent` | `#E9A36A` | Warm fills and rules |
| Light | `--al-border` | `#E4E0D8` | Flat surface boundaries |
| Light | `--al-focus` | `#54406E` | Keyboard focus ring |
| Dark | `--al-paper` | `#1B1A20` | Page background |
| Dark | `--al-surface` | `#24232B` | Cards, dialogs and drawers |
| Dark | `--al-surface-sunken` | `#2C2A34` | Table headers, toolbars and inset panels |
| Dark | `--al-ink` | `#F2EFE9` | Primary text |
| Dark | `--al-ink-muted` | `#A8A5B2` | Secondary text |
| Dark | `--al-primary` | `#B7A3D0` | Primary actions and links |
| Dark | `--al-primary-strong` | `#CDBCE2` | Hover and active states |
| Dark | `--al-positive` | `#6FBFA2` | Positive state and balance growth |
| Dark | `--al-negative` | `#E08D7B` | Negative fills, borders and text |
| Dark | `--al-negative-text` | `#E08D7B` | Normal-size negative text |
| Dark | `--al-accent` | `#E9A36A` | Warm fills and rules |
| Dark | `--al-border` | `#3A3844` | Flat surface boundaries |
| Dark | `--al-focus` | `#CDBCE2` | Keyboard focus ring |

Rules:

- `--al-negative` is below AA contrast for normal-size text on light paper. Use it only for fills, borders, icons, or text at 24px or larger (18.66px bold or larger). Use `--al-negative-text` for other normal-size text.
- `--al-accent` fails AA against light paper at every size. It is a fill and rule colour only; never use it as text on a light surface.

## Typography

Use Fraunces at weight 600 for page titles and headline balances only. Use DM Sans everywhere else. Hierarchy comes from size, weight and space, not from a third typeface.

| Token | Value |
| --- | --- |
| `--al-font-ui` | DM Sans, then system UI fallbacks |
| `--al-font-display` | Fraunces, then Georgia and serif fallbacks |
| `--al-text-xs` | 0.75rem |
| `--al-text-sm` | 0.875rem |
| `--al-text-base` | 1rem |
| `--al-text-lg` | 1.125rem |
| `--al-text-xl` | 1.375rem |
| `--al-text-2xl` | 1.75rem |
| `--al-text-3xl` | 2.25rem |
| `--al-weight-regular` | 400 |
| `--al-weight-medium` | 500 |
| `--al-weight-semibold` | 600 |
| `--al-weight-bold` | 700 |
| `--al-leading-tight` | 1.2 |
| `--al-leading` | 1.5 |

## Spacing and shape

Spacing uses a 4px base and an 8px rhythm: `--al-space-1` is 4px, then `--al-space-2` through `--al-space-8` are 8px, 12px, 16px, 24px, 32px, 48px and 64px.

The radius set is limited to `--al-radius-sm` at 8px, `--al-radius` at 10px and `--al-radius-lg` at 12px. Nothing is rounder.

Elevation has two levels. `--al-shadow-none` is used for cards, panels and the app bar, which remain flat with a 1px border. `--al-shadow-raised` is used only for dialogs, menus and popovers.

## Voice

The voice is calm, clear, encouraging and trustworthy. Say what happened and what happens next. Use no metaphors, kicker labels or exclamation marks. Use sentence case for everything except proper nouns. Never say “tenant” in family-facing UI; say “family”.

## Copy patterns

P10-T3 is the owner of the exact UI copy replacements. Keep this document and that task's `copy_map` aligned: each replacement is copied verbatim into the implementation, with no alternate wording introduced by components.

| Pattern | Rule |
| --- | --- |
| Labels and actions | Sentence case; name what the person controls. |
| Status and errors | Say what happened and what happens next. |
| Family-facing naming | Say “family”, never “tenant”. |
| Destructive actions | Explain the result and the available restore path. |

## Visual language

Use ledger rule lines instead of animated shapes. Keep the background quiet so balances dominate. Use one outlined icon set at one stroke weight. Shadows belong only on dialogs, menus and popovers.

## Assets

The mark, favicon and social preview live under `ChildAllowanceManager/wwwroot/brand/`. Self-hosted font files live under `ChildAllowanceManager/wwwroot/fonts/`. No new colour may be introduced outside `ChildAllowanceManager/wwwroot/tokens.css`.

No images are required in this document; describe assets rather than embedding them.
