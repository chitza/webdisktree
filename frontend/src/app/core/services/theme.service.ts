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
