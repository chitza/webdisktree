import { schemeTableau10 } from 'd3-scale-chromatic';
import { TreemapNode } from './treemap-layout';

export const DIRECTORY_COLOR = '#3a4a5c';
export const OTHER_COLOR = '#8a8a8a';

/** Memoized extension -> color mapping, shared by every hierarchical visualization so they all
 * agree on colors for the same data. */
export class ExtensionColorScale {
  private readonly cache = new Map<string, string>();

  colorFor(extension: string | null): string {
    const key = extension || '(none)';
    let color = this.cache.get(key);
    if (!color) {
      let hash = 0;
      for (let i = 0; i < key.length; i++) {
        hash = (hash * 31 + key.charCodeAt(i)) >>> 0;
      }
      color = schemeTableau10[hash % schemeTableau10.length];
      this.cache.set(key, color);
    }
    return color;
  }
}

export function colorForTreemapNode(data: TreemapNode, extensionColors: ExtensionColorScale): string {
  if (data.isDirectory) return DIRECTORY_COLOR;
  if (data.isOther) return OTHER_COLOR;
  return extensionColors.colorFor(data.extension);
}
