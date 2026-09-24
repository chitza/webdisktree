# Intent: Favorites path picker, and favorite labels in the New Scan drop-down
Author: chitza. Issue: follow-up for PR #32 (no issue number). Status: accepted.

## Problem
Today, on the Favorites page, a user adding a favorite must type the full path by hand
(`frontend/src/app/features/favorites/favorites.html`: a plain `matInput` free-text field).
There is no way to pick a path from the same drives/mounts the app already knows about.

On the New Scan page, the "Root to scan" drop-down (`scan-start.html`) lists detected mounts under
"Drives and mounts" (showing only the mount's own label) and favorites under "Favorites" (showing
`label (path)`). If a user has favorited a path that is also a detected mount's own path (or any
mount), the mount entry still shows only its plain mount label, not the friendlier name the user
gave it as a favorite.

## Proposed outcome
- The Favorites "add favorite" form lets the user pick a path from a drop-down of known roots
  (the same source `GET /api/roots` already provides to New Scan: detected mounts + existing
  favorites), **and** still lets them type any path by hand, exactly as today.
- On the New Scan page's "Root to scan" drop-down, a mount option whose path matches an existing
  favorite's path displays as `Label (Path)` using that favorite's label, instead of the mount's
  own plain label.
- No backend change: `GET /api/roots` already returns everything both pages need
  (`path`, `label`, `kind`, `allowDelete`). This is a frontend-only task.

## Affected parts
- Frontend only: `features/favorites/*`, `features/scan-start/*`.
- No backend API, no EF migration, no `core/services/*.ts` change expected (both pages already
  call `ScanService.getRoots()` / `FavoriteService`; `scan.model.ts`'s `ScanRoot` already has
  `path`, `label`, `kind`).
- Out of scope: `features/schedules/*`'s root drop-down (same `mat-optgroup` pattern as New Scan,
  not mentioned in the request) — flagged as an open question below, not assumed in scope.

## Constraints
- Existing favorites validation is unchanged: `POST /api/favorites` still rejects a path outside
  the host root (400), a non-existent directory (400), and a duplicate path (409). Picking a
  path from the new drop-down does not bypass any of this — it only pre-fills the free-text path.
- Existing New Scan behavior for a single-root case (auto-select when `roots().length === 1`)
  and the `mounts()`/`favorites()` computed split stay working.
- No change to how mount `AllowDelete`/detection logic works; this task only changes what text
  is displayed for an option and how a path gets into the favorites form's path field.

## Decisions
1. **Favorites path-entry UX: drop-down + "Custom path…" option.** The Favorites path field
   becomes a `mat-select` (mirroring New Scan's control), with an extra `mat-option` "Custom
   path…" at the top. Choosing "Custom path…" reveals the existing free-text `matInput`, unchanged
   from today, so a user can still type any path by hand.
2. **Favorites picker source: mounts only.** The drop-down lists only detected mounts (no
   "Favorites" optgroup) — offering an already-favorited path as something to pick would be
   redundant on an *add favorite* form and would only hit the existing 409 duplicate-path error.
3. **New Scan / Schedules: merge on exact path match.** When a favorite's path exactly equals a
   mount's path, that mount is dropped from "Drives and mounts"; only the favorite's entry (now
   showing `Label (Path)`) remains selectable. No two options share the same underlying path.
4. **Schedules is in scope.** `features/schedules/*` uses the same `getRoots()` +
   `mat-optgroup` drop-down as New Scan and gets the identical fix (relabel + merge), so the two
   root pickers keep behaving the same way.
