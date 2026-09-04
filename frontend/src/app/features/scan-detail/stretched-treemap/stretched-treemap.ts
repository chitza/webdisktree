import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { hierarchy, HierarchyRectangularNode, treemap, treemapSliceDice } from 'd3-hierarchy';
import { DirectoryNode } from '../../../core/models/directory-node.model';
import { FormatBytesPipe } from '../../../shared/format-bytes.pipe';
import { buildTreemapNode, TreemapNode } from '../treemap/treemap-layout';
import { prepareCanvas, drawHierarchyNode, findHierarchyNodeAtPoint } from '../treemap/canvas-hierarchy-render';
import { HierarchyColorScale, vizChromeFor } from '../treemap/hierarchy-colors';
import { ThemeService } from '../../../core/services/theme.service';

const MAX_VISIBLE_DEPTH = 2;

interface HoverInfo {
  node: HierarchyRectangularNode<TreemapNode>;
  x: number;
  y: number;
}

@Component({
  selector: 'app-stretched-treemap',
  imports: [FormatBytesPipe],
  templateUrl: './stretched-treemap.html',
  styleUrl: './stretched-treemap.scss',
})
export class StretchedTreemap implements AfterViewInit, OnDestroy {
  readonly node = input.required<DirectoryNode>();
  readonly drill = output<DirectoryNode>();

  @ViewChild('canvasRef') private canvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('containerRef') private containerRef!: ElementRef<HTMLDivElement>;

  readonly hover = signal<HoverInfo | null>(null);

  private layoutRoot: HierarchyRectangularNode<TreemapNode> | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private readonly hierarchyColors = new HierarchyColorScale();
  private readonly theme = inject(ThemeService);

  private readonly layout = computed(() => {
    const plain = buildTreemapNode(this.node(), MAX_VISIBLE_DEPTH);
    const root = hierarchy(plain, (d) => d.children)
      .sum((d) => (d.children && d.children.length > 0 ? 0 : d.size))
      .sort((a, b) => (b.value ?? 0) - (a.value ?? 0));
    return root;
  });

  constructor() {
    effect(() => {
      this.layout();
      // Read inside the effect so switching light/dark (or the OS flipping under
      // `system`) repaints — canvas can't pick up CSS custom properties on its own.
      this.theme.resolved();
      this.render();
    });
  }

  ngAfterViewInit(): void {
    this.resizeObserver = new ResizeObserver(() => this.render());
    this.resizeObserver.observe(this.containerRef.nativeElement);
    this.render();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
  }

  private render(): void {
    const canvas = this.canvasRef?.nativeElement;
    const container = this.containerRef?.nativeElement;
    if (!canvas || !container) return;

    const prepared = prepareCanvas(canvas, container);
    if (!prepared) return;
    const { width, height, ctx } = prepared;

    const rootHierarchy = this.layout();
    const treemapLayout = treemap<TreemapNode>()
      .tile(treemapSliceDice)
      .size([width, height])
      .paddingOuter(2)
      .paddingTop((d) => (d.depth > 0 && d.children && d.data.name ? 16 : 2))
      .paddingInner(1)
      .round(true);

    this.layoutRoot = treemapLayout(rootHierarchy);

    const chrome = vizChromeFor(this.theme.resolved());
    for (const node of this.layoutRoot.descendants()) {
      if (node.depth === 0) continue;
      drawHierarchyNode(ctx, node, this.hover()?.node === node, this.hierarchyColors, chrome);
    }
  }

  onMouseMove(event: MouseEvent): void {
    const found = findHierarchyNodeAtPoint(this.layoutRoot, event.offsetX, event.offsetY);
    this.hover.set(found ? { node: found, x: event.clientX, y: event.clientY } : null);
  }

  onMouseLeave(): void {
    this.hover.set(null);
  }

  onClick(event: MouseEvent): void {
    const found = findHierarchyNodeAtPoint(this.layoutRoot, event.offsetX, event.offsetY);
    if (found?.data.isDirectory && found.data.ref) {
      this.drill.emit(found.data.ref);
    }
  }
}
