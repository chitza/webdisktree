import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DirectoryNode } from '../models/directory-node.model';
import { DeleteFilesResult, FileEntryPage, TypeBreakdownEntry } from '../models/file-entry.model';

export interface FileListQuery {
  path: string;
  sort?: 'name' | 'size' | 'extension' | 'modified';
  dir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class FileService {
  private readonly http = inject(HttpClient);

  getTree(scanId: string): Observable<DirectoryNode> {
    return this.http.get<DirectoryNode>(`/api/scans/${scanId}/tree`);
  }

  getFiles(scanId: string, query: FileListQuery): Observable<FileEntryPage> {
    const params: Record<string, string> = { path: query.path };
    if (query.sort) params['sort'] = query.sort;
    if (query.dir) params['dir'] = query.dir;
    if (query.page) params['page'] = String(query.page);
    if (query.pageSize) params['pageSize'] = String(query.pageSize);

    return this.http.get<FileEntryPage>(`/api/scans/${scanId}/files`, { params });
  }

  getBreakdown(scanId: string): Observable<TypeBreakdownEntry[]> {
    return this.http.get<TypeBreakdownEntry[]>(`/api/scans/${scanId}/breakdown`);
  }

  deleteFiles(scanId: string, paths: string[]): Observable<DeleteFilesResult> {
    return this.http.post<DeleteFilesResult>('/api/files/delete', { scanId, paths });
  }

  triggerImdbLookup(scanId: string, path: string): Observable<{ queued: number; alreadyCached: number }> {
    return this.http.post<{ queued: number; alreadyCached: number }>(
      `/api/scans/${scanId}/imdb-lookup`,
      { path },
    );
  }
}
