# Page Dependency Trees

Traced recursively from each route's lazy-loaded component through local imports only (`./`, `../`); `@angular/*`, `angular-split` and `d3-*` are skipped. Every page also implicitly depends on the app shell (`app.ts` / `app.html` / `app.scss`) and the global `styles.scss` — listed once here rather than repeated per page.

**Shell (applies to all four pages)**
```
- frontend/src/app/app.ts
  - frontend/src/app/app.html
  - frontend/src/app/app.scss
  - frontend/src/app/core/services/theme.service.ts
- frontend/src/styles.scss
- frontend/src/index.html
```

---

## `/scans/:id` — Scan detail  ← **primary design target**

Entry: `frontend/src/app/features/scan-detail/scan-detail.ts`

```
- frontend/src/app/features/scan-detail/scan-detail.ts
  - frontend/src/app/features/scan-detail/scan-detail.html
  - frontend/src/app/features/scan-detail/scan-detail.scss
  - frontend/src/app/features/scan-detail/file-list/file-list.ts
    - frontend/src/app/features/scan-detail/file-list/file-list.html
    - frontend/src/app/features/scan-detail/file-list/file-list.scss
    - frontend/src/app/core/services/file.service.ts
    - frontend/src/app/core/models/directory-node.model.ts
    - frontend/src/app/shared/format-bytes.pipe.ts
    - frontend/src/app/shared/local-date.pipe.ts
  - frontend/src/app/features/scan-detail/treemap/treemap.ts
    - frontend/src/app/features/scan-detail/treemap/treemap.html
    - frontend/src/app/features/scan-detail/treemap/treemap.scss
    - frontend/src/app/features/scan-detail/treemap/treemap-layout.ts
    - frontend/src/app/features/scan-detail/treemap/canvas-hierarchy-render.ts
      - frontend/src/app/features/scan-detail/treemap/hierarchy-colors.ts   ← color authority
    - frontend/src/app/shared/format-bytes.pipe.ts
  - frontend/src/app/features/scan-detail/stretched-treemap/stretched-treemap.ts
    - frontend/src/app/features/scan-detail/stretched-treemap/stretched-treemap.html
    - frontend/src/app/features/scan-detail/stretched-treemap/stretched-treemap.scss
    - frontend/src/app/features/scan-detail/treemap/hierarchy-colors.ts
    - frontend/src/app/features/scan-detail/treemap/canvas-hierarchy-render.ts
  - frontend/src/app/features/scan-detail/sunburst/sunburst.ts
    - frontend/src/app/features/scan-detail/sunburst/sunburst.html
    - frontend/src/app/features/scan-detail/sunburst/sunburst.scss
    - frontend/src/app/features/scan-detail/treemap/hierarchy-colors.ts
    - frontend/src/app/features/scan-detail/treemap/treemap-layout.ts
  - frontend/src/app/features/scan-detail/type-breakdown/type-breakdown.ts
    - frontend/src/app/features/scan-detail/type-breakdown/type-breakdown.html
    - frontend/src/app/features/scan-detail/type-breakdown/type-breakdown.scss
    - frontend/src/app/core/services/file.service.ts
    - frontend/src/app/core/models/file-entry.model.ts
    - frontend/src/app/shared/format-bytes.pipe.ts
  - frontend/src/app/features/scan-detail/scan-progress-banner/scan-progress-banner.ts
    - frontend/src/app/features/scan-detail/scan-progress-banner/scan-progress-banner.html
    - frontend/src/app/features/scan-detail/scan-progress-banner/scan-progress-banner.scss
    - frontend/src/app/core/services/scan-progress.service.ts
    - frontend/src/app/shared/format-bytes.pipe.ts
  - frontend/src/app/core/services/scan.service.ts
  - frontend/src/app/core/services/file.service.ts
  - frontend/src/app/core/models/directory-node.model.ts
  - frontend/src/app/core/models/scan.model.ts
  - frontend/src/app/shared/format-bytes.pipe.ts
  - frontend/src/app/shared/format-count.pipe.ts
```

### Recommended `--context-file` bundle for designing this page

Visual files only — services and models carry no UI. The full set is **~1060 lines**, comfortably under the ~900-line-per-file trimming threshold *and* small enough in total that **no line ranges are needed and there is no 400 risk**. Pass every file whole:

```
.superdesign/design-system.md
frontend/src/styles.scss                                              (58)
frontend/src/app/app.html                                             (22)
frontend/src/app/app.scss                                             (56)
frontend/src/app/features/scan-detail/scan-detail.html                (87)
frontend/src/app/features/scan-detail/scan-detail.scss                (62)
frontend/src/app/features/scan-detail/file-list/file-list.html        (88)
frontend/src/app/features/scan-detail/file-list/file-list.scss       (123)
frontend/src/app/features/scan-detail/scan-progress-banner/*.html     (25)
frontend/src/app/features/scan-detail/scan-progress-banner/*.scss     (42)
frontend/src/app/features/scan-detail/type-breakdown/*.html           (18)
frontend/src/app/features/scan-detail/type-breakdown/*.scss           (47)
frontend/src/app/features/scan-detail/treemap/hierarchy-colors.ts     (31)
```

**Render-branch note (verified by reading, not inferred):** `scan-detail.html` has no responsive or feature-flag branching — there are no media queries anywhere in the codebase. Its branches are purely *data state*: `@if (scan(); as s)` → `@if (s.status === Completed && breadcrumb().length > 0)` → `@if (focus(); as node)`. **The branch that renders for a finished scan is the `as-split` two-pane block**; the alternatives are only a "Waiting for the scan to finish…" line and a "Loading…" line. Reproduce the completed-scan two-pane branch.

---

## `/scans` — Scan history

Entry: `frontend/src/app/features/scan-history/scan-history.ts`

```
- frontend/src/app/features/scan-history/scan-history.ts
  - frontend/src/app/features/scan-history/scan-history.html
  - frontend/src/app/features/scan-history/scan-history.scss   ← hardcoded status chip colors
  - frontend/src/app/core/services/scan.service.ts
  - frontend/src/app/core/models/scan.model.ts
  - frontend/src/app/shared/format-bytes.pipe.ts
  - frontend/src/app/shared/format-count.pipe.ts
```

---

## `/schedules` — Scheduled scans

Entry: `frontend/src/app/features/schedules/schedules.ts`

```
- frontend/src/app/features/schedules/schedules.ts
  - frontend/src/app/features/schedules/schedules.html
  - frontend/src/app/features/schedules/schedules.scss
  - frontend/src/app/core/services/schedule.service.ts
  - frontend/src/app/core/services/scan.service.ts
  - frontend/src/app/core/models/schedule.model.ts
  - frontend/src/app/core/models/scan.model.ts
```

---

## `/scans/new` — Start a scan

Entry: `frontend/src/app/features/scan-start/scan-start.ts`

```
- frontend/src/app/features/scan-start/scan-start.ts
  - frontend/src/app/features/scan-start/scan-start.html
  - frontend/src/app/features/scan-start/scan-start.scss
  - frontend/src/app/core/services/scan.service.ts
  - frontend/src/app/core/models/scan.model.ts
```
