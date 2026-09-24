import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateFavoriteRequest, Favorite } from '../models/favorite.model';

@Injectable({ providedIn: 'root' })
export class FavoriteService {
  private readonly http = inject(HttpClient);

  getFavorites(): Observable<Favorite[]> {
    return this.http.get<Favorite[]>('/api/favorites');
  }

  createFavorite(request: CreateFavoriteRequest): Observable<Favorite> {
    return this.http.post<Favorite>('/api/favorites', request);
  }

  renameFavorite(id: string, label: string): Observable<Favorite> {
    return this.http.put<Favorite>(`/api/favorites/${id}`, { label });
  }

  deleteFavorite(id: string): Observable<void> {
    return this.http.delete<void>(`/api/favorites/${id}`);
  }
}
