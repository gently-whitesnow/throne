---
version: alpha
name: Throne
description: Spec-driven AI engineering control plane for PRD, TECHSPEC, intents, instructions, runs, and review workflows.
# Canonical color space: OKLCH. Consumers of DESIGN.md generate runtime CSS
# from these tokens — do not transcribe to hex by hand, drift will follow.
colors:
  # Brand action / interactive
  primary: "oklch(0.5 0.2 255)"
  primary-content: "oklch(1 0 0)"
  primary-strong: "oklch(0.42 0.2 255)"
  secondary: "oklch(0.48 0.14 280)"
  secondary-content: "oklch(1 0 0)"
  accent: "oklch(0.56 0.17 168)"
  accent-content: "oklch(1 0 0)"
  accent-strong: "oklch(0.48 0.13 168)"
  neutral: "oklch(0.3 0.006 260)"
  neutral-content: "oklch(0.96 0 0)"
  # Surface scale (DaisyUI base-*)
  base-100: "oklch(1 0 0)"
  base-200: "oklch(0.97 0.005 250)"
  base-300: "oklch(0.93 0.005 250)"
  base-content: "oklch(0.22 0.005 250)"
  # Semantic surface aliases (landing / public)
  canvas: "oklch(1 0 0)"
  surface: "oklch(0.99 0.003 250)"
  neutral-soft: "oklch(0.97 0.005 250)"
  border: "oklch(0.93 0.005 250)"
  text: "oklch(0.22 0.005 250)"
  text-muted: "oklch(0.39 0.02 257)"
  text-subtle: "oklch(0.59 0.02 257)"
  # State
  info: "oklch(0.58 0.2 240)"
  info-content: "oklch(1 0 0)"
  info-soft: "oklch(0.95 0.04 240)"
  success: "oklch(0.58 0.2 155)"
  success-content: "oklch(1 0 0)"
  success-soft: "oklch(0.95 0.05 155)"
  warning: "oklch(0.68 0.2 70)"
  warning-content: "oklch(0.15 0 0)"
  warning-soft: "oklch(0.95 0.06 80)"
  error: "oklch(0.55 0.24 25)"
  error-content: "oklch(1 0 0)"
  error-soft: "oklch(0.95 0.05 25)"
dark:
  # throne-dark theme — overrides for [data-theme="throne-dark"]
  primary: "oklch(0.68 0.17 250)"
  primary-content: "oklch(0.12 0 0)"
  primary-strong: "oklch(0.55 0.18 250)"
  secondary: "oklch(0.62 0.13 280)"
  secondary-content: "oklch(1 0 0)"
  accent: "oklch(0.74 0.16 168)"
  accent-content: "oklch(0.12 0 0)"
  accent-strong: "oklch(0.6 0.14 168)"
  neutral: "oklch(0.22 0.005 250)"
  neutral-content: "oklch(0.85 0 0)"
  base-100: "oklch(0.17 0.005 260)"
  base-200: "oklch(0.2 0.005 260)"
  base-300: "oklch(0.27 0.005 260)"
  base-content: "oklch(0.88 0.005 250)"
  canvas: "oklch(0.17 0.005 260)"
  surface: "oklch(0.19 0.005 260)"
  neutral-soft: "oklch(0.2 0.005 260)"
  border: "oklch(0.27 0.005 260)"
  text: "oklch(0.88 0.005 250)"
  text-muted: "oklch(0.7 0.02 257)"
  text-subtle: "oklch(0.55 0.02 257)"
  info: "oklch(0.72 0.16 240)"
  info-content: "oklch(0.12 0 0)"
  info-soft: "oklch(0.25 0.06 240)"
  success: "oklch(0.74 0.18 155)"
  success-content: "oklch(0.12 0 0)"
  success-soft: "oklch(0.25 0.07 155)"
  warning: "oklch(0.78 0.17 70)"
  warning-content: "oklch(0.12 0 0)"
  warning-soft: "oklch(0.28 0.08 80)"
  error: "oklch(0.68 0.2 25)"
  error-content: "oklch(1 0 0)"
  error-soft: "oklch(0.28 0.07 25)"
