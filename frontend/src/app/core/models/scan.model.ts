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

export interface AllowedRoot {
  path: string;
  label: string;
  allowDelete: boolean;
}
