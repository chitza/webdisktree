# WebDiskTree — Design System

## Product context

WebDiskTree is a **self-hosted disk-usage analyzer** (Angular 22 SPA + .NET backend). An operator points it at an allowed filesystem root, it scans, and then the user explores *where the space went* — drilling down a directory tree while a live visualization redraws at each level. Think WinDirStat / DaisyDisk / `ncdu`, on the web.

**Jobs to be done**
1. *"What is eating my disk?"* — see the biggest offenders at a glance, at any depth.
2. *"Drill into it."* — descend a directory, keep bearings via breadcrumb, go back up.
3. *"Delete it."* — multi-select files and remove them, then see the view marked stale.
4. *"Keep it current."* — schedule recurring scans, watch a running scan's live progress.

**Audience:** technically literate operators and homelab/server admins. They read dense tables and expect precise numbers, not rounded marketing figures.

### Key screens
| Screen | Role |
|---|---|
| `/scans/:id` **Scan detail** | The product. Two-pane: file list left, visualization right. Everything else is supporting. |
| `/scans` Scan history | Dense table of past scans with status. |
| `/scans/new` Start a scan | One small form card. |
| `/schedules` Schedules | Form card + table of cron schedules. |

---

## The design problem being solved

The current UI is the **unmodified Angular CLI Material scaffold** — `mat.theme()` with stock `$azure-palette` / `$blue-palette`, Roboto, density 0, scaffold comments still in place. It reads as "a default Angular app": generic blue chrome, no identity, and the only real color in the product (the treemap) comes from d3's generic `schemeTableau10`, unrelated to anything else on screen.

**Goal:** an original, deliberately-colored identity that works in light *and* dark, where the visualization palette and the app chrome are visibly one system.

---

## HARD CONSTRAINTS — identical across every direction

These are **not** the variable being explored. Every draft must honour all of them.

### Structure (do not redesign)
- **Two-pane split on scan detail**, horizontal, ~35% file list / ~65% visualization, with a draggable gutter between them. Not tabs, not stacked, not a modal.
- **File list columns stay**: selection checkbox (40px) · Name (absorbs remaining width, ellipsis-truncated, leading folder/file icon, trailing chevron on directories) · Size (100px, right-ish) · Modified (150px). Sticky header, internal scroll, paginator pinned below, "Up" button in a toolbar above.
- **Four-way view switcher** above the viz: Treemap · Stretched treemap · Sunburst · File types. All four labels stay.
- **Breadcrumb** as a horizontal row of path segments with chevron separators, directly under the page header.
- **Page header** on scan detail: root path as the title (long, must wrap/break) + a metadata subtitle line (`size · N files · N folders`) + a "Back to history" action.
- **App shell**: top bar with the "WebDiskTree" wordmark, three nav links (Scans / New Scan / Schedules), and a theme toggle at the far right.

### Substrate
- **Angular Material 22 (M3) stays.** Tables, menus, dialogs, segmented controls, checkboxes, slide toggles, form fields and icons remain Material components — they will be re-tokenized, not replaced. The app shell, page headers and status pills *will* be hand-built. Do not design controls that Material cannot be themed into (no exotic custom scrollbars, no bespoke dropdown animations).
- **Material Icons ligatures** are the icon set. Existing names in use: `folder`, `description`, `chevron_right`, `arrow_upward`, `arrow_back`, `delete`, `cancel`, `error`, `warning`, `play_arrow`, `light_mode`, `dark_mode`, `brightness_auto`. Reproduce icons 1:1; do not substitute another icon family.
- **No logo exists.** The identity is the text wordmark "WebDiskTree". Do not invent a logo mark, monogram, or emoji stand-in.

### Both themes are first-class
Every direction must specify **light and dark**. Dark is not an afterthought: the app ships a tri-state toggle (light / dark / follow-OS) and the current implementation resolves both halves from a single `light-dark()`-based token set. Specify both grounds, both text colors, and a viz palette that survives on both.

### Data-display rules
- **Numbers are precise and tabular.** Sizes format as `1.4 GB` / `912 KB` / `0 B`; counts group as `12.345`. Use tabular/lining figures so columns align.
- **Paths are the primary content** and are often long. They need a treatment that tolerates breaking and truncation.
- **Density is high on purpose** — the file table runs 32px rows with a 40px header, ~13px type. Do not loosen it into a spacious marketing layout.
- **Status has five states**: completed, running (incl. pending), failed, cancelled, plus a separate "stale" flag. Each needs a distinguishable color that is not carried by hue alone.

### Accessibility
- Body text and all status pills must clear **WCAG AA (4.5:1)** on their own ground in both themes; viz labels sitting on colored fills must clear **3:1** minimum.
- Keep visible focus states — this is a keyboard-heavy, table-driven tool.
- Never encode meaning in hue alone (status pills carry text; viz relies on size + label + tooltip).

### Non-goals
- No responsive/mobile design. The codebase has **zero** media queries and this is a desktop tool; design at desktop width.
- No new features, routes, or data. Do not add charts, filters, or panels that do not exist.

---

## THE VARIABLE UNDER EXPLORATION

**Color personality and typographic voice** — and nothing else.

Three directions are being generated side by side to compare. Each one specifies its own concrete palette (grounds, text, accent, five status colors, and a 10–12 hue visualization ramp tuned for both themes) and its own type pairing. The direction prompts carry those exact values.

The visualization palette is **part of the identity, not a separate concern**: today all four viz views share one memoized extension→color scale, and whatever replaces it must still be one shared scale that the chrome visibly belongs to.

### Motion
Restrained. Material's default control transitions, the indeterminate progress bar on a running scan, and the split-gutter drag are the whole motion budget. No scroll-triggered reveals, no decorative animation — a scan of 400k files should never feel like it is waiting on an animation.

---

## Fidelity rule for every generation

Use ONLY the fonts, colors, spacing and component styles defined by this document plus the specific direction being generated. Do not introduce fonts, colors, or visual styles beyond those. In particular: do not add gradients, glassmorphism, decorative illustration, or marketing-page furniture (hero sections, testimonials, feature grids) — this is a dense internal tool.
