import { Component, OnChanges, SimpleChanges, inject, input, output, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { FileService } from '../../../core/services/file.service';
import { FileEntry, ImdbLookupStatus } from '../../../core/models/directory-node.model';
import { FormatBytesPipe } from '../../../shared/format-bytes.pipe';
import { LocalDatePipe } from '../../../shared/local-date.pipe';

@Component({
  selector: 'app-file-list',
  imports: [
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatCheckboxModule,
    MatIconModule,
    FormatBytesPipe,
    LocalDatePipe,
  ],
  templateUrl: './file-list.html',
  styleUrl: './file-list.scss',
})
export class FileList implements OnChanges {
  private readonly fileService = inject(FileService);

  readonly ImdbLookupStatus = ImdbLookupStatus;
  readonly scanId = input.required<string>();
  readonly path = input.required<string>();
  readonly canDelete = input(false);
  readonly canGoUp = input(false);

  readonly open = output<string>();
  readonly deleted = output<FileEntry[]>();
  readonly up = output<void>();

  readonly displayedColumns = ['select', 'name', 'sizeBytes', 'modifiedUtc'];
  readonly items = signal<FileEntry[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selection = new SelectionModel<FileEntry>(true, []);
  readonly imdbLookupPending = signal(false);
  readonly imdbLookupMessage = signal('');

  private sortField: 'name' | 'size' | 'modified' = 'size';
  private sortDir: 'asc' | 'desc' = 'desc';
  private page = 1;
  private readonly pageSize = 25;
  private imdbPollHandle: ReturnType<typeof setTimeout> | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['path']) {
      this.page = 1;
      this.selection.clear();
      this.imdbLookupMessage.set('');
      if (this.imdbPollHandle) {
        clearTimeout(this.imdbPollHandle);
        this.imdbPollHandle = null;
      }
      this.load();
    }
  }

  findImdbLinks(): void {
    if (this.imdbLookupPending()) return;

    this.imdbLookupPending.set(true);
    this.imdbLookupMessage.set('');
    this.fileService.triggerImdbLookup(this.scanId(), this.path()).subscribe({
      next: ({ queued, alreadyCached }) => {
        this.imdbLookupPending.set(false);
        if (queued > 0) {
          this.imdbLookupMessage.set(`Looking up ${queued} title${queued === 1 ? '' : 's'} on IMDB…`);
          this.pollForImdbResults();
        } else {
          this.imdbLookupMessage.set(`Nothing new to look up (${alreadyCached} already cached).`);
        }
      },
      error: () => {
        this.imdbLookupPending.set(false);
        this.imdbLookupMessage.set('Could not start the IMDB lookup. Please try again.');
      },
    });
  }

  /** Lookups run in a background queue with no push notification back to the UI, so this polls
   * a bounded number of times to reveal newly-resolved links without requiring a manual refresh. */
  private pollForImdbResults(remaining = 5): void {
    if (remaining <= 0) {
      this.imdbLookupMessage.set('');
      return;
    }
    this.imdbPollHandle = setTimeout(() => {
      this.load();
      this.pollForImdbResults(remaining - 1);
    }, 2000);
  }

  private load(): void {
    this.loading.set(true);
    this.fileService
      .getFiles(this.scanId(), {
        path: this.path(),
        sort: this.sortField,
        dir: this.sortDir,
        page: this.page,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  onSortChange(sort: Sort): void {
    if (!sort.direction) {
      this.sortField = 'size';
      this.sortDir = 'desc';
    } else {
      this.sortField = sort.active as typeof this.sortField;
      this.sortDir = sort.direction;
    }
    this.page = 1;
    this.load();
  }

  onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.load();
  }

  toggleAll(): void {
    if (this.selection.selected.length === this.items().length) {
      this.selection.clear();
    } else {
      this.selection.select(...this.items());
    }
  }

  openRow(row: FileEntry): void {
    if (row.isDirectory) {
      this.open.emit(row.name);
    }
  }

  deleteSelected(): void {
    const selected = this.selection.selected;
    const paths = selected.map((f) => `${this.path()}/${f.name}`);
    if (paths.length === 0) return;

    this.error.set(null);
    this.fileService.deleteFiles(this.scanId(), paths).subscribe({
      next: (result) => {
        const failedPaths = new Set(result.failed.map((f) => f.path));
        const succeeded = selected.filter((_, i) => !failedPaths.has(paths[i]));

        this.selection.clear();
        this.load();

        if (result.failed.length > 0) {
          this.error.set(
            result.failed.length === 1
              ? result.failed[0].reason
              : `${result.failed.length} items could not be deleted: ${result.failed[0].reason}`,
          );
        }
        if (succeeded.length > 0) {
          this.deleted.emit(succeeded);
        }
      },
      error: () => this.error.set('Failed to delete selected items.'),
    });
  }
}
