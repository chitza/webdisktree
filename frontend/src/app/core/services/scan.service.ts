import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AllowedRoot, ScanSummary } from '../models/scan.model';

@Injectable({ providedIn: 'root' })
export class ScanService {
  private readonly http = inject(HttpClient);

  getRoots(): Observable<AllowedRoot[]> {
    return this.http.get<AllowedRoot[]>('/api/roots');
  }

  getScans(): Observable<ScanSummary[]> {
    return this.http.get<ScanSummary[]>('/api/scans');
  }

  getScan(id: string): Observable<ScanSummary> {
    return this.http.get<ScanSummary>(`/api/scans/${id}`);
  }

  createScan(rootPath: string): Observable<ScanSummary> {
    return this.http.post<ScanSummary>('/api/scans', { rootPath });
  }

  cancelScan(id: string): Observable<void> {
    return this.http.post<void>(`/api/scans/${id}/cancel`, {});
  }
}
