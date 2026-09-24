# WebDiskTree

A self-hosted, web-based disk usage visualizer — like WizTree or TreeMap-Disk-Visualizer, but with a browser UI instead of a desktop app. ASP.NET Core (.NET 10) backend, Angular 22 frontend, packaged as a single Docker image.

## Features

- Full recursive directory scan with live progress (SignalR)
- Interactive treemap (drill-down, hover tooltips, color-by-file-type)
- Sortable file/folder list view
- File-type size breakdown
- Delete files/folders from the UI (with path-safety validation)
- Scan history — revisit past scans, not just the latest
- Export completed scans as tar.gz archives and import them as read-only snapshots
- Scheduled scans (cron expressions) for specific paths
- Drives and mounts detected automatically, plus favorite paths you manage in the app

## Repo layout

```
backend/   ASP.NET Core solution (WebDiskTree.Api / Core / Infrastructure, plus tests)
frontend/  Angular app
Dockerfile
docker-compose.yml
```

## What can be scanned and deleted

The app only works under one directory, the **host root** (`HostRoot:Path`): `/hostfs` by default, `/` in development.
Scans, schedules and favorites outside it are rejected.

- **Drives and mounts.** The scan and schedule drop-downs list the disk filesystems mounted under the host root
  (ext4, xfs, btrfs, zfs, ntfs, exfat, vfat, nfs, cifs, ...), read from `/proc/self/mountinfo` on every request,
  so a newly attached drive shows up without a restart. `/boot`, `/snap`, `/var/lib/docker`, `/run`, `/proc` and `/sys`
  are skipped. Each entry is labelled with its path on the host (`/media/you/WD Black`).
- **Favorites.** Any directory under the host root can be added on the **Favorites** page, with an optional label.
  Favorites appear in the same drop-downs, and are stored in the database.
- **Delete.** Deleting is allowed on any **read-write** mount under the host root, and refused on read-only mounts.
  With `-v /:/hostfs:ro`, nothing is deletable until you add a read-write mount over a subpath (see below).
  **Anything you mount `:rw` under `/hostfs` can be deleted from the UI, and the app has no login.**
  The other delete checks still apply: no `..` segments, never the scan root itself, only inside the scanned root, symlinks resolved.

## Running with Docker

```bash
docker build -t webdisktree .
docker run -d --name webdisktree -p 8080:8080 \
  -v webdisktree-data:/data \
  -v /:/hostfs:ro \
  webdisktree
```

- `/hostfs` (read-only) is where the host filesystem is mounted so the app can measure real disk usage. Mount only what you're comfortable exposing.
- To allow deleting under a specific path, add an additional **read-write** mount over that subpath (e.g. `-v /home/you/Downloads:/hostfs/home/you/Downloads:rw`). The host path must already exist.
- `/data` persists the SQLite database and gzip-compressed scan tree blobs across restarts.

Or with `docker-compose.yml` (edit the volume mounts for your setup first):

```bash
docker compose up --build
```

Then open http://localhost:8080.

## Local development

Run the backend and Angular dev server separately; the dev server proxies `/api` and `/hubs` to the backend (see `frontend/proxy.conf.json`), so both are effectively same-origin and no CORS setup is needed.

```bash
# Terminal 1 — backend (applies EF Core migrations automatically on startup)
cd backend
dotnet run --project src/WebDiskTree.Api

# Terminal 2 — frontend
cd frontend
npm install
npm start
```

Open http://localhost:4200. In development, `appsettings.Development.json` points `Storage:DataDirectory` at `./data` (relative to the API project) and sets `HostRoot:Path` to `/`, so your machine's own mounts are listed and read-write mounts allow delete. On macOS, every drive counts as read-write.

### Backend tests

```bash
cd backend
dotnet test
```

### Exporting and importing scans

In **Scan history**, use the download action on a completed scan to export a
WebDiskTree `.tar.gz` archive containing `scan.json`. Download names include the
sanitized scan path and original scan date in UTC, for example
`webdisktree-hostfs-home-2026-09-06_06-24-07Z.tar.gz`.
Use **Import scan** to restore an export (up to 100 MiB compressed and 1 GiB for
`scan.json`). Legacy JSON exports can also be imported.
Imports receive a new ID and appear in history with an Imported trigger; the
original scan dates, totals, tree, full file listings, and stale status are preserved.
Imported scans are read-only and do not require the original filesystem to be mounted.
The file contains paths and metadata, not file contents. Only WebDiskTree version 1
exports are supported.
