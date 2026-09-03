import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { ScanService } from '../../core/services/scan.service';
import { FileService } from '../../core/services/file.service';
import { DirectoryNode } from '../../core/models/directory-node.model';
import { ScanStatus, ScanSummary } from '../../core/models/scan.model';
import { Treemap } from './treemap/treemap';
import { FileList } from './file-list/file-list';
import { TypeBreakdown } from './type-breakdown/type-breakdown';
import { ScanProgressBanner } from './scan-progress-banner/scan-progress-banner';
import { FormatBytesPipe } from '../../shared/format-bytes.pipe';

@Component({
  selector: 'app-scan-detail',
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    Treemap,
    FileList,
    TypeBreakdown,
    ScanProgressBanner,
    FormatBytesPipe,
  ],
  templateUrl: './scan-detail.html',
  styleUrl: './scan-detail.scss',
})
export class ScanDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scanService = inject(ScanService);
  private readonly fileService = inject(FileService);

  readonly ScanStatus = ScanStatus;
  readonly scan = signal<ScanSummary | null>(null);
  readonly breadcrumb = signal<DirectoryNode[]>([]);
  readonly loadingTree = signal(false);
  readonly canDelete = signal(false);

  readonly focus = computed(() => {
    const crumbs = this.breadcrumb();
    return crumbs.length > 0 ? crumbs[crumbs.length - 1] : null;
  });

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) this.loadScan(id);
    });
  }

  private loadScan(id: string): void {
    this.scanService.getScan(id).subscribe((scan) => {
      this.scan.set(scan);
      this.checkDeletePermission(scan.rootPath);
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

  onRefresh(): void {
    const current = this.scan();
    if (current) this.loadScan(current.id);
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

  onOpenByName(name: string): void {
    const current = this.focus();
    const child = current?.directories.find((d) => d.name === name);
    if (child) this.onDrill(child);
  }
}
