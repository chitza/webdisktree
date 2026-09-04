import { Injectable, computed, effect, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';

/** What `preference` actually resolves to right now — `system` collapsed to a real side. */
export type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'webdisktree.theme';
const DARK_QUERY = '(prefers-color-scheme: dark)';

function readStoredPreference(): ThemePreference {
  const stored = localStorage.getItem(STORAGE_KEY);
  return stored === 'light' || stored === 'dark' ? stored : 'system';
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly preference = signal<ThemePreference>(readStoredPreference());

  private readonly systemPrefersDark = signal(false);

  /** CSS resolves `system` on its own via `color-scheme`, but the canvas visualizations paint
   * with explicit colors and have no such mechanism — they need to know which side is live. */
  readonly resolved = computed<ResolvedTheme>(() => {
    const preference = this.preference();
    if (preference !== 'system') return preference;
    return this.systemPrefersDark() ? 'dark' : 'light';
  });

  constructor() {
    // matchMedia is missing in some non-browser test environments; treating that as "light"
    // matches what `color-scheme: light dark` falls back to there anyway.
    const media = typeof window !== 'undefined' && window.matchMedia ? window.matchMedia(DARK_QUERY) : null;
    if (media) {
      this.systemPrefersDark.set(media.matches);
      media.addEventListener('change', (event) => this.systemPrefersDark.set(event.matches));
    }

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
