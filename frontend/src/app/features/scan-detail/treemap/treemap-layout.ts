import { DirectoryNode, FileEntry } from '../../../core/models/directory-node.model';

/** A plain node fed to d3.hierarchy — `ref` points back to the real DirectoryNode for drill-down,
 * null for files/the synthetic "other files" leaf which can't be drilled into further. */
export interface TreemapNode {
  name: string;
  path: string;
  size: number;
  isDirectory: boolean;
  isOther: boolean;
  extension: string | null;
  ref: DirectoryNode | null;
  children?: TreemapNode[];
}

/** Maps a real DirectoryNode subtree into the plain shape d3.hierarchy needs, stopping recursion at
 * maxDepth so a treemap always shows a fixed, legible number of nested levels — deeper content is
 * still represented (as a single leaf sized by its true rolled-up SizeBytes) and still drillable via `ref`. */
export function buildTreemapNode(dir: DirectoryNode, maxDepth: number, depth = 0): TreemapNode {
  const base: TreemapNode = {
    name: dir.name,
    path: dir.fullPath,
    size: dir.sizeBytes,
    isDirectory: true,
    isOther: false,
    extension: null,
    ref: dir,
  };

  if (depth >= maxDepth) {
    return base;
  }

  const children: TreemapNode[] = [
    ...dir.directories.map((child) => buildTreemapNode(child, maxDepth, depth + 1)),
    ...dir.files.map((file) => fileToTreemapNode(dir.fullPath, file)),
  ];

  if (dir.otherFilesCount > 0) {
    children.push({
      name: `${dir.otherFilesCount} more files`,
      path: `${dir.fullPath}/*`,
      size: dir.otherFilesSizeBytes,
      isDirectory: false,
      isOther: true,
      extension: null,
      ref: null,
    });
  }

  if (children.length > 0) {
    base.children = children;
  }

  return base;
}

function fileToTreemapNode(parentPath: string, file: FileEntry): TreemapNode {
  return {
    name: file.name,
    path: `${parentPath}/${file.name}`,
    size: file.sizeBytes,
    isDirectory: false,
    isOther: false,
    extension: file.extension,
    ref: null,
  };
}
