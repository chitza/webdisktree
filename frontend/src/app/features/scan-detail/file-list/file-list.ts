import { Component, OnChanges, SimpleChanges, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { FileService } from '../../../core/services/file.service';
import { FileEntry } from '../../../core/models/directory-node.model';
import { FormatBytesPipe } from '../../../shared/format-bytes.pipe';

@Component({
  selector: 'app-file-list',
  imports: [
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    FormatBytesPipe,
    DatePipe,
  ],
  templateUrl: './file-list.html',
  styleUrl: './file-list.scss',
})
export class FileList implements OnChanges {
  private readonly fileService = inject(FileService);

  readonly scanId = input.required<string>();
  readonly path = input.required<string>();
  readonly canDelete = input(false);

  readonly open = output<string>();
  readonly deleted = output<FileEntry[]>();

  readonly displayedColumns = ['select', 'name', 'sizeBytes', 'modifiedUtc'];
  readonly items = signal<FileEntry[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selection = new SelectionModel<FileEntry>(true, []);

  private sortField: 'name' | 'size' | 'modified' = 'size';
  private sortDir: 'asc' | 'desc' = 'desc';
  private page = 1;
  private readonly pageSize = 25;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['path']) {
      this.page = 1;
      this.selection.clear();
      this.load();
    }
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
