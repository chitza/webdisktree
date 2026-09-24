import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ScanService } from '../../core/services/scan.service';
import { ScheduleService } from '../../core/services/schedule.service';
import { ScanRoot } from '../../core/models/scan.model';
import { Schedules } from './schedules';

describe('Schedules root picker', () => {
  async function setup(roots: ScanRoot[]) {
    const scanService = { getRoots: vi.fn(() => of(roots)) };
    const scheduleService = {
      getSchedules: vi.fn(() => of([])),
      createSchedule: vi.fn(),
      updateSchedule: vi.fn(),
      deleteSchedule: vi.fn(),
      runNow: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [Schedules],
      providers: [
        provideRouter([]),
        { provide: ScanService, useValue: scanService },
        { provide: ScheduleService, useValue: scheduleService },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Schedules);
    fixture.detectChanges();
    return { fixture };
  }

  function openPanel(fixture: { nativeElement: HTMLElement; detectChanges: () => void }) {
    (fixture.nativeElement.querySelector('.mat-mdc-select-trigger') as HTMLElement).click();
    fixture.detectChanges();
  }

  it('renders mounts under "Drives and mounts" and favorites as label (path) under "Favorites"', async () => {
    const roots: ScanRoot[] = [
      { path: '/hostfs/media', label: 'Media', kind: 'mount', allowDelete: false },
      { path: '/hostfs/data/movies', label: 'Movies', kind: 'favorite', allowDelete: true },
    ];
    const { fixture } = await setup(roots);
    openPanel(fixture);

    const groupLabels = Array.from(document.querySelectorAll('.mat-mdc-optgroup-label')).map((el) =>
      el.textContent?.trim(),
    );
    expect(groupLabels).toEqual(['Drives and mounts', 'Favorites']);

    const options = Array.from(document.querySelectorAll('.mat-mdc-option')).map((el) => el.textContent?.trim());
    expect(options).toEqual(['Media', 'Movies (/hostfs/data/movies)']);
  });

  it("excludes a mount whose path matches a favorite's path from Drives and mounts", async () => {
    const roots: ScanRoot[] = [
      { path: '/hostfs/media', label: 'Media', kind: 'mount', allowDelete: false },
      { path: '/hostfs/media', label: 'My Media', kind: 'favorite', allowDelete: true },
    ];
    const { fixture } = await setup(roots);
    openPanel(fixture);

    expect(document.querySelector('.mat-mdc-optgroup-label')?.textContent?.trim()).toBe('Favorites');
    const options = Array.from(document.querySelectorAll('.mat-mdc-option')).map((el) => el.textContent?.trim());
    expect(options).toEqual(['My Media (/hostfs/media)']);
  });
});
