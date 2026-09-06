import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ImdbLookupCacheImportResult, ImdbLookupCacheSummary } from '../models/imdb-cache.model';

@Injectable({ providedIn: 'root' })
export class ImdbLookupCacheService {
  private readonly http = inject(HttpClient);

  getSummary(): Observable<ImdbLookupCacheSummary> {
    return this.http.get<ImdbLookupCacheSummary>('/api/imdb-lookup-cache');
  }

  importCache(file: File): Observable<ImdbLookupCacheImportResult> {
    return this.http.post<ImdbLookupCacheImportResult>('/api/imdb-lookup-cache/import', file, {
      headers: { 'Content-Type': file.name.toLowerCase().endsWith('.json') ? 'application/json' : 'application/gzip' },
    });
  }
}
