import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { hierarchy, HierarchyRectangularNode, treemap, treemapSquarify } from 'd3-hierarchy';
import { schemeTableau10 } from 'd3-scale-chromatic';
import { DirectoryNode } from '../../../core/models/directory-node.model';
import { FormatBytesPipe } from '../../../shared/format-bytes.pipe';
import { buildTreemapNode, TreemapNode } from './treemap-layout';

const MAX_VISIBLE_DEPTH = 2;
const MIN_LABEL_WIDTH = 40;
const MIN_LABEL_HEIGHT = 16;
const DIRECTORY_COLOR = '#3a4a5c';
const OTHER_COLOR = '#8a8a8a';

interface HoverInfo {
  node: HierarchyRectangularNode<TreemapNode>;
  x: number;
  y: number;
}

@Component({
  selector: 'app-treemap',
  imports: [FormatBytesPipe],
  templateUrl: './treemap.html',
  styleUrl: './treemap.scss',
})
export class Treemap implements AfterViewInit, OnDestroy {
  readonly node = input.required<DirectoryNode>();
  readonly drill = output<DirectoryNode>();

  @ViewChild('canvasRef') private canvasRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('containerRef') private containerRef!: ElementRef<HTMLDivElement>;

  readonly hover = signal<HoverInfo | null>(null);

  private layoutRoot: HierarchyRectangularNode<TreemapNode> | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private extensionColorCache = new Map<string, string>();

  private readonly layout = computed(() => {
    const plain = buildTreemapNode(this.node(), MAX_VISIBLE_DEPTH);
    const root = hierarchy(plain, (d) => d.children).sum((d) =>
      d.children && d.children.length > 0 ? 0 : d.size,
    );
    return root;
  });

  constructor() {
    effect(() => {
      this.layout();
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

    const width = container.clientWidth;
    const height = container.clientHeight || 480;
    const dpr = window.devicePixelRatio || 1;

    canvas.width = width * dpr;
    canvas.height = height * dpr;
    canvas.style.width = `${width}px`;
    canvas.style.height = `${height}px`;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);

    const rootHierarchy = this.layout();
    const treemapLayout = treemap<TreemapNode>()
      .tile(treemapSquarify)
      .size([width, height])
      .paddingOuter(2)
      // depth 0 (the focus root) is never drawn — see the loop below — so it must not reserve a label
      // header either, or that space becomes a dead, unclickable gap above the actually-visible content.
      .paddingTop((d) => (d.depth > 0 && d.children && d.data.name ? 16 : 2))
      .paddingInner(1)
      .round(true);

    this.layoutRoot = treemapLayout(rootHierarchy);

    for (const node of this.layoutRoot.descendants()) {
      if (node.depth === 0) continue;
      this.drawNode(ctx, node);
    }
  }

  private drawNode(ctx: CanvasRenderingContext2D, node: HierarchyRectangularNode<TreemapNode>): void {
    const w = node.x1 - node.x0;
    const h = node.y1 - node.y0;
    if (w <= 0 || h <= 0) return;

    const isHovered = this.hover()?.node === node;
    ctx.fillStyle = this.colorFor(node);
    ctx.fillRect(node.x0, node.y0, w, h);

    ctx.strokeStyle = isHovered ? '#ffffff' : 'rgba(255,255,255,0.25)';
    ctx.lineWidth = isHovered ? 2 : 1;
    ctx.strokeRect(node.x0, node.y0, w, h);

    if (w >= MIN_LABEL_WIDTH && h >= MIN_LABEL_HEIGHT) {
      ctx.fillStyle = '#ffffff';
      ctx.font = '11px sans-serif';
      ctx.textBaseline = 'top';
      const label = node.data.name;
      const maxChars = Math.floor(w / 6);
      const truncated = label.length > maxChars ? label.slice(0, Math.max(0, maxChars - 1)) + '…' : label;
      ctx.fillText(truncated, node.x0 + 3, node.y0 + 2, w - 6);
    }
  }

  private colorFor(node: HierarchyRectangularNode<TreemapNode>): string {
    const data = node.data;
    if (data.isDirectory) return DIRECTORY_COLOR;
    if (data.isOther) return OTHER_COLOR;
    return this.colorForExtension(data.extension);
  }

  private colorForExtension(extension: string | null): string {
    const key = extension || '(none)';
    let color = this.extensionColorCache.get(key);
    if (!color) {
      let hash = 0;
      for (let i = 0; i < key.length; i++) {
        hash = (hash * 31 + key.charCodeAt(i)) >>> 0;
      }
      color = schemeTableau10[hash % schemeTableau10.length];
      this.extensionColorCache.set(key, color);
    }
    return color;
  }

  onMouseMove(event: MouseEvent): void {
    const found = this.findNodeAtPoint(event.offsetX, event.offsetY);
    this.hover.set(found ? { node: found, x: event.clientX, y: event.clientY } : null);
  }

  onMouseLeave(): void {
    this.hover.set(null);
  }

  onClick(event: MouseEvent): void {
    const found = this.findNodeAtPoint(event.offsetX, event.offsetY);
    if (found?.data.isDirectory && found.data.ref) {
      this.drill.emit(found.data.ref);
    }
  }

  private findNodeAtPoint(x: number, y: number): HierarchyRectangularNode<TreemapNode> | null {
    if (!this.layoutRoot) return null;

    let best: HierarchyRectangularNode<TreemapNode> | null = null;
    for (const node of this.layoutRoot.descendants()) {
      if (node.depth === 0) continue;
      if (x >= node.x0 && x <= node.x1 && y >= node.y0 && y <= node.y1) {
        if (!best || node.depth > best.depth) {
          best = node;
        }
      }
    }
    return best;
  }
}
