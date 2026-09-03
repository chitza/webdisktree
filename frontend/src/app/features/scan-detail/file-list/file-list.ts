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
  readonly deleted = output<void>();

  readonly displayedColumns = ['select', 'name', 'extension', 'sizeBytes', 'modifiedUtc'];
  readonly items = signal<FileEntry[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly selection = new SelectionModel<FileEntry>(true, []);

  private sortField: 'name' | 'size' | 'extension' | 'modified' = 'size';
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
    const paths = this.selection.selected.map((f) => `${this.path()}/${f.name}`);
    if (paths.length === 0) return;

    this.fileService.deleteFiles(this.scanId(), paths).subscribe(() => {
      this.selection.clear();
      this.load();
      this.deleted.emit();
    });
  }
}
