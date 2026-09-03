# WebDiskTree

A self-hosted, web-based disk usage visualizer — like WizTree or TreeMap-Disk-Visualizer, but with a browser UI instead of a desktop app. ASP.NET Core (.NET 10) backend, Angular 22 frontend, packaged as a single Docker image.

## Features

- Full recursive directory scan with live progress (SignalR)
- Interactive treemap (drill-down, hover tooltips, color-by-file-type)
- Sortable file/folder list view
- File-type size breakdown
- Delete files/folders from the UI (with path-safety validation)
- Scan history — revisit past scans, not just the latest
- Scheduled scans (cron expressions) for specific paths

## Repo layout

```
backend/   ASP.NET Core solution (WebDiskTree.Api / Core / Infrastructure, plus tests)
frontend/  Angular app
Dockerfile
docker-compose.yml
```

## Configuration: AllowedRoots

For safety, the app will only scan (and, if enabled, delete under) paths explicitly listed in `AllowedRoots` — there is no free-text arbitrary path scanning. This is empty by default, so **nothing is scannable until you configure it**.

Configure via environment variables (see `docker-compose.yml` for an example) or `appsettings.json`:

```json
{
  "AllowedRoots": {
    "Roots": [
      { "Path": "/hostfs", "Label": "Whole disk", "AllowDelete": false },
      { "Path": "/hostfs/home/me/Downloads", "Label": "Downloads", "AllowDelete": true }
    ]
  }
}
```

Delete is only permitted under a root with `AllowDelete: true`, which also requires that path to be mounted read-write in the container (see below).

## Running with Docker

```bash
docker build -t webdisktree .
docker run -d --name webdisktree -p 8080:8080 \
  -v webdisktree-data:/data \
  -v /:/hostfs:ro \
  webdisktree
```

- `/hostfs` (read-only) is where the host filesystem is mounted so the app can measure real disk usage. Mount only what you're comfortable exposing.
- To allow deleting under a specific path, add an additional **read-write** mount over that subpath (e.g. `-v /home/you/Downloads:/hostfs/home/you/Downloads:rw`) and mark the matching `AllowedRoots` entry `AllowDelete: true`.
- `/data` persists the SQLite database and gzip-compressed scan tree blobs across restarts.

Or with `docker-compose.yml` (edit the `AllowedRoots` env vars and volume mounts for your setup first):

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

Open http://localhost:4200. In development, `appsettings.Development.json` points `Storage:DataDirectory` at `./data` (relative to the API project) and pre-configures one delete-enabled `AllowedRoots` entry at `./data/sample` for local testing.

### Backend tests

```bash
cd backend
dotnet test
```
