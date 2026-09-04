import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, computed, input, output, signal } from '@angular/core';
import { hierarchy, HierarchyRectangularNode, partition } from 'd3-hierarchy';
import { arc, Arc } from 'd3-shape';
import { DirectoryNode } from '../../../core/models/directory-node.model';
import { FormatBytesPipe } from '../../../shared/format-bytes.pipe';
import { buildTreemapNode, TreemapNode } from '../treemap/treemap-layout';
import { HierarchyColorScale, colorForTreemapNode, labelColorFor } from '../treemap/hierarchy-colors';

const MAX_VISIBLE_DEPTH = 3;
const MIN_LABEL_ARC_LENGTH = 16;

interface HoverInfo {
  node: HierarchyRectangularNode<TreemapNode>;
  x: number;
  y: number;
}

interface RadialLayout {
  root: HierarchyRectangularNode<TreemapNode>;
  arcGenerator: Arc<unknown, HierarchyRectangularNode<TreemapNode>>;
}

@Component({
  selector: 'app-sunburst',
  imports: [FormatBytesPipe],
  templateUrl: './sunburst.html',
  styleUrl: './sunburst.scss',
})
export class Sunburst implements AfterViewInit, OnDestroy {
  readonly node = input.required<DirectoryNode>();
  readonly drill = output<DirectoryNode>();

  @ViewChild('containerRef') private containerRef!: ElementRef<HTMLDivElement>;
  private resizeObserver: ResizeObserver | null = null;

  readonly size = signal<{ width: number; height: number }>({ width: 0, height: 0 });
  readonly hover = signal<HoverInfo | null>(null);
  private readonly hierarchyColors = new HierarchyColorScale();

  private readonly radialLayout = computed<RadialLayout | null>(() => {
    const plain = buildTreemapNode(this.node(), MAX_VISIBLE_DEPTH);
    const built = hierarchy(plain, (d) => d.children)
      .sum((d) => (d.children && d.children.length > 0 ? 0 : d.size))
      .sort((a, b) => (b.value ?? 0) - (a.value ?? 0));

    const { width, height } = this.size();
    const radius = Math.min(width, height) / 2;
    if (radius <= 0) return null;

    const root = partition<TreemapNode>().size([2 * Math.PI, radius])(built);
    const arcGenerator = arc<HierarchyRectangularNode<TreemapNode>>()
      .startAngle((d) => d.x0)
      .endAngle((d) => d.x1)
      .padAngle((d) => Math.min((d.x1 - d.x0) / 2, 0.005))
      .padRadius(radius * 1.5)
      .innerRadius((d) => d.y0)
      .outerRadius((d) => Math.max(d.y0, d.y1 - 1));

    return { root, arcGenerator };
  });

  readonly visibleNodes = computed(() => {
    const layout = this.radialLayout();
    return layout ? layout.root.descendants().filter((d) => d.depth > 0) : [];
  });

  ngAfterViewInit(): void {
    this.resizeObserver = new ResizeObserver(() => this.updateSize());
    this.resizeObserver.observe(this.containerRef.nativeElement);
    this.updateSize();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
  }

  private updateSize(): void {
    const el = this.containerRef?.nativeElement;
    if (!el) return;
    this.size.set({ width: el.clientWidth, height: el.clientHeight || 480 });
  }

  arcPath(d: HierarchyRectangularNode<TreemapNode>): string {
    return this.radialLayout()?.arcGenerator(d) ?? '';
  }

  colorFor(d: HierarchyRectangularNode<TreemapNode>): string {
    return colorForTreemapNode(d.data, this.hierarchyColors);
  }

  /** Label ink follows the arc's own fill rather than the theme — the ramp spans bright hues
   * where the previously hardcoded white was unreadable. */
  labelColorFor(d: HierarchyRectangularNode<TreemapNode>): string {
    return labelColorFor(this.colorFor(d));
  }

  isLabelVisible(d: HierarchyRectangularNode<TreemapNode>): boolean {
    return (d.x1 - d.x0) * d.y1 > MIN_LABEL_ARC_LENGTH;
  }

  labelTransform(d: HierarchyRectangularNode<TreemapNode>): string {
    const angleDeg = ((d.x0 + d.x1) / 2) * (180 / Math.PI);
    const r = (d.y0 + d.y1) / 2;
    return `rotate(${angleDeg - 90}) translate(${r},0) rotate(${angleDeg < 180 ? 0 : 180})`;
  }

  onHover(d: HierarchyRectangularNode<TreemapNode>, event: MouseEvent): void {
    this.hover.set({ node: d, x: event.clientX, y: event.clientY });
  }

  onHoverLeave(): void {
    this.hover.set(null);
  }

  onSelect(d: HierarchyRectangularNode<TreemapNode>): void {
    if (d.data.isDirectory && d.data.ref) {
      this.drill.emit(d.data.ref);
    }
  }
}
