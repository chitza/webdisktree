import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Favorite } from '../../core/models/favorite.model';
import { FavoriteService } from '../../core/services/favorite.service';
import { ScanService } from '../../core/services/scan.service';
import { ScanRoot } from '../../core/models/scan.model';

@Component({
  selector: 'app-favorites',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
  ],
  templateUrl: './favorites.html',
  styleUrl: './favorites.scss',
})
export class Favorites {
  private readonly favoriteService = inject(FavoriteService);
  private readonly scanService = inject(ScanService);

  readonly CUSTOM_PATH = '__custom__';

  readonly favorites = signal<Favorite[]>([]);
  readonly displayedColumns = ['label', 'path', 'actions'];
  readonly error = signal<string | null>(null);
  readonly saving = signal(false);
  readonly editingId = signal<string | null>(null);

  readonly roots = signal<ScanRoot[]>([]);
  readonly mounts = computed(() => this.roots().filter((r) => r.kind === 'mount'));
  selectedPathOption = this.CUSTOM_PATH;
  readonly showCustomPath = signal(true);

  newPath = '';
  newLabel = '';
  editLabel = '';

  constructor() {
    this.load();
    this.scanService.getRoots().subscribe((roots) => this.roots.set(roots));
  }

  onPathOptionChange(value: string): void {
    if (value === this.CUSTOM_PATH) {
      this.showCustomPath.set(true);
      this.newPath = '';
    } else {
      this.showCustomPath.set(false);
      this.newPath = value;
    }
  }

  load(): void {
    this.favoriteService.getFavorites().subscribe((favorites) => this.favorites.set(favorites));
  }

  add(): void {
    const path = this.newPath.trim();
    if (!path) return;

    this.saving.set(true);
    this.error.set(null);
    this.favoriteService.createFavorite({ path, label: this.newLabel.trim() || null }).subscribe({
      next: () => {
        this.saving.set(false);
        this.newPath = '';
        this.newLabel = '';
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error ?? 'Failed to add favorite.');
      },
    });
  }

  startEdit(favorite: Favorite): void {
    this.editingId.set(favorite.id);
    this.editLabel = favorite.label;
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveEdit(favorite: Favorite): void {
    const label = this.editLabel.trim();
    if (!label) return;

    this.error.set(null);
    this.favoriteService.renameFavorite(favorite.id, label).subscribe({
      next: () => {
        this.editingId.set(null);
        this.load();
      },
      error: (err) => this.error.set(err?.error ?? 'Failed to rename favorite.'),
    });
  }

  remove(favorite: Favorite): void {
    this.error.set(null);
    this.favoriteService.deleteFavorite(favorite.id).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(err?.error ?? 'Failed to delete favorite.'),
    });
  }
}
