export enum ScanTrigger {
  Manual = 0,
  Scheduled = 1,
  Imported = 2,
}

export enum ScanStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4,
}

export interface ScanSummary {
  id: string;
  rootPath: string;
  trigger: ScanTrigger;
  status: ScanStatus;
  startedAt: string | null;
  completedAt: string | null;
  totalBytes: number;
  totalFiles: number;
  totalDirs: number;
  errorCount: number;
  isStale: boolean;
  isPinned: boolean;
  errorMessage: string | null;
}

export interface ScanRoot {
  path: string;
  label: string;
  kind: 'mount' | 'favorite';
  allowDelete: boolean;
}

/** True if `path` is `root` or below it, compared on whole path segments. */
export function isSameOrUnder(root: string, path: string): boolean {
  if (path === root) return true;
  const prefix = root.endsWith('/') ? root : root + '/';
  return path.startsWith(prefix);
}

/**
 * Splits `roots` into mounts and favorites, dropping any mount whose `path` exactly matches a
 * favorite's `path` — the favorite's own entry is what remains selectable for that path.
 */
export function groupRoots(roots: ScanRoot[]): { mounts: ScanRoot[]; favorites: ScanRoot[] } {
  const favorites = roots.filter((r) => r.kind === 'favorite');
  const favoritePaths = new Set(favorites.map((r) => r.path));
  const mounts = roots.filter((r) => r.kind === 'mount' && !favoritePaths.has(r.path));
  return { mounts, favorites };
}
