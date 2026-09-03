import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
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
    MatChipsModule,
    FormatBytesPipe,
    FormatCountPipe,
    DatePipe,
  ],
  templateUrl: './scan-history.html',
  styleUrl: './scan-history.scss',
})
export class ScanHistory {
  private readonly scanService = inject(ScanService);

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

  cancel(scan: ScanSummary, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    this.scanService.cancelScan(scan.id).subscribe(() => this.load());
  }

  isCancellable(scan: ScanSummary): boolean {
    return scan.status === ScanStatus.Pending || scan.status === ScanStatus.Running;
  }

  statusLabel(status: ScanStatus): string {
    return ScanStatus[status];
  }

  triggerLabel(trigger: ScanTrigger): string {
    return ScanTrigger[trigger];
  }
}
