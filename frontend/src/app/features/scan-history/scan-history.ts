import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { UnpinScanDialog } from '../../shared/unpin-scan-dialog';
import { DeleteScanDialog } from './delete-scan-dialog';
import { ScanService } from '../../core/services/scan.service';
import { ScanStatus, ScanSummary, ScanTrigger } from '../../core/models/scan.model';
import { FormatBytesPipe } from '../../shared/format-bytes.pipe';
import { FormatCountPipe } from '../../shared/format-count.pipe';
import { LocalDatePipe } from "../../shared/local-date.pipe";

@Component({
  selector: 'app-scan-history',
  imports: [
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    FormatBytesPipe,
    FormatCountPipe,
    LocalDatePipe
],
  templateUrl: './scan-history.html',
  styleUrl: './scan-history.scss',
})
export class ScanHistory {
  private readonly scanService = inject(ScanService);
  private readonly dialog = inject(MatDialog);
  readonly actionError = signal('');
  readonly pinning = signal<string[]>([]);

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

  togglePin(scan: ScanSummary, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    if (this.pinning().includes(scan.id)) return;
    if (scan.isPinned) {
      this.dialog.open(UnpinScanDialog, {
        data: { rootPath: scan.rootPath },
        width: '480px',
        autoFocus: 'first-tabbable',
      }).afterClosed().subscribe(confirmed => {
        if (confirmed === true) this.savePin(scan, false);
      });
    } else {
      this.savePin(scan, true);
    }
  }

  private savePin(scan: ScanSummary, isPinned: boolean): void {
    this.actionError.set('');
    this.pinning.update(ids => [...ids, scan.id]);
    this.scanService.setPinned(scan.id, isPinned).subscribe({
      next: updated => {
        this.scans.update(scans => scans.map(s => s.id === updated.id ? updated : s));
        this.pinning.update(ids => ids.filter(id => id !== scan.id));
      },
      error: () => {
        this.pinning.update(ids => ids.filter(id => id !== scan.id));
        this.actionError.set('Could not update the scan pin. Please try again.');
      },
    });
  }

  remove(scan: ScanSummary, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    this.dialog.open(DeleteScanDialog, {
      data: { rootPath: scan.rootPath, isPinned: scan.isPinned },
      width: '480px',
      autoFocus: 'first-tabbable',
    }).afterClosed().subscribe(confirmed => {
      if (confirmed !== true) return;
      this.actionError.set('');
      this.scanService.deleteScan(scan.id, scan.isPinned).subscribe({
        next: () => this.load(),
        error: (error) => {
          this.actionError.set(typeof error.error === 'string'
            ? error.error : 'Could not delete the scan. Please try again.');
          this.load();
        },
      });
    });
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
