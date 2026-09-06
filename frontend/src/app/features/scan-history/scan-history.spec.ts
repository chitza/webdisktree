import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { ScanService } from '../../core/services/scan.service';
import { ScanStatus, ScanSummary, ScanTrigger } from '../../core/models/scan.model';
import { ScanHistory } from './scan-history';

describe('ScanHistory export', () => {
  it('uses a native HTTP download without triggering row navigation', async () => {
    const scan: ScanSummary = {
      id: '71f5459e-8f21-4d51-9f17-ab51ba223fbb', rootPath: '/',
      trigger: ScanTrigger.Manual, status: ScanStatus.Completed,
      startedAt: null, completedAt: null, totalBytes: 0, totalFiles: 0,
      totalDirs: 0, errorCount: 0, isStale: false, isPinned: false, errorMessage: null,
    };
    await TestBed.configureTestingModule({
      imports: [ScanHistory],
      providers: [provideRouter([]), { provide: ScanService, useValue: { getScans: () => of([scan]) } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(ScanHistory);
    fixture.detectChanges();
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('a[aria-label="Export scan"]');
    expect(link.getAttribute('href')).toBe(`/api/scans/${scan.id}/export`);
    expect(link.hasAttribute('download')).toBe(true);
    expect(link.download).toBe(''); // Use the filename supplied by the server.
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl');
    let preventedByComponent = true;
    link.addEventListener('click', (event) => {
      preventedByComponent = event.defaultPrevented;
      event.preventDefault(); // Prevent jsdom from attempting the native download.
    });
    link.click();
    expect(preventedByComponent).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });
});

describe('ScanHistory pin and delete', () => {
  const scan: ScanSummary = {
    id: 'scan-1', rootPath: '/data', trigger: ScanTrigger.Manual,
    status: ScanStatus.Completed, startedAt: null, completedAt: null,
    totalBytes: 0, totalFiles: 0, totalDirs: 0, errorCount: 0,
    isStale: false, isPinned: false, errorMessage: null,
  };

  async function setup(isPinned = false) {
    const service = {
      getScans: vi.fn(() => of([{ ...scan, isPinned }])),
      setPinned: vi.fn((id: string, pinned: boolean) => of({ ...scan, isPinned: pinned })),
      deleteScan: vi.fn(() => of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [ScanHistory],
      providers: [provideRouter([]), { provide: ScanService, useValue: service }],
    }).compileComponents();
    const fixture = TestBed.createComponent(ScanHistory);
    fixture.detectChanges();
    return { fixture, service };
  }

  it('pins and unpins without navigating', async () => {
    const { fixture, service } = await setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl');
    fixture.nativeElement.querySelector('[aria-label="Pin scan"]').click();
    fixture.detectChanges();
    expect(service.setPinned).toHaveBeenLastCalledWith(scan.id, true);
    fixture.nativeElement.querySelector('[aria-label="Unpin scan"]').click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(service.setPinned).toHaveBeenCalledTimes(1);
    (document.querySelector('mat-dialog-actions button:last-child') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(service.setPinned).toHaveBeenLastCalledWith(scan.id, false));
    expect(navigate).not.toHaveBeenCalled();
  });

  it('keeps the scan pinned when unpinning is cancelled', async () => {
    const { fixture, service } = await setup(true);
    fixture.nativeElement.querySelector('[aria-label="Unpin scan"]').click();
    fixture.detectChanges();
    await fixture.whenStable();
    (document.querySelector('mat-dialog-actions button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(document.querySelector('mat-dialog-container')).toBeNull());
    expect(service.setPinned).not.toHaveBeenCalled();
    expect(fixture.componentInstance.scans()[0].isPinned).toBe(true);
  });

  for (const pinned of [false, true]) {
    it(`requires dialog confirmation${pinned ? ' and pinned acknowledgement' : ''}`, async () => {
      const { fixture, service } = await setup(pinned);
      fixture.nativeElement.querySelector('[aria-label="Delete scan"]').click();
      fixture.detectChanges();
      await fixture.whenStable();
      const dialog = document.querySelector('mat-dialog-container')!;
      expect(dialog).not.toBeNull();
      expect(service.deleteScan).not.toHaveBeenCalled();
      const buttons = dialog.querySelectorAll('mat-dialog-actions button');
      const remove = buttons[1] as HTMLButtonElement;
      expect(remove.disabled).toBe(pinned);
      if (pinned) {
        remove.click();
        expect(service.deleteScan).not.toHaveBeenCalled();
        (dialog.querySelector('input[type="checkbox"]') as HTMLInputElement).click();
        fixture.detectChanges();
      }
      remove.click();
      await fixture.whenStable();
      await vi.waitFor(() => expect(service.deleteScan).toHaveBeenCalledWith(scan.id, pinned));
    });

    it(`cancels deletion of a ${pinned ? 'pinned' : 'regular'} scan`, async () => {
      const { fixture, service } = await setup(pinned);
      fixture.nativeElement.querySelector('[aria-label="Delete scan"]').click();
      fixture.detectChanges();
      await fixture.whenStable();
      (document.querySelector('mat-dialog-actions button') as HTMLButtonElement).click();
      await fixture.whenStable();
      await vi.waitFor(() => expect(document.querySelector('mat-dialog-container')).toBeNull());
      expect(service.deleteScan).not.toHaveBeenCalled();
    });
  }
});
