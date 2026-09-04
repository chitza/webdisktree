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
