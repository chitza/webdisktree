export enum ImdbLookupStatus {
  Pending = 0,
  Found = 1,
  NotFound = 2,
  Failed = 3,
}

export interface FileEntry {
  name: string;
  extension: string | null;
  sizeBytes: number;
  modifiedUtc: string;
  isDirectory: boolean;
  isSymlink?: boolean;
  parsedTitle?: string | null;
  imdbUrl?: string | null;
  imdbStatus?: ImdbLookupStatus | null;
}

export interface DirectoryNode {
  name: string;
  fullPath: string;
  sizeBytes: number;
  modifiedUtc: string;
  isSymlink: boolean;
  directories: DirectoryNode[];
  files: FileEntry[];
  otherFilesCount: number;
  otherFilesSizeBytes: number;
}
