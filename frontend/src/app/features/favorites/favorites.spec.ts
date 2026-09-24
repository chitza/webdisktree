import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Favorite } from '../../core/models/favorite.model';
import { FavoriteService } from '../../core/services/favorite.service';
import { Favorites } from './favorites';

describe('Favorites', () => {
  const favorites: Favorite[] = [
    { id: 'f1', path: '/hostfs/media/cristi/WD Black', label: 'WD Black', createdAt: '2026-09-24T10:00:00Z' },
    { id: 'f2', path: '/hostfs/home/cristi/Downloads', label: 'Downloads', createdAt: '2026-09-24T10:00:00Z' },
  ];

  async function setup() {
    const service = {
      getFavorites: vi.fn(() => of(favorites)),
      createFavorite: vi.fn(() => of(favorites[0])),
      renameFavorite: vi.fn(() => of(favorites[0])),
      deleteFavorite: vi.fn(() => of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [Favorites],
      providers: [{ provide: FavoriteService, useValue: service }],
    }).compileComponents();
    const fixture = TestBed.createComponent(Favorites);
    fixture.detectChanges();
    return { fixture, service };
  }

  it('lists the favorites from the service', async () => {
    const { fixture } = await setup();

    const rows: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('tr.mat-mdc-row'));
    expect(rows.map((r) => r.textContent)).toEqual([
      expect.stringContaining('/hostfs/media/cristi/WD Black'),
      expect.stringContaining('/hostfs/home/cristi/Downloads'),
    ]);
    expect(rows[0].textContent).toContain('WD Black');
  });

  it('adds a favorite with the trimmed path and label, then reloads', async () => {
    const { fixture, service } = await setup();
    const component = fixture.componentInstance;

    component.newPath = '  /hostfs/data  ';
    component.newLabel = '  Data  ';
    component.add();

    expect(service.createFavorite).toHaveBeenCalledWith({ path: '/hostfs/data', label: 'Data' });
    expect(service.getFavorites).toHaveBeenCalledTimes(2);
    expect(component.newPath).toBe('');
  });

  it('sends a null label when none is given', async () => {
    const { fixture, service } = await setup();
    const component = fixture.componentInstance;

    component.newPath = '/hostfs/data';
    component.add();

    expect(service.createFavorite).toHaveBeenCalledWith({ path: '/hostfs/data', label: null });
  });

  it('renames and deletes through the service', async () => {
    const { fixture, service } = await setup();
    const component = fixture.componentInstance;

    component.startEdit(favorites[0]);
    component.editLabel = ' Movies ';
    component.saveEdit(favorites[0]);
    component.remove(favorites[1]);

    expect(service.renameFavorite).toHaveBeenCalledWith('f1', 'Movies');
    expect(component.editingId()).toBeNull();
    expect(service.deleteFavorite).toHaveBeenCalledWith('f2');
  });
});
