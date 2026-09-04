import { TreemapNode } from './treemap-layout';

/** The "terminal / instrument" visualization ramp — the same ten hues the app chrome is built
 * from, so the treemap reads as part of the theme rather than a generic d3 default. Deliberately
 * identical in both themes: an extension keeps its colour when you flip light/dark, which is
 * what makes the colour learnable across sessions. */
export const EXTENSION_RAMP = [
  '#4ade80', // signal green — the accent itself
  '#38bdf8',
  '#fbbf24',
  '#f87171',
  '#a78bfa',
  '#2dd4bf',
  '#fb923c',
  '#60a5fa',
  '#f472b6',
  '#a3e635',
] as const;

/** The aggregated "N more files" leaf. Stays neutral on purpose: it is genuinely a mixed bag of
 * unknown types, so giving it a ramp colour would imply an identity it does not have. */
export const OTHER_COLOR = '#475569';

/** Stable name -> ramp hue. The hash is order-independent of the tree, so a given extension or
 * folder name keeps its colour across drill-downs, views and sessions. */
function hashToRamp(key: string): string {
  let hash = 0;
  for (let i = 0; i < key.length; i++) {
    hash = (hash * 31 + key.charCodeAt(i)) >>> 0;
  }
  return EXTENSION_RAMP[hash % EXTENSION_RAMP.length];
}

/** Relative luminance per WCAG, used only to pick a readable label ink for a given fill. */
function relativeLuminance(hex: string): number {
  const value = parseInt(hex.slice(1), 16);
  const channels = [(value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff].map((c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}

const LABEL_ON_LIGHT_FILL = '#0b0f14';
const LABEL_ON_DARK_FILL = '#e4ecf2';

function contrastRatio(a: number, b: number): number {
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

/** Picks the label ink that actually reads on a given fill. The old renderer hardcoded white,
 * which is unreadable on the bright half of this ramp (#fbbf24, #a3e635) — the ramp spans both
 * light and dark fills, so the label has to follow the fill rather than the theme.
 *
 * Compares both candidate inks rather than thresholding luminance: most of this ramp sits in the
 * mid range (#38bdf8 at L≈0.43, #a78bfa at L≈0.34) where "is the fill bright?" is the wrong
 * question — light ink there scores under 2.5:1 while dark ink scores 7:1 or better. */
export function labelColorFor(fill: string): string {
  const fillLuminance = relativeLuminance(fill);
  const onDark = contrastRatio(fillLuminance, relativeLuminance(LABEL_ON_LIGHT_FILL));
  const onLight = contrastRatio(fillLuminance, relativeLuminance(LABEL_ON_DARK_FILL));
  return onDark >= onLight ? LABEL_ON_LIGHT_FILL : LABEL_ON_DARK_FILL;
}

/** Memoized colour authority shared by every hierarchical visualization so they all agree on
 * colours for the same data.
 *
 * Files are coloured by extension and folders by their own name — both drawn from the same ramp,
 * so a folder is an identity, not a category: `node_modules` looks the same wherever it appears,
 * and sibling folders are always distinguishable from each other. The trade-off is that a folder
 * and a file type can land on the same hue; the treemap keeps them apart structurally instead,
 * since a folder renders as a labelled frame around its children rather than a solid block. */
export class HierarchyColorScale {
  private readonly extensionCache = new Map<string, string>();
  private readonly directoryCache = new Map<string, string>();

  colorFor(extension: string | null): string {
    const key = extension || '(none)';
    let color = this.extensionCache.get(key);
    if (!color) {
      color = hashToRamp(key);
      this.extensionCache.set(key, color);
    }
    return color;
  }

  colorForDirectory(name: string): string {
    // Keyed on the bare folder name rather than the full path so a folder keeps one colour
    // wherever it shows up in the tree.
    let color = this.directoryCache.get(name);
    if (!color) {
      color = hashToRamp(name);
      this.directoryCache.set(name, color);
    }
    return color;
  }
}

export function colorForTreemapNode(data: TreemapNode, colors: HierarchyColorScale): string {
  if (data.isOther) return OTHER_COLOR;
  if (data.isDirectory) return colors.colorForDirectory(data.name);
  return colors.colorFor(data.extension);
}

/** Which side of the theme is currently live. Kept as a local union rather than importing
 * ThemeService's type so this stays a dependency-free colour module. */
export type VizTheme = 'light' | 'dark';

/** Colours the visualizations need that come from the theme rather than the data. Canvas and
 * SVG can't read CSS custom properties, so these have to be resolved in TS. */
export interface VizChrome {
  /** Hairline between adjacent rectangles/arcs. */
  separator: string;
  /** Outline on the hovered rectangle/arc. */
  hoverStroke: string;
}

export function vizChromeFor(theme: VizTheme): VizChrome {
  return theme === 'dark'
    ? // Tinted toward the ground so rectangles read as inset into the panel rather than
      // outlined on top of it.
      { separator: 'rgba(11, 15, 20, 0.45)', hoverStroke: '#4ade80' }
    : { separator: 'rgba(244, 246, 245, 0.55)', hoverStroke: '#1a7f4b' };
}
