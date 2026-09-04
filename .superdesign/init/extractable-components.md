# Extractable Components

Candidates for Superdesign `DraftComponent` extraction (Petite-Vue templates reused across drafts via `<sd-component>`).

**Context:** this app has **no brand logo asset** — the identity is the plain text wordmark "WebDiskTree" in the toolbar. There is no SVG mark, no image, no `favicon.svg`; `index.html` references only a `favicon.ico`. The Brand-Asset logo invariant therefore does not apply to any component here: nothing has a logo *position* to fill. Do not invent a mark.

---

## Layout Components

### AppShellHeader
- Source: `frontend/src/app/app.html` (+ `app.scss`, `app.ts`)
- Category: layout
- Description: Top Material toolbar with the "WebDiskTree" text wordmark, three nav links, and a right-aligned theme-cycle icon button
- Extractable props: `activeItem` (string, default: `"scans"` — one of `scans` / `new` / `schedules`), `themeIcon` (string, default: `"brightness_auto"` — one of `light_mode` / `dark_mode` / `brightness_auto`)
- Hardcoded: the "WebDiskTree" wordmark text, all three nav labels and hrefs (`/scans`, `/scans/new`, `/schedules`), Material Icons ligature names, the active-link bold+underline treatment, all CSS
- **Worth extracting** — it is the only element on every page, so it should be identical across all generated drafts.

---

## Basic Components

### ScanStatusChip
- Source: `frontend/src/app/features/scan-history/scan-history.html` (chip markup) + `scan-history.scss` (the five color rules)
- Category: basic
- Description: Small pill showing scan state, in one of five hardcoded colors
- Extractable props: `status` (string, default: `"completed"` — one of `completed` / `running` / `pending` / `failed` / `cancelled`), `stale` (boolean, default: `false` — renders an additional orange "stale" pill beside the status pill)
- Hardcoded: the five background hexes (`#2e7d32`, `#1565c0`, `#c62828`, `#616161`, `#ef6c00`), white foreground, label text
- **Worth extracting** — it is the clearest carrier of the app's status color language and one of the main things the redesign is replacing.

### ScanProgressBanner
- Source: `frontend/src/app/features/scan-detail/scan-progress-banner/scan-progress-banner.html` (+ `.scss`)
- Category: basic
- Description: Full-width inline banner in one of three variants — running (indeterminate progress bar + live file/dir/byte counts + current path), failed (error icon + message + Retry), stale (warning icon + message + Rescan)
- Extractable props: `variant` (string, default: `"running"` — one of `running` / `failed` / `stale`), `message` (string, default: `""`), `detail` (string, default: `""` — the live progress line)
- Hardcoded: the `--mat-sys-*-container` token pairs per variant, `error` / `warning` icon names, button labels, 4px radius, 13px type
- **Worth extracting** — three visually distinct states that should stay consistent across drafts.

### VizViewSwitcher
- Source: `frontend/src/app/features/scan-detail/scan-detail.html` (the `mat-button-toggle-group`)
- Category: basic
- Description: Four-way segmented control selecting the visualization — Treemap / Stretched treemap / Sunburst / File types
- Extractable props: `active` (string, default: `"treemap"` — one of `treemap` / `stretched` / `sunburst` / `types`)
- Hardcoded: all four labels, the segmented-control styling, 12px bottom margin
- **Worth extracting** — it appears in every scan-detail draft and its four labels must not drift.

---

## Deliberately NOT extracted

| Component | Why |
|---|---|
| Buttons, cards, inputs, selects, checkboxes, slide toggles | Angular Material primitives used raw — too simple to extract; better inline in drafts (per the skill's guidance to skip basic primitives) |
| `FileList` | A `mat-table` bound to live data with sort + paginator + selection; its value is the *table layout* (fixed layout, 40px select / 100px size / 150px modified columns, name column absorbing the remainder), which is better expressed inline per draft than frozen as a component |
| `Treemap` / `StretchedTreemap` / `Sunburst` | Canvas- and SVG-rendered from d3 layouts at runtime — cannot be meaningfully represented as a static Petite-Vue template. Drafts should mock these with representative colored rectangles / arcs. |
| `TypeBreakdown` | A simple 4-column CSS grid of label + bar + size + count; trivial to inline |
| Breadcrumb | Not a component — inline markup inside `scan-detail.html` |
