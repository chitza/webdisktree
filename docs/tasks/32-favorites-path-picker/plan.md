# Plan: Favorites path picker, and favorite labels in the New Scan/Schedules drop-downs
Intent: [intent.md](intent.md). Status: accepted.
Branch: feature/32-favorites-path-picker

## Design

- **Shared merge helper.** New `groupRoots(roots: ScanRoot[]): { mounts: ScanRoot[]; favorites: ScanRoot[] }`
  in `core/models/scan.model.ts`. It splits `roots` by `kind`, then drops any mount whose `path` exactly
  equals a favorite's `path` (a `Set` of favorite paths, filtered against the mount list). The favorite
  entry (already rendered `{{ label }} ({{ path }})`) is what remains selectable for that path, so no
  separate "relabel the mount" step is needed — the mount option is just removed. `scan-start.ts` and
  `schedules.ts` both replace their own `roots().filter(r => r.kind === 'mount' | 'favorite')` computeds
  with one `private readonly grouped = computed(() => groupRoots(this.roots()))` plus
  `mounts = computed(() => this.grouped().mounts)` / `favorites = computed(() => this.grouped().favorites)`.
  No template change is needed on `scan-start.html` for this part — it already reads `mounts()`/`favorites()`.
- **`scan-start.ts` auto-select fix.** The constructor currently auto-selects when
  `roots.length === 1` (the raw, un-grouped list). After the merge, a favorited mount's own path produces
  *two* raw roots (the mount and the favorite) that collapse to *one* visible option, so the raw-length
  check must change to the grouped/effective count: `const { mounts, favorites } = groupRoots(roots);
  const only = [...mounts, ...favorites]; if (only.length === 1) this.selectedPath.set(only[0].path);`.
- **`schedules.html` favorite label format.** Today it renders favorite options as `{{ root.label }}` only
  (no path), unlike `scan-start.html`'s `{{ root.label }} ({{ root.path }})`. Intent decision 4 asks for
  the identical fix on both pages; since the merge relies on the favorite entry already showing
  `Label (Path)`, `schedules.html`'s favorite `mat-option` must gain the same `({{ root.path }})` suffix,
  or a merged mount would disappear into a schedule option that shows only its label, losing the
  "Label (Path)" result the intent asks for. This is a visible change beyond pure New Scan scope; call it
  out under "Not done"/PR notes as flowing from decision 4, not asked for standalone.
