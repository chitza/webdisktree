import { FileEntry } from './directory-node.model';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type FileEntryPage = PagedResult<FileEntry>;

export interface TypeBreakdownEntry {
  extension: string;
  totalSizeBytes: number;
  fileCount: number;
}

export interface DeleteFailure {
  path: string;
  reason: string;
}

export interface DeleteFilesResult {
  deleted: string[];
  failed: DeleteFailure[];
}
