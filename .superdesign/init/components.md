# Shared UI Components

## Stack detection

- **Framework**: Angular 22 (standalone components, signals, zoneless change detection)
- **Meta-framework**: none — Angular CLI application builder (`@angular/build:application`)
- **Component library**: **Angular Material 22** (Material 3 / M3) + `@angular/cdk`
- **CSS approach**: **component-scoped SCSS** (no Tailwind, no CSS modules, no CSS-in-JS). Global theme in `frontend/src/styles.scss`; each component has a sibling `.scss` file referenced via `styleUrl`.
- **Charts/viz**: `d3-hierarchy`, `d3-scale`, `d3-scale-chromatic`, `d3-shape`, `d3-color` — rendered to `<canvas>` (treemaps) and inline `<svg>` (sunburst)
- **Split panes**: `angular-split` (`as-split` / `as-split-area`)
- **Realtime**: `@microsoft/signalr` for live scan progress

## IMPORTANT: this project defines no UI primitives of its own

There is **no `src/components/ui/` directory and no custom Button/Card/Input/Dialog wrapper**. Every primitive is consumed directly from Angular Material in each feature component's `imports: [...]` array. Any reproduction must render *Angular Material M3 defaults themed by `mat.theme()`*, not bespoke components.

### Angular Material primitives actually used

| Primitive | Module | Used in |
|---|---|---|
| Toolbar | `MatToolbarModule` | app shell |
| Button / icon button / stroked / flat | `MatButtonModule` | everywhere |
| Icon (Material Icons ligature font) | `MatIconModule` | everywhere |
| Table + sort + paginator | `MatTableModule`, `MatSortModule`, `MatPaginatorModule` | scan-history, file-list, schedules |
| Chip | `MatChipsModule` | scan-history status |
| Card | `MatCardModule` | scan-start, schedules |
| Form field / select / input | `MatFormFieldModule`, `MatSelectModule`, `MatInputModule` | scan-start, schedules |
| Checkbox | `MatCheckboxModule` | file-list selection |
| Slide toggle | `MatSlideToggleModule` | schedules enable/disable |
| Button toggle group | `MatButtonToggleModule` | scan-detail view switcher |
| Progress bar / spinner | `MatProgressBarModule`, `MatProgressSpinnerModule` | scan progress banner, scan-start |
| Selection model | `SelectionModel` from `@angular/cdk/collections` | file-list |

Icons are **Material Icons ligatures** loaded from Google Fonts in `index.html` (e.g. `<mat-icon>folder</mat-icon>`). Icon names in use: `folder`, `description`, `chevron_right`, `arrow_upward`, `arrow_back`, `delete`, `cancel`, `error`, `warning`, `play_arrow`, `light_mode`, `dark_mode`, `brightness_auto`.

## Shared formatting pipes

These three pipes produce almost every number and date string visible in the UI. Reproductions should format sample data the same way (e.g. `1.4 GB`, `12.345`, `04.09.26, 14:30`).

### `frontend/src/app/shared/format-bytes.pipe.ts`
Binary byte formatter — `0 B`, whole numbers for bytes, one decimal above.

```ts
import { Pipe, PipeTransform } from '@angular/core';

const UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];

@Pipe({ name: 'formatBytes' })
export class FormatBytesPipe implements PipeTransform {
  transform(bytes: number | null | undefined): string {
    if (bytes === null || bytes === undefined || Number.isNaN(bytes)) return '—';
    if (bytes === 0) return '0 B';

    const exponent = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), UNITS.length - 1);
    const value = bytes / Math.pow(1024, exponent);
    return `${value.toFixed(exponent === 0 ? 0 : 1)} ${UNITS[exponent]}`;
  }
}
```

### `frontend/src/app/shared/format-count.pipe.ts`
Thousands-separated counts using a **de-DE** grouping (`12.345`).

```ts
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'formatCount' })
export class FormatCountPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(value)) return '—';
    return value.toLocaleString('de-DE');
  }
}
```

### `frontend/src/app/shared/local-date.pipe.ts`
Browser-locale short date+time, unlike Angular's `DatePipe` which is pinned to `LOCALE_ID`.

```ts
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'localDate' })
export class LocalDatePipe implements PipeTransform {
  // Passing no locale to Intl.DateTimeFormat resolves to the browser's own locale, unlike
  // Angular's DatePipe which always formats using the app's fixed LOCALE_ID (en-US).
  private readonly formatter = new Intl.DateTimeFormat(undefined, {
    dateStyle: 'short',
    timeStyle: 'short',
  });

  transform(value: string | Date | null | undefined): string {
    if (!value) return '';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return this.formatter.format(date);
  }
}
```

## Theme service (drives light/dark)

`frontend/src/app/core/services/theme.service.ts` — the *only* stateful UI service. It writes `data-theme` on `<html>`; `styles.scss` keys `color-scheme` off that attribute.

```ts
import { Injectable, effect, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'webdisktree.theme';

function readStoredPreference(): ThemePreference {
  const stored = localStorage.getItem(STORAGE_KEY);
  return stored === 'light' || stored === 'dark' ? stored : 'system';
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly preference = signal<ThemePreference>(readStoredPreference());

  constructor() {
    effect(() => {
      const preference = this.preference();
      // styles.scss keys `color-scheme` off this attribute; leaving it off means the bare
      // `color-scheme: light dark` applies, which defers to the OS setting.
      if (preference === 'system') {
        document.documentElement.removeAttribute('data-theme');
      } else {
        document.documentElement.setAttribute('data-theme', preference);
      }
      localStorage.setItem(STORAGE_KEY, preference);
    });
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
  }
}
```

**Note for design work:** `preference` is tri-state (`light` / `dark` / `system`) but there is no *resolved* signal — nothing exposes whether `system` currently means light or dark. The canvas/SVG visualizations consequently do **not** repaint on a theme switch.
