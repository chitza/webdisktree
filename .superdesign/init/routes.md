# Routes

Config-based routing via `provideRouter(routes)`. All feature routes are lazy (`loadComponent`). Every route renders inside the single `App` shell (`layouts.md`).

| URL | Component | File | Layout |
|---|---|---|---|
| `/` | — | redirects to `/scans` | — |
| `/scans` | `ScanHistory` | `frontend/src/app/features/scan-history/scan-history.ts` | App shell (centred, max 1400px) |
| `/scans/new` | `ScanStart` | `frontend/src/app/features/scan-start/scan-start.ts` | App shell (centred, max 1400px) |
| `/scans/:id` | `ScanDetail` | `frontend/src/app/features/scan-detail/scan-detail.ts` | App shell (**full-bleed**, no page scroll) |
| `/schedules` | `Schedules` | `frontend/src/app/features/schedules/schedules.ts` | App shell (centred, max 1400px) |
| `**` | — | redirects to `/scans` | — |

Route order matters: `scans/new` is declared **before** `scans/:id` so the literal path wins.

## Full router config — `frontend/src/app/app.routes.ts`

```ts
import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'scans' },
  {
    path: 'scans/new',
    loadComponent: () => import('./features/scan-start/scan-start').then((m) => m.ScanStart),
  },
  {
    path: 'scans/:id',
    loadComponent: () => import('./features/scan-detail/scan-detail').then((m) => m.ScanDetail),
  },
  {
    path: 'scans',
    loadComponent: () => import('./features/scan-history/scan-history').then((m) => m.ScanHistory),
  },
  {
    path: 'schedules',
    loadComponent: () => import('./features/schedules/schedules').then((m) => m.Schedules),
  },
  { path: '**', redirectTo: 'scans' },
];
```

## What each page renders

### `/scans` — Scan history (list)
`<h1>Scan history</h1>` then a full-width `mat-table` with columns **Root path · Trigger · Status · Started · Size · Files · (actions)**. Status is a `mat-chip` in one of five colors (completed / running+pending / failed / cancelled / stale). Rows are clickable and route to `/scans/:id`; running scans get a `cancel` icon button. Empty state: *"No scans yet. Start one."*

### `/scans/new` — Start a scan (form)
A single narrow `mat-card` (`max-width: 480px`) titled **"Start a new scan"**, containing an outline `mat-select` of allowed roots and a flat primary **"Start scan"** button (swaps to an 18px spinner while starting).

### `/scans/:id` — Scan detail (**the primary screen**)
The densest and most identity-bearing page:
- **Header row**: `<h1>` with the root path (`word-break: break-all`) + a subtitle line of `size · N files · N folders · [N unreadable paths]`; a right-aligned "Back to history" button.
- **Progress banner** (conditional): running / failed / stale variants.
- **Breadcrumb**: `mat-button` per path segment, `chevron_right` separators.
- **Two-pane `as-split`** (horizontal, percent, 4px gutter): **left 35%** = file list, **right 65%** = visualization panel.
- The viz panel has a `mat-button-toggle-group` switcher — **Treemap · Stretched treemap · Sunburst · File types** — above the viz body.

### `/schedules` — Scheduled scans
`<h1>Scheduled scans</h1>`, a `mat-card` (`max-width: 640px`) with a root `mat-select` + cron-expression text input side by side and a "Create schedule" button, then a full-width `mat-table` with **Root · Cron (as `<code>`) · Enabled (slide toggle) · Last run · Next run · (run/delete icon buttons)**.
