import { Component, OnChanges, inject, input, signal } from '@angular/core';
import { FileService } from '../../../core/services/file.service';
import { TypeBreakdownEntry } from '../../../core/models/file-entry.model';
import { FormatBytesPipe } from '../../../shared/format-bytes.pipe';

@Component({
  selector: 'app-type-breakdown',
  imports: [FormatBytesPipe],
  templateUrl: './type-breakdown.html',
  styleUrl: './type-breakdown.scss',
})
export class TypeBreakdown implements OnChanges {
  private readonly fileService = inject(FileService);

  readonly scanId = input.required<string>();

  readonly entries = signal<TypeBreakdownEntry[]>([]);
  readonly loading = signal(true);

  ngOnChanges(): void {
    this.loading.set(true);
    this.fileService.getBreakdown(this.scanId()).subscribe({
      next: (entries) => {
        this.entries.set(entries);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  maxSize(): number {
    return Math.max(1, ...this.entries().map((e) => e.totalSizeBytes));
  }

  barWidth(entry: TypeBreakdownEntry): string {
    return `${Math.max(2, (entry.totalSizeBytes / this.maxSize()) * 100)}%`;
  }
}
