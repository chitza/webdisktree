import { Component, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ImdbLookupCacheService } from '../../core/services/imdb-lookup-cache.service';
import { FormatCountPipe } from '../../shared/format-count.pipe';

const MAX_IMPORT_BYTES = 20 * 1024 * 1024;

@Component({
  selector: 'app-imdb-cache',
  imports: [MatCardModule, MatButtonModule, MatIconModule, FormatCountPipe],
  templateUrl: './imdb-cache.html',
  styleUrl: './imdb-cache.scss',
})
export class ImdbCache {
  private readonly cacheService = inject(ImdbLookupCacheService);

  readonly count = signal<number | null>(null);
  readonly importing = signal(false);
  readonly transferError = signal('');
  readonly lastImportResult = signal<{ added: number; updated: number } | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.cacheService.getSummary().subscribe((summary) => this.count.set(summary.count));
  }

  importCache(input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) return;
    input.value = '';
    this.transferError.set('');
    this.lastImportResult.set(null);
    if (file.size > MAX_IMPORT_BYTES) {
      this.transferError.set('IMDB cache exports must be 20 MiB or smaller.');
      return;
    }
    this.importing.set(true);
    this.cacheService.importCache(file).subscribe({
      next: (result) => {
        this.importing.set(false);
        this.lastImportResult.set(result);
        this.load();
      },
      error: (error) => {
        this.importing.set(false);
        this.transferError.set(typeof error.error === 'string'
          ? error.error : 'Import failed. Choose a valid WebDiskTree IMDB cache export and try again.');
      },
    });
  }
}