surface-scale:
  base-100: "{colors.canvas}"
  base-200: "{colors.neutral-soft}"
  base-300: "{colors.border}"
  base-content: "{colors.text}"
themes:
  light: throne-light
  dark: throne-dark
typography:
  page-title:
    fontFamily: Mona Sans
    fontSize: 1.5rem
    fontWeight: 700
    lineHeight: 1.25
  section-title:
    fontFamily: Mona Sans
    fontSize: 1.25rem
    fontWeight: 600
    lineHeight: 1.3
  subsection:
    fontFamily: Mona Sans
    fontSize: 1.125rem
    fontWeight: 500
    lineHeight: 1.4
  body:
    fontFamily: Mona Sans
    fontSize: 0.875rem
    fontWeight: 400
    lineHeight: 1.5
  meta:
    fontFamily: Mona Sans
    fontSize: 0.75rem
    fontWeight: 400
    lineHeight: 1.5
  code:
    fontFamily: Monaspace Neon
    fontSize: 0.75rem
    fontWeight: 400
    lineHeight: 1.4
rounded:
  sm: 6px
  md: 8px
  lg: 12px
  pill: 999px
spacing:
  1: 4px
  2: 8px
  3: 12px
  4: 16px
  5: 20px
  6: 24px
  8: 32px
components:
  shell-sidebar:
    backgroundColor: "{colors.neutral-soft}"
    textColor: "{colors.text-muted}"
    rounded: "{rounded.md}"
    width: 56px
  nav-item:
    rounded: "{rounded.md}"
    size: 40px
    textColor: "{colors.text-muted}"
  nav-item-active:
    backgroundColor: "{colors.primary}/10"
    textColor: "{colors.primary}"
    rounded: "{rounded.md}"
  card:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.text}"
    rounded: "{rounded.md}"
    padding: "{spacing.4}"
  modal:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    rounded: "{rounded.lg}"
    padding: "{spacing.6}"
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.canvas}"
    rounded: "{rounded.sm}"
    padding: 8px 16px
  button-secondary:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.text}"
    rounded: "{rounded.sm}"
    padding: 8px 16px
  badge-success:
    backgroundColor: "{colors.success-soft}"
    textColor: "{colors.success}"
    rounded: "{rounded.pill}"
  badge-warning:
    backgroundColor: "{colors.warning-soft}"
    textColor: "{colors.warning}"
    rounded: "{rounded.pill}"
  badge-error:
    backgroundColor: "{colors.error-soft}"
    textColor: "{colors.error}"
    rounded: "{rounded.pill}"
---

## Overview

Throne is not a marketing site and not a playful productivity app. It is a command center for spec-driven AI engineering: calm, dense, trustworthy, and operationally clear.

The interface should feel precise and intentional. Normal state stays quiet; only actionable changes surface stronger emphasis. The system is light-first; a `throne-dark` theme is shipped and mirrors every token in the same semantic shape.

The frontmatter above is the source of truth. `apps/web` and any other consumer regenerate their runtime CSS from these tokens (see `scripts/design/sync_design_tokens.py` for the in-repo generator). Editing token values by hand in CSS files will drift the theme away from this file — don't do it; edit DESIGN.md and re-run the generator.

## Surface scale

The shell is layered on three surfaces; map them directly when a runtime depends on DaisyUI/CSS variables:

- `base-100` → `canvas`. Main content background.
- `base-200` → `neutral-soft`. Sidebar, code blocks, soft chips, hover wash.
- `base-300` → `border`. Panel borders, dividers, faint separators.
- `base-content` → `text`. Default foreground inside the shell.

