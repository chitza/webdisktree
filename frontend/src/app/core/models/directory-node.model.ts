export interface FileEntry {
  name: string;
  extension: string | null;
  sizeBytes: number;
  modifiedUtc: string;
  isDirectory: boolean;
  isSymlink?: boolean;
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
