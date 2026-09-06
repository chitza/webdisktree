import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ScanService } from '../../core/services/scan.service';
import { ScanStatus, ScanSummary, ScanTrigger } from '../../core/models/scan.model';
import { FormatBytesPipe } from '../../shared/format-bytes.pipe';
import { FormatCountPipe } from '../../shared/format-count.pipe';

@Component({
  selector: 'app-scan-history',
  imports: [
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    FormatBytesPipe,
    FormatCountPipe,
    DatePipe,
  ],
  templateUrl: './scan-history.html',
  styleUrl: './scan-history.scss',
})
export class ScanHistory {
  private readonly scanService = inject(ScanService);

  private readonly router = inject(Router);

  readonly importing = signal(false);
  readonly transferError = signal('');

  readonly ScanStatus = ScanStatus;
  readonly ScanTrigger = ScanTrigger;
  readonly displayedColumns = ['rootPath', 'trigger', 'status', 'startedAt', 'totalBytes', 'totalFiles', 'actions'];
  readonly scans = signal<ScanSummary[]>([]);
  readonly loading = signal(true);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.scanService.getScans().subscribe({
      next: (scans) => {
        this.scans.set(scans);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  importScan(input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) return;
    input.value = '';
    this.transferError.set('');
    if (file.size > 100 * 1024 * 1024) {
      this.transferError.set('Scan exports must be 100 MiB or smaller.');
      return;
    }
    this.importing.set(true);
    this.scanService.importScan(file).subscribe({
      next: (scan) => {
        this.importing.set(false);
        this.router.navigate(['/scans', scan.id]);
      },
      error: (error) => {
        this.importing.set(false);
        this.transferError.set(typeof error.error === 'string'
          ? error.error : 'Import failed. Choose a valid WebDiskTree .tar.gz export and try again.');
      },
    });
  }

  cancel(scan: ScanSummary, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    this.scanService.cancelScan(scan.id).subscribe(() => this.load());
  }

  remove(scan: ScanSummary, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    if (!confirm(`Delete the scan of "${scan.rootPath}"? This cannot be undone.`)) {
      return;
    }
    this.scanService.deleteScan(scan.id).subscribe(() => this.load());
  }

  isCancellable(scan: ScanSummary): boolean {
    return scan.status === ScanStatus.Pending || scan.status === ScanStatus.Running;
  }

  isDeletable(scan: ScanSummary): boolean {
    return !this.isCancellable(scan);
  }

  statusLabel(status: ScanStatus): string {
    return ScanStatus[status];
  }

  triggerLabel(trigger: ScanTrigger): string {
    return ScanTrigger[trigger];
  }
}
