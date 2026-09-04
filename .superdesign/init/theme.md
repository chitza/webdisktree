# Theme & Design Tokens

## Part 1 — Compact token summary

### Origin: unmodified Angular CLI scaffold

The theme is the **stock `ng new` Material 3 block**, including its scaffold comments. Nothing about it is brand-specific. This is the single biggest reason the app reads as "a default Angular app".

```scss
@include mat.theme((
  color: (
    primary: mat.$azure-palette,     // stock Material palette
    tertiary: mat.$blue-palette,     // stock Material palette
  ),
  typography: Roboto,                // stock
  density: 0,                        // stock
));
```

### Color system

`mat.theme()` emits **every** M3 system color as `light-dark(<light>, <dark>)`. `color-scheme` alone therefore decides which half resolves — there is no second dark theme block. This mechanism is important and must be preserved by any redesign.

| Mode | Selector | `color-scheme` |
|---|---|---|
| Follow OS (default) | `html` (no `data-theme`) | `light dark` |
| Forced light | `html[data-theme='light']` | `light` |
| Forced dark | `html[data-theme='dark']` | `dark` |

`ThemeService` sets/removes `data-theme` on `<html>` and persists to `localStorage['webdisktree.theme']`.

**System tokens actually referenced in component SCSS** (all `--mat-sys-*`, resolved through `light-dark()`):

| Token | Used for |
|---|---|
| `--mat-sys-surface` | body background, sticky table header background |
| `--mat-sys-on-surface` | body text; also `color-mix`ed at 6% for row hover, 10% for bar track, 40% for the dim drill chevron |
| `--mat-sys-on-surface-variant` | subtitles, secondary counts, file-list icons |
| `--mat-sys-primary` | type-breakdown bar fill, active split gutter |
| `--mat-sys-outline-variant` | idle split gutter |
| `--mat-sys-error` | error text (fallback `#b00020`) |
| `--mat-sys-primary-container` / `--mat-sys-on-primary-container` | running banner |
| `--mat-sys-error-container` / `--mat-sys-on-error-container` | failed banner |
| `--mat-sys-tertiary-container` / `--mat-sys-on-tertiary-container` | stale banner |
| `--mat-sys-body-medium` | body `font` shorthand |

### Hardcoded colors that bypass the theme (redesign targets)

These do **not** respond to light/dark and are the app's only "real" color:

| Value | Where | Purpose |
|---|---|---|
| `schemeTableau10` (d3) | `treemap/hierarchy-colors.ts` | per-extension fill in **all four** viz views |
| `#3a4a5c` | `hierarchy-colors.ts` `DIRECTORY_COLOR` | directory rectangles |
| `#8a8a8a` | `hierarchy-colors.ts` `OTHER_COLOR` | the aggregated "other" bucket |
| `#ffffff` | `canvas-hierarchy-render.ts:55` | canvas label text |
| `#ffffff` / `rgba(255,255,255,0.25)` | `sunburst.scss` | arc stroke, hovered stroke, label fill |
| `rgba(20,20,20,0.9)` + `white` | treemap / stretched / sunburst `.tooltip` | tooltip chrome (duplicated in 3 files) |
| `#2e7d32` `#1565c0` `#c62828` `#616161` `#ef6c00` | `scan-history.scss` | status chips (completed / running+pending / failed / cancelled / stale) |

### Typography

- Families: **Roboto** 300/400/500 (Google Fonts) + **Material Icons** ligatures. No monospace family is loaded, though `schedules.html` renders cron expressions in `<code>`.
- Scale: Material 3 defaults. Local overrides only: `h1` in scan-detail = `20px`; `.subtitle`, `.file-table`, `.breakdown-row`, `.banner` = `13px`; tooltips and sunburst labels = `12px` / `10px`.

### Spacing, radius, elevation

- No spacing scale — literal px throughout. Recurring values: `4 · 6 · 8 · 10 · 12 · 16 · 24`.
- Layout: content `max-width: 1400px`, padding `16px 24px`; scan-start card `480px`; schedules card `640px`.
- Radius: `4px` on banners and tooltips, `3px` on the type-breakdown bar track. Nothing else is rounded beyond Material defaults.
- Elevation: none used explicitly — only Material component defaults.
- Density: `0` (Material default), except the file table which tightens rows via `--mat-table-header-container-height: 40px` and `--mat-table-row-item-container-height: 32px`.

