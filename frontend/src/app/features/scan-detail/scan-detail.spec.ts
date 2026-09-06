import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, EMPTY, of } from 'rxjs';
import { ScanService } from '../../core/services/scan.service';
import { FileService } from '../../core/services/file.service';
import { ScanStatus, ScanTrigger } from '../../core/models/scan.model';
import { DirectoryNode, FileEntry } from '../../core/models/directory-node.model';
import { ScanDetail } from './scan-detail';

const file = (name: string, sizeBytes: number, isDirectory = false): FileEntry => ({
  name, sizeBytes, isDirectory, extension: null, modifiedUtc: '2026-09-06T00:00:00Z',
});
const directory = (name: string, fullPath: string): DirectoryNode => ({
  ...file(name, 100, true), fullPath, isSymlink: false, directories: [], files: [],
  otherFilesCount: 0, otherFilesSizeBytes: 0,
});

describe('ScanDetail freed space', () => {
  const params = new BehaviorSubject(convertToParamMap({ id: 'first' }));

  beforeEach(async () => {
    params.next(convertToParamMap({ id: 'first' }));
    await TestBed.configureTestingModule({
      imports: [ScanDetail],
      providers: [provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
        { provide: ScanService, useValue: {
          getScan: (id: string) => of({ id, rootPath: '/root', status: ScanStatus.Completed,
            trigger: ScanTrigger.Manual, isStale: true, totalBytes: 100, totalFiles: 2, totalDirs: 1, errorCount: 0 }),
          getRoots: () => of([]),
        } },
        { provide: FileService, useValue: { getTree: () => EMPTY } },
      ],
    }).compileComponents();
  });

  it('shows a running total, keeps it on refresh, and resets on a different scan', () => {
    const fixture = TestBed.createComponent(ScanDetail);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.freed-space')).toBeNull();
    component.onFilesDeleted([file('a', 1024)]);
    component.onFilesDeleted([file('b', 2048)]);
    component.onRefresh();
    fixture.detectChanges();
    expect(component.freedBytes()).toBe(3072);
    expect(fixture.nativeElement.querySelector('.banner-stale .freed-space').textContent).toContain('Freed-up');
    params.next(convertToParamMap({ id: 'second' }));
    expect(component.freedBytes()).toBe(0);
  });

  it('does not count a deleted child again when its parent folder is deleted', () => {
    const component = TestBed.createComponent(ScanDetail).componentInstance;
    const root = directory('root', '/root');
    const child = directory('child', '/root/child');
    child.files = [file('a', 30)];
    root.directories = [child];
    component.breadcrumb.set([root, child]);
    component.onFilesDeleted([file('a', 30)]);
    component.onNavigateUp();
    component.onFilesDeleted([file('child', 100, true)]);
    expect(component.freedBytes()).toBe(100);
    expect(component.focus()?.sizeBytes).toBe(0);
  });
});
