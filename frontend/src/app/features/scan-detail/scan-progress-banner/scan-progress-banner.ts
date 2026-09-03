import { Component, DestroyRef, OnChanges, SimpleChanges, inject, input, output, signal } from '@angular/core';
import { Subscription, filter } from 'rxjs';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ScanProgressEvent, ScanProgressService } from '../../../core/services/scan-progress.service';
import { ScanStatus, ScanSummary } from '../../../core/models/scan.model';
import { FormatBytesPipe } from '../../../shared/format-bytes.pipe';

@Component({
  selector: 'app-scan-progress-banner',
  imports: [MatProgressBarModule, MatButtonModule, MatIconModule, FormatBytesPipe],
  templateUrl: './scan-progress-banner.html',
  styleUrl: './scan-progress-banner.scss',
})
export class ScanProgressBanner implements OnChanges {
  private readonly progressService = inject(ScanProgressService);
  private readonly destroyRef = inject(DestroyRef);

  readonly scan = input.required<ScanSummary>();
  readonly refresh = output<void>();
  readonly rescan = output<void>();

  readonly ScanStatus = ScanStatus;
  readonly progress = signal<ScanProgressEvent | null>(null);

  private joinedScanId: string | null = null;
  private subscriptions = new Subscription();

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.subscriptions.unsubscribe();
      if (this.joinedScanId) {
        this.progressService.leaveScan(this.joinedScanId);
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['scan']) return;

    const scan = this.scan();
    const isActive = scan.status === ScanStatus.Pending || scan.status === ScanStatus.Running;

    if (isActive && this.joinedScanId !== scan.id) {
      this.joinGroup(scan.id);
    } else if (!isActive && this.joinedScanId === scan.id) {
      this.leaveGroup();
    }
  }

  private joinGroup(scanId: string): void {
    this.leaveGroup();

    this.joinedScanId = scanId;
    this.progress.set(null);
    this.progressService.joinScan(scanId);

    this.subscriptions.add(
      this.progressService.progress$
        .pipe(filter((e) => e.scanId === scanId))
        .subscribe((event) => this.progress.set(event)),
    );
    this.subscriptions.add(
      this.progressService.completed$.pipe(filter((e) => e.scanId === scanId)).subscribe(() => this.refresh.emit()),
    );
    this.subscriptions.add(
      this.progressService.failed$.pipe(filter((e) => e.scanId === scanId)).subscribe(() => this.refresh.emit()),
    );
    this.subscriptions.add(
      this.progressService.cancelled$.pipe(filter((e) => e.scanId === scanId)).subscribe(() => this.refresh.emit()),
    );
  }

  private leaveGroup(): void {
    if (this.joinedScanId) {
      this.progressService.leaveScan(this.joinedScanId);
    }
    this.subscriptions.unsubscribe();
    this.subscriptions = new Subscription();
    this.joinedScanId = null;
  }
}
