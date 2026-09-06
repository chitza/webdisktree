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
      totalDirs: 0, errorCount: 0, isStale: false, errorMessage: null,
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
