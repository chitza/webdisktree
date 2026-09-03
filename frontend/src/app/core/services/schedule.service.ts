import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateScheduleRequest, Schedule } from '../models/schedule.model';

@Injectable({ providedIn: 'root' })
export class ScheduleService {
  private readonly http = inject(HttpClient);

  getSchedules(): Observable<Schedule[]> {
    return this.http.get<Schedule[]>('/api/schedules');
  }

  createSchedule(request: CreateScheduleRequest): Observable<Schedule> {
    return this.http.post<Schedule>('/api/schedules', request);
  }

  updateSchedule(id: string, request: CreateScheduleRequest): Observable<Schedule> {
    return this.http.put<Schedule>(`/api/schedules/${id}`, request);
  }

  deleteSchedule(id: string): Observable<void> {
    return this.http.delete<void>(`/api/schedules/${id}`);
  }

  runNow(id: string): Observable<{ scanId: string }> {
    return this.http.post<{ scanId: string }>(`/api/schedules/${id}/run-now`, {});
  }
}
