export interface Favorite {
  id: string;
  path: string;
  label: string;
  createdAt: string;
}

export interface CreateFavoriteRequest {
  path: string;
  label: string | null;
}
