import { groupRoots, ScanRoot } from './scan.model';

describe('groupRoots', () => {
  it('keeps every mount and favorite when no paths overlap', () => {
    const roots: ScanRoot[] = [
      { path: '/hostfs/media', label: 'Media', kind: 'mount', allowDelete: false },
      { path: '/hostfs/data/movies', label: 'Movies', kind: 'favorite', allowDelete: true },
    ];

    const { mounts, favorites } = groupRoots(roots);

    expect(mounts).toEqual([roots[0]]);
    expect(favorites).toEqual([roots[1]]);
  });

  it("drops a mount whose path exactly equals a favorite's path", () => {
    const mount: ScanRoot = { path: '/hostfs/media', label: 'Media', kind: 'mount', allowDelete: false };
    const favorite: ScanRoot = { path: '/hostfs/media', label: 'My Media', kind: 'favorite', allowDelete: true };

    const { mounts, favorites } = groupRoots([mount, favorite]);

    expect(mounts).toEqual([]);
    expect(favorites).toEqual([favorite]);
  });

  it('keeps a mount whose path is merely a prefix of a favorite path', () => {
    const mount: ScanRoot = { path: '/hostfs/data', label: 'Data', kind: 'mount', allowDelete: false };
    const favorite: ScanRoot = { path: '/hostfs/data/movies', label: 'Movies', kind: 'favorite', allowDelete: true };

    const { mounts, favorites } = groupRoots([mount, favorite]);

    expect(mounts).toEqual([mount]);
    expect(favorites).toEqual([favorite]);
  });
});