### Breakpoints

**None.** There is not a single media query in the codebase — no responsive behaviour is defined. The two-pane split is fixed at 35/65 percent at every viewport.

### Third-party component vars

```scss
as-split { --as-gutter-background-color: var(--mat-sys-outline-variant); }
as-split .as-split-gutter:hover,
as-split .as-split-gutter.as-dragged { --as-gutter-background-color: var(--mat-sys-primary); }
```

---

## Part 2 — Raw source

### `frontend/src/styles.scss` (the entire global stylesheet — 58 lines)

```scss
// Include theming for Angular Material with `mat.theme()`.
// This Sass mixin will define CSS variables that are used for styling Angular Material
// components according to the Material 3 design spec.
// Learn more about theming and how to use it for your application's
// custom components at https://material.angular.dev/guide/theming
@use '@angular/material' as mat;
@use 'angular-split/theme';

html {
  height: 100%;
  @include mat.theme(
    (
      color: (
        primary: mat.$azure-palette,
        tertiary: mat.$blue-palette,
      ),
      typography: Roboto,
      density: 0,
    )
  );

  // mat.theme() emits every system colour as `light-dark(<light>, <dark>)`, so `color-scheme`
  // alone decides which half resolves — no second theme block needed. Default is to follow the
  // OS; ThemeService sets data-theme on <html> when the user picks an explicit side.
  color-scheme: light dark;

  &[data-theme='light'] {
    color-scheme: light;
  }

  &[data-theme='dark'] {
    color-scheme: dark;
  }
}

// Match the angular-split gutter to the Material theme instead of its default flat gray.
as-split {
  --as-gutter-background-color: var(--mat-sys-outline-variant);
}

as-split .as-split-gutter:hover,
as-split .as-split-gutter.as-dragged {
  --as-gutter-background-color: var(--mat-sys-primary);
}

body {
  // Set a default background, font and text colors for the application using
  // Angular Material's system-level CSS variables. Learn more about these
  // variables at https://material.angular.dev/guide/system-variables
  background-color: var(--mat-sys-surface);
  color: var(--mat-sys-on-surface);
  font: var(--mat-sys-body-medium);

  // Reset the user agent margin.
  margin: 0;
  height: 100%;
}
/* You can add global styles to this file, and also import other style files */
```

### `frontend/src/app/features/scan-detail/treemap/hierarchy-colors.ts`

The shared color authority for treemap, stretched treemap, sunburst and type-breakdown — all four agree because they share this memoized scale.

```ts
import { schemeTableau10 } from 'd3-scale-chromatic';
import { TreemapNode } from './treemap-layout';

export const DIRECTORY_COLOR = '#3a4a5c';
export const OTHER_COLOR = '#8a8a8a';

/** Memoized extension -> color mapping, shared by every hierarchical visualization so they all
 * agree on colors for the same data. */
export class ExtensionColorScale {
  private readonly cache = new Map<string, string>();

  colorFor(extension: string | null): string {
    const key = extension || '(none)';
    let color = this.cache.get(key);
    if (!color) {
      let hash = 0;
      for (let i = 0; i < key.length; i++) {
        hash = (hash * 31 + key.charCodeAt(i)) >>> 0;
      }
      color = schemeTableau10[hash % schemeTableau10.length];
      this.cache.set(key, color);
    }
    return color;
  }
}

export function colorForTreemapNode(data: TreemapNode, extensionColors: ExtensionColorScale): string {
  if (data.isDirectory) return DIRECTORY_COLOR;
  if (data.isOther) return OTHER_COLOR;
  return extensionColors.colorFor(data.extension);
}
```

### `frontend/src/app/features/scan-history/scan-history.scss` (hardcoded status chips)

```scss
.scans-table { width: 100%; }
.scan-row { cursor: pointer; }
.status-completed { background: #2e7d32; color: white; }
.status-running,
.status-pending { background: #1565c0; color: white; }
.status-failed { background: #c62828; color: white; }
.status-cancelled { background: #616161; color: white; }
.status-stale { background: #ef6c00; color: white; }
```

### Build budgets — `frontend/angular.json`

Any redesign must stay inside these:

```json
"budgets": [
  { "type": "initial", "maximumWarning": "500kB", "maximumError": "1MB" },
  { "type": "anyComponentStyle", "maximumWarning": "4kB", "maximumError": "8kB" }
]
```
