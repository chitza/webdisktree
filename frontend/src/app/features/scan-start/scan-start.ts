import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';
import { ScanService } from '../../core/services/scan.service';
import { groupRoots, ScanRoot } from '../../core/models/scan.model';

@Component({
  selector: 'app-scan-start',
  imports: [
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    FormsModule,
  ],
  templateUrl: './scan-start.html',
  styleUrl: './scan-start.scss',
})
export class ScanStart {
  private readonly scanService = inject(ScanService);
  private readonly router = inject(Router);

  readonly roots = signal<ScanRoot[]>([]);
  private readonly grouped = computed(() => groupRoots(this.roots()));
  readonly mounts = computed(() => this.grouped().mounts);
  readonly favorites = computed(() => this.grouped().favorites);
  readonly selectedPath = signal<string | null>(null);
  readonly starting = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    this.scanService.getRoots().subscribe({
      next: (roots) => {
        this.roots.set(roots);
        const { mounts, favorites } = groupRoots(roots);
        const only = [...mounts, ...favorites];
        if (only.length === 1) {
          this.selectedPath.set(only[0].path);
        }
      },
      error: () => this.error.set('Failed to load drives and favorites.'),
    });
  }

  startScan(): void {
    const path = this.selectedPath();
    if (!path) return;

    this.starting.set(true);
    this.error.set(null);
    this.scanService.createScan(path).subscribe({
      next: (scan) => this.router.navigate(['/scans', scan.id]),
      error: (err) => {
        this.starting.set(false);
        this.error.set(err?.error ?? 'Failed to start scan.');
      },
    });
  }
}
