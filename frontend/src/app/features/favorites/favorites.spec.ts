import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Favorite } from '../../core/models/favorite.model';
import { FavoriteService } from '../../core/services/favorite.service';
import { ScanService } from '../../core/services/scan.service';
import { ScanRoot } from '../../core/models/scan.model';
import { Favorites } from './favorites';

describe('Favorites', () => {
  const favorites: Favorite[] = [
    { id: 'f1', path: '/hostfs/media/cristi/WD Black', label: 'WD Black', createdAt: '2026-09-24T10:00:00Z' },
    { id: 'f2', path: '/hostfs/home/cristi/Downloads', label: 'Downloads', createdAt: '2026-09-24T10:00:00Z' },
  ];
  const mounts: ScanRoot[] = [
    { path: '/hostfs/media/cristi/WD Black', label: 'WD Black', kind: 'mount', allowDelete: false },
  ];

  async function setup(roots: ScanRoot[] = mounts) {
    const service = {
      getFavorites: vi.fn(() => of(favorites)),
      createFavorite: vi.fn(() => of(favorites[0])),
      renameFavorite: vi.fn(() => of(favorites[0])),
      deleteFavorite: vi.fn(() => of(undefined)),
    };
    const scanService = { getRoots: vi.fn(() => of(roots)) };
    await TestBed.configureTestingModule({
      imports: [Favorites],
      providers: [
        { provide: FavoriteService, useValue: service },
        { provide: ScanService, useValue: scanService },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Favorites);
    fixture.detectChanges();
    return { fixture, service };
  }

  function openPanel(fixture: { nativeElement: HTMLElement; detectChanges: () => void }) {
    (fixture.nativeElement.querySelector('.mat-mdc-select-trigger') as HTMLElement).click();
    fixture.detectChanges();
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

  it('lists detected mounts under "Drives and mounts" plus "Custom path…" in the path picker', async () => {
    const { fixture } = await setup();
    openPanel(fixture);

    const groupLabels = Array.from(document.querySelectorAll('.mat-mdc-optgroup-label')).map((el) =>
      el.textContent?.trim(),
    );
    expect(groupLabels).toEqual(['Drives and mounts']);

    const options = Array.from(document.querySelectorAll('.mat-mdc-option')).map((el) => el.textContent?.trim());
    expect(options).toEqual(['WD Black', 'Custom path…']);
  });

  it('selecting a mount sets the path add() sends and hides the free-text field', async () => {
    const { fixture, service } = await setup();
    const component = fixture.componentInstance;

    component.onPathOptionChange('/hostfs/media/cristi/WD Black');
    fixture.detectChanges();

    expect(component.showCustomPath()).toBe(false);
    expect(fixture.nativeElement.querySelector('input[name="path"]')).toBeNull();
    expect(component.newPath).toBe('/hostfs/media/cristi/WD Black');

    component.add();
    expect(service.createFavorite).toHaveBeenCalledWith({
      path: '/hostfs/media/cristi/WD Black',
      label: null,
    });
  });

  it('selecting "Custom path…" shows the free-text field again and add() sends what is typed', async () => {
    const { fixture, service } = await setup();
    const component = fixture.componentInstance;

    component.onPathOptionChange('/hostfs/media/cristi/WD Black');
    fixture.detectChanges();
    component.onPathOptionChange(component.CUSTOM_PATH);
    fixture.detectChanges();

    expect(component.showCustomPath()).toBe(true);
    expect(fixture.nativeElement.querySelector('input[name="path"]')).not.toBeNull();

    component.newPath = '/hostfs/data';
    component.add();
    expect(service.createFavorite).toHaveBeenCalledWith({ path: '/hostfs/data', label: null });
  });

  it('shows the free-text field from the start when there are no mounts', async () => {
    const { fixture } = await setup([]);

    expect(fixture.componentInstance.showCustomPath()).toBe(true);
    expect(fixture.nativeElement.querySelector('input[name="path"]')).not.toBeNull();
  });
});