- **Favorites path picker.** `favorites.ts` gains a `roots` signal populated the same way as
  `scan-start.ts`/`schedules.ts` (`scanService.getRoots()` in the constructor) and
  `mounts = computed(() => this.roots().filter(r => r.kind === 'mount'))` — mounts only, no merge, no
  "Favorites" group (intent decision 2). Component state: `readonly CUSTOM_PATH = '__custom__';`,
  `selectedPathOption = this.CUSTOM_PATH;` (default, so the page behaves exactly as today — a free-text
  field — before the mount list loads or when there are no mounts), `readonly showCustomPath =
  signal(true);`. Method `onPathOptionChange(value: string)`: if `value === this.CUSTOM_PATH`, set
  `showCustomPath.set(true)` and `this.newPath = ''`; otherwise set `showCustomPath.set(false)` and
  `this.newPath = value` (the chosen mount's path). Template: a `mat-select [(ngModel)]="selectedPathOption"
  (selectionChange)="onPathOptionChange($event.value)"` labeled "Path", with a "Drives and mounts"
  `mat-optgroup` (mirroring `scan-start.html`'s markup, one option per mount showing `root.label`) followed
  by a plain `mat-option [value]="CUSTOM_PATH"` reading "Custom path…". When `showCustomPath()` is true, the
  existing free-text `mat-form-field` (labeled "Custom path" instead of "Path", to avoid two fields both
  called "Path") renders below it, unchanged apart from the label. `add()`, its validation, and the Label
  field are untouched — a picked mount's default label already matches what the backend assigns when the
  label is left blank (`FavoritesController` defaults to `hostRoot.ToHostPath(path)`, the same string
  `MountDetectionService` uses as the mount's own `label`), so no client-side label prefill is needed.
- No backend change. `GET /api/roots` (`RootsController.cs`) already returns `path`, `label`, `kind`,
  `allowDelete` for both mounts and favorites; nothing here needs a new field or a new endpoint.

## Files that change
- `frontend/src/app/core/models/scan.model.ts` (modified): add `groupRoots()`.
- `frontend/src/app/core/models/scan.model.spec.ts` (new): tests for `groupRoots()`.
- `frontend/src/app/features/scan-start/scan-start.ts` (modified): use `groupRoots()`; fix the single-root
  auto-select to count the grouped/effective list, not the raw `roots` array.
- `frontend/src/app/features/scan-start/scan-start.spec.ts` (new): tests below.
- `frontend/src/app/features/schedules/schedules.ts` (modified): use `groupRoots()`.
- `frontend/src/app/features/schedules/schedules.html` (modified): favorite `mat-option` gains
  `({{ root.path }})`, matching `scan-start.html`.
- `frontend/src/app/features/schedules/schedules.spec.ts` (new): tests below.
- `frontend/src/app/features/favorites/favorites.ts` (modified): inject `ScanService`; add `roots`,
  `mounts`, `selectedPathOption`, `showCustomPath`, `CUSTOM_PATH`, `onPathOptionChange()`.
- `frontend/src/app/features/favorites/favorites.html` (modified): path `mat-select` (mounts +
  "Custom path…") plus the conditional free-text field.
- `frontend/src/app/features/favorites/favorites.spec.ts` (modified): update `setup()` to provide a
  `ScanService` mock; add the picker tests below.
- `frontend/src/app/features/favorites/favorites.scss` (modified, only if the picker's two stacked fields
  need spacing beyond the existing `.form-row` flex layout; otherwise left alone).

## Order of work
1. Shared merge helper and its tests. Files: `scan.model.ts`, `scan.model.spec.ts`.
2. New Scan: use `groupRoots()`, fix single-root auto-select, add its spec. Files: `scan-start.ts`,
   `scan-start.spec.ts`.
3. Schedules: use `groupRoots()`, add the `({{ root.path }})` suffix to the favorite option, add its spec.
   Files: `schedules.ts`, `schedules.html`, `schedules.spec.ts`.
4. Favorites path picker: component state, template, and spec updates. Files: `favorites.ts`,
   `favorites.html`, `favorites.spec.ts`, `favorites.scss` (if needed).

## Pinned tests
None.

## Risks
- **Riskiest: step 2's auto-select fix.** If the grouped/effective count is not used, a user with a single
  favorited mount (now two raw roots, one visible option) would see New Scan load with nothing
  auto-selected, a silent regression from today's single-root behavior. The new spec must assert
  auto-select still fires in that exact case (one mount whose path is also a favorite, `roots.length`
  raw = 2, effective = 1).
- Step 3's `schedules.html` label-format change is visible beyond pure "New Scan" scope; it follows from
  intent decision 4 ("identical fix") and is called out in the PR description, not hidden as a side effect.
- Favorites' new mounts-only dropdown does not stop a user from typing (via "Custom path…") a path that is
  already a favorite; the existing 409 handling in `add()`'s error callback is unchanged and still shows it.
- No backend/API/Security/migration files touched, so no data-safety or delete-path risk from this change.

## Needs approval
None. No change to `Security/`, no EF migration, no Dockerfile/compose/CI/`appsettings*.json` change, no
API route or DTO removed or renamed, no new package.

## Proof
- Checks: frontend only (`cd frontend && npm ci --no-audit --no-fund && npx ng build && npx ng test --watch=false`).
- Migration check: not needed — no file under `backend/src/WebDiskTree.Infrastructure/Data/` changes.
- Smoke run: not needed — no backend or API change; `GET /api/roots`'s shape is unchanged, and CI's
  "Docker build" check already covers that the image still builds with the frontend changes.
- New tests:
  - `scan.model.spec.ts`: `groupRoots` keeps every mount and favorite when no paths overlap; drops a mount
    whose `path` exactly equals a favorite's `path`, leaving only the favorite entry; keeps a mount whose
    path is merely a prefix of a favorite's path (not an exact match, e.g. mount `/hostfs/data` vs. favorite
    `/hostfs/data/movies`).
  - `scan-start.spec.ts`: renders mounts under "Drives and mounts" (label only) and favorites as
    `label (path)` under "Favorites"; a mount whose path matches a favorite's path does not appear under
    "Drives and mounts" (only its favorite entry does); auto-selects the sole path when the merged list has
    exactly one entry even though the raw roots array from the service has two (a mount and a favorite at
    the same path).
  - `schedules.spec.ts`: renders mounts under "Drives and mounts" (label only) and favorites as
    `label (path)` under "Favorites"; a mount whose path matches a favorite's path is excluded from
    "Drives and mounts".
  - `favorites.spec.ts`: existing four tests still pass with a mocked `ScanService.getRoots` added to
    `setup()`; the path `mat-select` lists detected mounts under "Drives and mounts" plus "Custom path…";
    selecting a mount option sets the value `add()` sends and hides the free-text field; selecting
    "Custom path…" shows the free-text field again and `add()` sends whatever is typed into it (unchanged
    behavior); with no mounts returned by `getRoots()`, the free-text field is shown from the start.
- Target: `npx ng build` and `npx ng test --watch=false` both exit 0 with the new spec files included, and
  `git diff --name-only origin/main...HEAD` touches only files under `frontend/`.
