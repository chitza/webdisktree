import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { UnpinScanDialog } from '../../shared/unpin-scan-dialog';
import { MatIconModule } from '@angular/material/icon';
import { SplitAreaComponent, SplitComponent } from 'angular-split';
import { ScanService } from '../../core/services/scan.service';
import { FileService } from '../../core/services/file.service';
import { DirectoryNode, FileEntry } from '../../core/models/directory-node.model';
import { ScanStatus, ScanSummary, ScanTrigger } from '../../core/models/scan.model';
import { Treemap } from './treemap/treemap';
import { StretchedTreemap } from './stretched-treemap/stretched-treemap';
import { Sunburst } from './sunburst/sunburst';
import { FileList } from './file-list/file-list';
import { TypeBreakdown } from './type-breakdown/type-breakdown';
import { ScanProgressBanner } from './scan-progress-banner/scan-progress-banner';
import { FormatBytesPipe } from '../../shared/format-bytes.pipe';
import { FormatCountPipe } from '../../shared/format-count.pipe';

type ViewMode = 'treemap' | 'stretched' | 'sunburst' | 'types';

@Component({
  selector: 'app-scan-detail',
  imports: [
    RouterLink,
    MatButtonToggleModule,
    MatIconModule,
    SplitComponent,
    SplitAreaComponent,
    Treemap,
    StretchedTreemap,
    Sunburst,
    FileList,
    TypeBreakdown,
    ScanProgressBanner,
    FormatBytesPipe,
    FormatCountPipe,
  ],
  templateUrl: './scan-detail.html',
  styleUrl: './scan-detail.scss',
})
export class ScanDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scanService = inject(ScanService);
  private readonly dialog = inject(MatDialog);
  private readonly fileService = inject(FileService);

  readonly ScanStatus = ScanStatus;
  readonly ScanTrigger = ScanTrigger;
  readonly scan = signal<ScanSummary | null>(null);
  readonly breadcrumb = signal<DirectoryNode[]>([]);
  readonly loadingTree = signal(false);
  readonly pinning = signal(false);
  readonly pinError = signal('');
  readonly canDelete = signal(false);
  readonly viewMode = signal<ViewMode>('treemap');
  readonly freedBytes = signal(0);
  private readonly deletedSizes = new Map<string, number>();

  readonly focus = computed(() => {
    const crumbs = this.breadcrumb();
    return crumbs.length > 0 ? crumbs[crumbs.length - 1] : null;
  });

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) {
        this.pinError.set('');
        this.freedBytes.set(0);
        this.deletedSizes.clear();
        this.loadScan(id);
      }
    });
  }

  private loadScan(id: string): void {
    this.scanService.getScan(id).subscribe((scan) => {
      this.scan.set(scan);
      this.canDelete.set(false);
      if (scan.trigger !== ScanTrigger.Imported) this.checkDeletePermission(scan.rootPath);
      if (scan.status === ScanStatus.Completed) {
        this.loadTree(id);
      }
    });
  }

  private checkDeletePermission(rootPath: string): void {
    this.scanService.getRoots().subscribe((roots) => {
      this.canDelete.set(roots.some((r) => r.allowDelete && rootPath.startsWith(r.path)));
    });
  }

  private loadTree(scanId: string): void {
    this.loadingTree.set(true);
    this.fileService.getTree(scanId).subscribe({
      next: (root) => {
        this.breadcrumb.set([root]);
        this.loadingTree.set(false);
      },
      error: () => this.loadingTree.set(false),
    });
  }

  togglePin(): void {
    const current = this.scan();
    if (!current || this.pinning()) return;
    if (current.isPinned) {
      this.dialog.open(UnpinScanDialog, {
        data: { rootPath: current.rootPath },
        width: '480px',
        autoFocus: 'first-tabbable',
      }).afterClosed().subscribe(confirmed => {
        if (confirmed === true && this.scan()?.id === current.id) this.savePin(current, false);
      });
    } else {
      this.savePin(current, true);
    }
  }

  private savePin(current: ScanSummary, isPinned: boolean): void {
    this.pinning.set(true);
    this.pinError.set('');
    this.scanService.setPinned(current.id, isPinned).subscribe({
      next: updated => {
        this.scan.update(scan => scan?.id === updated.id
          ? { ...scan, isPinned: updated.isPinned } : scan);
        this.pinning.set(false);
      },
      error: () => {
        this.pinning.set(false);
        if (this.scan()?.id === current.id) {
          this.pinError.set('Could not update the scan pin. Please try again.');
        }
      },
    });
  }

  onRefresh(): void {
    const current = this.scan();
    if (current) this.loadScan(current.id);
  }

  onFilesDeleted(deletedEntries: FileEntry[]): void {
    const parentPath = this.focus()?.fullPath;
    const adjustedEntries = deletedEntries.map((entry) => {
      const path = `${parentPath?.replace(/[\\/]$/, '') ?? ''}/${entry.name}`;
      let alreadyCounted = 0;
      for (const [deletedPath, size] of this.deletedSizes) {
        if (deletedPath === path || deletedPath.startsWith(`${path}/`)) {
          alreadyCounted += size;
          this.deletedSizes.delete(deletedPath);
        }
      }
      this.deletedSizes.set(path, entry.sizeBytes);
      return { ...entry, sizeBytes: Math.max(0, entry.sizeBytes - alreadyCounted) };
    });
    this.freedBytes.update((bytes) => bytes + adjustedEntries.reduce((sum, entry) => sum + entry.sizeBytes, 0));
    const current = this.scan();
    if (current) {
      // Refresh stale status; the freed-space total stays local to this view.
      this.scanService.getScan(current.id).subscribe((scan) => this.scan.set(scan));
    }

    this.breadcrumb.update((crumbs) => this.removeFromBreadcrumb(crumbs, adjustedEntries));
  }

  /** The tree the treemap renders comes from a static blob generated at scan time — deleting rows from
   * it server-side would require regenerating that blob, so instead patch the in-memory copy directly,
   * the same way the file list already reflects deletions against its own (live) data source. */
  private removeFromBreadcrumb(crumbs: DirectoryNode[], deletedEntries: FileEntry[]): DirectoryNode[] {
    if (crumbs.length === 0 || deletedEntries.length === 0) return crumbs;

    const removedDirNames = new Set(deletedEntries.filter((e) => e.isDirectory).map((e) => e.name));
    const removedFileNames = new Set(deletedEntries.filter((e) => !e.isDirectory).map((e) => e.name));
    const removedBytes = deletedEntries.reduce((sum, e) => sum + e.sizeBytes, 0);

    const patched = crumbs.slice();
    const leafIndex = patched.length - 1;
    const leaf = patched[leafIndex];

    const itemizedFileNames = new Set(leaf.files.map((f) => f.name));
    const aggregatedDeletedFiles = deletedEntries.filter(
      (e) => !e.isDirectory && !itemizedFileNames.has(e.name),
    );
    const aggregatedBytes = aggregatedDeletedFiles.reduce((sum, e) => sum + e.sizeBytes, 0);

    patched[leafIndex] = {
      ...leaf,
      directories: leaf.directories.filter((d) => !removedDirNames.has(d.name)),
      files: leaf.files.filter((f) => !removedFileNames.has(f.name)),
      sizeBytes: leaf.sizeBytes - removedBytes,
      otherFilesCount: leaf.otherFilesCount - aggregatedDeletedFiles.length,
      otherFilesSizeBytes: leaf.otherFilesSizeBytes - aggregatedBytes,
    };

    for (let i = leafIndex - 1; i >= 0; i--) {
      const child = patched[i + 1];
      patched[i] = {
        ...patched[i],
        sizeBytes: patched[i].sizeBytes - removedBytes,
        directories: patched[i].directories.map((d) => (d.fullPath === child.fullPath ? child : d)),
      };
    }

    return patched;
  }

  onRescan(): void {
    const current = this.scan();
    if (!current) return;
    this.scanService.createScan(current.rootPath).subscribe((newScan) => {
      this.router.navigate(['/scans', newScan.id]);
    });
  }

  onDrill(node: DirectoryNode): void {
    this.breadcrumb.update((crumbs) => [...crumbs, node]);
  }

  onBreadcrumbClick(index: number): void {
    this.breadcrumb.update((crumbs) => crumbs.slice(0, index + 1));
  }

  onNavigateUp(): void {
    this.onBreadcrumbClick(this.breadcrumb().length - 2);
  }

  onOpenByName(name: string): void {
    const current = this.focus();
    const child = current?.directories.find((d) => d.name === name);
    if (child) this.onDrill(child);
  }

  onViewModeChange(mode: ViewMode): void {
    this.viewMode.set(mode);
  }
}
