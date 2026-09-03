import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { ScheduleService } from '../../core/services/schedule.service';
import { ScanService } from '../../core/services/scan.service';
import { Schedule } from '../../core/models/schedule.model';
import { AllowedRoot } from '../../core/models/scan.model';

@Component({
  selector: 'app-schedules',
  imports: [
    FormsModule,
    DatePipe,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
  ],
  templateUrl: './schedules.html',
  styleUrl: './schedules.scss',
})
export class Schedules {
  private readonly scheduleService = inject(ScheduleService);
  private readonly scanService = inject(ScanService);
  private readonly router = inject(Router);

  readonly schedules = signal<Schedule[]>([]);
  readonly roots = signal<AllowedRoot[]>([]);
  readonly displayedColumns = ['rootPath', 'cronExpression', 'enabled', 'lastRunAt', 'nextRunAt', 'actions'];

  newRootPath: string | null = null;
  newCronExpression = '0 3 * * *';
  readonly error = signal<string | null>(null);
  readonly creating = signal(false);

  constructor() {
    this.load();
    this.scanService.getRoots().subscribe((roots) => this.roots.set(roots));
  }

  load(): void {
    this.scheduleService.getSchedules().subscribe((schedules) => this.schedules.set(schedules));
  }

  create(): void {
    if (!this.newRootPath) return;

    this.creating.set(true);
    this.error.set(null);
    this.scheduleService
      .createSchedule({ rootPath: this.newRootPath, cronExpression: this.newCronExpression, enabled: true })
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.newCronExpression = '0 3 * * *';
          this.load();
        },
        error: (err) => {
          this.creating.set(false);
          this.error.set(err?.error ?? 'Failed to create schedule.');
        },
      });
  }

  toggleEnabled(schedule: Schedule): void {
    this.scheduleService
      .updateSchedule(schedule.id, {
        rootPath: schedule.rootPath,
        cronExpression: schedule.cronExpression,
        enabled: !schedule.enabled,
      })
      .subscribe(() => this.load());
  }

  remove(schedule: Schedule): void {
    this.scheduleService.deleteSchedule(schedule.id).subscribe(() => this.load());
  }

  runNow(schedule: Schedule): void {
    this.scheduleService.runNow(schedule.id).subscribe((result) => {
      this.router.navigate(['/scans', result.scanId]);
    });
  }
}
