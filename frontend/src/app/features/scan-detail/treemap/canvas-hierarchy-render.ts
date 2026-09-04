import { HierarchyRectangularNode } from 'd3-hierarchy';
import { TreemapNode } from './treemap-layout';
import { HierarchyColorScale, VizChrome, colorForTreemapNode, labelColorFor } from './hierarchy-colors';

export const MIN_LABEL_WIDTH = 40;
export const MIN_LABEL_HEIGHT = 16;

export interface PreparedCanvas {
  width: number;
  height: number;
  ctx: CanvasRenderingContext2D;
}

/** Sizes `canvas`'s backing store to `container`'s CSS box at devicePixelRatio, sets the transform
 * so draw calls can use CSS-pixel coordinates, and clears it. */
export function prepareCanvas(
  canvas: HTMLCanvasElement,
  container: HTMLDivElement,
  fallbackHeight = 480,
): PreparedCanvas | null {
  const width = container.clientWidth;
  const height = container.clientHeight || fallbackHeight;
  const dpr = window.devicePixelRatio || 1;

  canvas.width = width * dpr;
  canvas.height = height * dpr;
  canvas.style.width = `${width}px`;
  canvas.style.height = `${height}px`;

  const ctx = canvas.getContext('2d');
  if (!ctx) return null;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, width, height);
  return { width, height, ctx };
}

export function drawHierarchyNode(
  ctx: CanvasRenderingContext2D,
  node: HierarchyRectangularNode<TreemapNode>,
  isHovered: boolean,
  hierarchyColors: HierarchyColorScale,
  chrome: VizChrome,
): void {
  const w = node.x1 - node.x0;
  const h = node.y1 - node.y0;
  if (w <= 0 || h <= 0) return;

  const fill = colorForTreemapNode(node.data, hierarchyColors);
  ctx.fillStyle = fill;
  ctx.fillRect(node.x0, node.y0, w, h);

  ctx.strokeStyle = isHovered ? chrome.hoverStroke : chrome.separator;
  ctx.lineWidth = isHovered ? 2 : 1;
  ctx.strokeRect(node.x0, node.y0, w, h);

  if (w >= MIN_LABEL_WIDTH && h >= MIN_LABEL_HEIGHT) {
    // Follows the fill, not the theme: this ramp spans bright hues (#fbbf24, #a3e635) where
    // the previous hardcoded white was unreadable.
    ctx.fillStyle = labelColorFor(fill);
    ctx.font = '11px "IBM Plex Mono", ui-monospace, monospace';
    ctx.textBaseline = 'top';
    const label = node.data.name;
    const maxChars = Math.floor(w / 6);
    const truncated = label.length > maxChars ? label.slice(0, Math.max(0, maxChars - 1)) + '…' : label;
    ctx.fillText(truncated, node.x0 + 3, node.y0 + 2, w - 6);
  }
}

export function findHierarchyNodeAtPoint(
  root: HierarchyRectangularNode<TreemapNode> | null,
  x: number,
  y: number,
): HierarchyRectangularNode<TreemapNode> | null {
  if (!root) return null;

  let best: HierarchyRectangularNode<TreemapNode> | null = null;
  for (const node of root.descendants()) {
    if (node.depth === 0) continue;
    if (x >= node.x0 && x <= node.x1 && y >= node.y0 && y <= node.y1) {
      if (!best || node.depth > best.depth) {
        best = node;
      }
    }
  }
  return best;
}
