# Intent: Detect drives and mounts automatically, add favorite paths
Author: chitza. Issue: #28. Status: accepted.

## Problem
To make a path scannable, the operator must list it in the `AllowedRoots` config (env vars or `appsettings.json`)
and restart the container. A newly attached drive, or any folder not listed, cannot be scanned from the UI.
The root drop-down on "Start scan" and "Schedules" shows only these configured entries.

## Proposed outcome
- The root drop-down lists the drives and mounts that the app detects at request time
  (for example `/hostfs`, `/hostfs/media/cristi/WD Black`), with no config.
- A new settings section in the app lets the user add, rename and remove "favorite" paths.
  Favorites appear in the same drop-down as the detected mounts, and are stored in the database.
- Scans, schedules and the scan-detail page work with any path from the drop-down.
- The `AllowedRoots` config and the "No allowed roots are configured" message are gone.

## Affected parts
Backend API (`RootsController`, `ScansController`, `SchedulesController`, `FilesController` delete),
backend infrastructure (`Security/`, new mount detection, new favorites entity),
EF schema/migration (favorites table), frontend (scan-start, schedules, scan-detail, new favorites settings page),
`docker-compose.yml`, `appsettings*.json`, README.

## Constraints
- The app has no authentication. Anyone who can open the UI can call every API.
- Delete is the dangerous part. Today it needs `AllowDelete: true` on a config root, and a read-write mount.
- Detection runs inside the container. It sees the container's own mounts (`/data`, `/etc/hosts`, overlay root, `/proc` ...),
  and the host mounts only as they appear under the `/hostfs` bind mount.
- Existing scans, schedules and exported archives keep working; their stored `RootPath` values do not change.
- Local development runs on macOS and Linux without `/hostfs`.
- Out of scope: authentication, Windows hosts.

## Decisions
1. **Scan boundary.** Scans and favorites may target only paths under one configured host root:
   default `/hostfs` in Docker, `/` in development. Anything outside it is rejected, as today.
2. **Delete.** Allowed under any path whose mount is read-write. The operator controls this with `:rw` Docker mounts.
   The existing checks stay: no traversal, not the scan root itself, inside the scanned root, symlinks resolved.
3. **Which mounts to list.** Only real filesystems (ext4, xfs, btrfs, ntfs, exfat, vfat, zfs, nfs, cifs ...) under the host root.
   Skip pseudo filesystems, `/boot*`, snap loop mounts and Docker's own mounts.
4. **Existing config.** `AllowedRoots` support is removed outright. No copy into favorites.
5. **Labels.** A detected mount's label is its path with the host root prefix removed (`/media/cristi/WD Black`).
6. **Favorites UI.** A new "Favorites" page in the nav.

## Open questions
None.