Active emphasis is rendered as a 10 % primary wash (`bg-primary/10`) with `text-primary`, not as a hard-fill chip — see `nav-item-active`.

## Colors

The palette is semantic before decorative. Canonical color space is OKLCH so that lightness/chroma can be tuned without breaking hue identity across themes.

- `primary` and `primary-strong` are reserved for the main action path, active navigation, focused controls, and decision-forward UI.
- `accent` is for relations, enrichment, and supporting structure rather than primary calls to action.
- `neutral-soft`, `surface`, `canvas`, and `border` create a layered shell with very subtle separation and almost no visual noise.
- `success`, `warning`, `error`, and `info` always communicate real state, never decoration.
- Soft semantic backgrounds (`*-soft`) are preferred for badges, inline callouts, and diff/context chips.

Avoid pastel hero gradients, candy-colored surfaces, or color usage that looks illustrative rather than operational.

## Typography

Use `Mona Sans` (variable, latin + latin-ext) as the default UI family and `Monaspace Neon` for code, diffs, IDs, and markdown-like machine text. Both ship as `@fontsource(-variable)` packages in `apps/web`; downstream consumers of DESIGN.md (public landing, etc.) must use the same two families, not a generic monospace fallback.

- Body copy is compact by default: 14px equivalent inside `apps/web`. Public landings may scale body up to 16px, but every heading still references the same `Mona Sans` family at restrained weights (≤ 600).
- Meta text can drop to 12px, but only for timestamps, IDs, and secondary annotations.
- In-app headings should stay restrained. `page-title` is the maximum scale for application chrome.
- Monospace text should feel editorial and crisp, not terminal-heavy.

The UI should never depend on giant display typography. Throne communicates authority through structure and clarity, not oversized headlines.

## Layout

Use a 4px base spacing system with generous separation between sections.

- App shell remains pinned and structured.
- Lists and panes can be information-dense, but each major region needs enough air to stay scannable.
- Standard card padding is 16px.
- Section gaps are typically 24px.
- Sidebar and supporting rails should read as secondary surfaces, not as ornamental panels.

Layouts should prioritize operational scanning: titles, status, metadata, and next actions should be easy to pick out in one pass.

## Elevation & Depth

Elevation is subtle and functional.

- Cards use a light border and minimal shadow.
- Dropdowns and overlays can step up one level.
- Modals and drawers are the highest layer, but still avoid heavy blur, glossy effects, or theatrical lighting.

Depth exists to explain stacking order, not to create spectacle.

## Shapes

Corners stay professional and controlled.

- Buttons and inputs use `6px`.
- Cards and panels use `8px`.
- Modals use `12px`.
- Pills and status badges can be fully rounded.

Avoid oversized radii that make the product feel playful or consumer-social.

## Components

Component behavior should reinforce the control-plane feel.

- Navigation items are compact, precise, and visibly stateful.
- Cards are plain, readable containers with minimal ornament.
- Primary buttons are confident blue actions.
- Secondary buttons are quiet outlined controls.
- Semantic badges use soft fills plus strong text color.
- Modals are neutral surfaces with clean borders, measured spacing, and no gradient wash.
- File pickers, inspectors, and diff surfaces should look like tooling, not marketing cards.

When a component needs emphasis, prefer stronger hierarchy, tighter copy, or clearer status over decorative treatment.

## Do's and Don'ts

Do:

- Use color semantically and pair it with text or icon meaning.
- Keep the shell light, crisp, and slightly cool.
- Let spacing create calm around dense information.
- Use monospace selectively for machine-originated or reference-like content.
- Preserve a clear visual hierarchy between chrome, content, and overlays.

Don't:

- Reintroduce Miro-like gradients, glossy panels, or oversized rounded modals.
- Use flashy hover transforms or motion that feels promotional.
- Make every badge or card colorful by default.
- Treat the app like a landing page.
- Use color as the only indicator of status.
