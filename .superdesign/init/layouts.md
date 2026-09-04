# Shared Layouts

There is exactly **one** layout: the root `App` shell. No sidebar, no footer, no per-feature layout wrappers, no route-level layout components. Every route renders inside `<main class="content">`.

## App shell — `frontend/src/app/app.ts`

Standalone root component. Holds the theme toggle (cycles light → dark → system → light).

```ts
import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ThemePreference, ThemeService } from './core/services/theme.service';

const THEME_CYCLE: ThemePreference[] = ['light', 'dark', 'system'];

const THEME_ICONS: Record<ThemePreference, string> = {
  light: 'light_mode',
  dark: 'dark_mode',
  system: 'brightness_auto',
};

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  readonly theme = inject(ThemeService);

  readonly themeIcon = computed(() => THEME_ICONS[this.theme.preference()]);
  readonly nextTheme = computed(
    () => THEME_CYCLE[(THEME_CYCLE.indexOf(this.theme.preference()) + 1) % THEME_CYCLE.length],
  );

  cycleTheme(): void {
    this.theme.set(this.nextTheme());
  }
}
```

## App shell template — `frontend/src/app/app.html`

Renders: a Material toolbar with the wordmark **"WebDiskTree"**, three text nav links (Scans / New Scan / Schedules), and a right-aligned theme-toggle icon button. Active nav link is bold + underlined.

```html
<mat-toolbar color="primary" class="toolbar">
  <span class="brand">WebDiskTree</span>
  <nav class="nav-links">
    <a mat-button routerLink="/scans" routerLinkActive="active">Scans</a>
    <a mat-button routerLink="/scans/new" routerLinkActive="active">New Scan</a>
    <a mat-button routerLink="/schedules" routerLinkActive="active">Schedules</a>
  </nav>

  <button
    mat-icon-button
    class="theme-toggle"
    (click)="cycleTheme()"
    [attr.aria-label]="'Theme: ' + theme.preference() + '. Switch to ' + nextTheme() + '.'"
    [title]="'Theme: ' + theme.preference() + ' — click for ' + nextTheme()"
  >
    <mat-icon>{{ themeIcon() }}</mat-icon>
  </button>
</mat-toolbar>

<main class="content">
  <router-outlet />
</main>
```

## App shell styles — `frontend/src/app/app.scss`

Content is centred at `max-width: 1400px` with `16px 24px` padding — **except** on the scan-detail route, where it goes full-bleed and stops scrolling (the visualization manages its own internal scrolling).

```scss
// Constrains .content below to a definite height so scan-detail's own flex layout (header/
// breadcrumb natural height, split panel filling the rest) has a real viewport-derived height
// to fill, instead of the split panel guessing at a fixed vh fraction.
:host {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.toolbar {
  flex: 0 0 auto;

  .brand {
    font-weight: 600;
    margin-right: 24px;
  }

  .nav-links {
    display: flex;
    gap: 4px;

    .active {
      font-weight: 600;
      text-decoration: underline;
    }
  }

  .theme-toggle {
    margin-left: auto;
  }
}

.content {
  flex: 1 1 auto;
  min-height: 0; // flexbox gotcha: without this the child won't shrink below its content size
  overflow-y: auto;
  // Explicit width rather than relying on the flex default (stretch): `margin: 0 auto` is an
  // auto cross-axis margin, which per spec suppresses stretch and shrink-wraps the item
  // instead. Firefox does exactly that; Blink stretches anyway, so without this the layout
  // collapses to content width on Firefox only.
  width: 100%;
  box-sizing: border-box;
  max-width: 1400px;
  margin: 0 auto;
  padding: 16px 24px;

  // The scan detail view (treemap/sunburst) benefits from using the full viewport width and
  // height — it manages its own internal scrolling (file list, tree panels) rather than the
  // whole page scrolling.
  &:has(app-scan-detail) {
    max-width: none;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }
}
```

## Document shell — `frontend/src/index.html`

Loads **Roboto** (300/400/500) and the **Material Icons** ligature font from Google Fonts. `<html>` and `<body>` are both `height: 100%`.

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>WebDiskTree</title>
    <base href="/" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <link rel="icon" type="image/x-icon" href="favicon.ico" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link
      href="https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500&display=swap"
      rel="stylesheet"
    />
    <link href="https://fonts.googleapis.com/icon?family=Material+Icons" rel="stylesheet" />
  </head>
  <body>
    <app-root></app-root>
  </body>
</html>
```

## Bootstrap config — `frontend/src/app/app.config.ts`

Zoneless change detection; animations loaded async.

```ts
import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(),
    provideAnimationsAsync(),
  ],
};
```

## Breadcrumb (not a shared component)

The only breadcrumb lives **inside** `scan-detail.html` — a flat row of `mat-button`s separated by inline `chevron_right` icons. It is not extracted or reusable.
