# 08. Database Specification

## DB File

Default local DB:

```text
local-data/visioncell.db
```

Do not commit DB files.

## Tables

### system_events

```sql
CREATE TABLE IF NOT EXISTS system_events (
  id TEXT PRIMARY KEY,
  correlation_id TEXT NOT NULL,
  severity TEXT NOT NULL,
  source TEXT NOT NULL,
  event_type TEXT NOT NULL,
  message TEXT NOT NULL,
  data_json TEXT NULL,
  created_at TEXT NOT NULL
);
```

### recipes

```sql
CREATE TABLE IF NOT EXISTS recipes (
  id TEXT PRIMARY KEY,
  recipe_id TEXT NOT NULL,
  version TEXT NOT NULL,
  product_name TEXT NOT NULL,
  file_path TEXT NOT NULL,
  checksum TEXT NOT NULL,
  is_active INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
```

### teaching_history

```sql
CREATE TABLE IF NOT EXISTS teaching_history (
  id TEXT PRIMARY KEY,
  teaching_point_id TEXT NOT NULL,
  recipe_id TEXT NULL,
  action TEXT NOT NULL,
  before_json TEXT NULL,
  after_json TEXT NULL,
  created_at TEXT NOT NULL
);
```

Implementation note:

- `VisionCell.Persistence` initializes `teaching_history` through migration id `003_teaching_history`.
- `SqliteTeachingHistoryRepository` implements `ITeachingHistoryRepository` for append-only save and latest-first per-point history queries.
- Save Current Position writes a Created history row through `ITeachingHistoryRepository` after the Teaching Point is persisted.
- `recipe_id` is nullable until active recipe ownership is wired into the teaching workflow.

### inspection_results

```sql
CREATE TABLE IF NOT EXISTS inspection_results (
  id TEXT PRIMARY KEY,
  correlation_id TEXT NOT NULL,
  lot_id TEXT NOT NULL,
  recipe_id TEXT NOT NULL,
  recipe_version TEXT NOT NULL,
  judgment TEXT NOT NULL,
  defect_summary TEXT NULL,
  source_image_path TEXT NOT NULL,
  overlay_image_path TEXT NULL,
  height_map_path TEXT NULL,
  cycle_time_ms INTEGER NOT NULL,
  step_timings_json TEXT NOT NULL,
  parameters_json TEXT NOT NULL,
  created_at TEXT NOT NULL
);
```

### defects

```sql
CREATE TABLE IF NOT EXISTS defects (
  id TEXT PRIMARY KEY,
  result_id TEXT NOT NULL,
  defect_type TEXT NOT NULL,
  score REAL NOT NULL,
  roi_id TEXT NULL,
  bbox_x INTEGER NOT NULL,
  bbox_y INTEGER NOT NULL,
  bbox_w INTEGER NOT NULL,
  bbox_h INTEGER NOT NULL,
  message TEXT NULL,
  FOREIGN KEY(result_id) REFERENCES inspection_results(id)
);
```

### teaching_points

```sql
CREATE TABLE IF NOT EXISTS teaching_points (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL COLLATE NOCASE UNIQUE,
  role TEXT NOT NULL,
  x REAL NOT NULL,
  y REAL NOT NULL,
  z REAL NOT NULL,
  theta REAL NOT NULL,
  tolerance_x REAL NOT NULL,
  tolerance_y REAL NOT NULL,
  tolerance_z REAL NOT NULL,
  tolerance_theta REAL NOT NULL,
  memo TEXT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
```

Implementation note:

- `VisionCell.Persistence` initializes `teaching_points` through migration id `002_teaching_points`.
- `SqliteTeachingPointRepository` implements `ITeachingPointRepository` for list, ID lookup, case-insensitive name lookup, and save/upsert.
- The `name` column uses a case-insensitive unique constraint to support duplicate-name validation before WPF binding.

### motion_command_history

```sql
CREATE TABLE IF NOT EXISTS motion_command_history (
  id TEXT PRIMARY KEY,
  correlation_id TEXT NOT NULL,
  command_name TEXT NOT NULL,
  axis_id TEXT NULL,
  request_json TEXT NOT NULL,
  result_json TEXT NOT NULL,
  elapsed_ms INTEGER NOT NULL,
  created_at TEXT NOT NULL
);
```

Implementation note:

- `VisionCell.Persistence` initializes `schema_version`, `motion_command_history`, `teaching_points`, and `teaching_history` idempotently through `SqliteSchemaInitializer`.
- `SqliteMotionCommandHistoryRepository` writes `MachineCommandRequest` and `MachineCommandResult` JSON with correlation ID and elapsed time.

## Migration Policy

- Every schema change creates a new migration class/file.
- Migration must be idempotent.
- App startup applies pending migrations.
- Schema version stored in `schema_version` table.

## Repository Interfaces

- `IEventRepository`
- `IRecipeRepository`
- `ITeachingPointRepository`
- `ITeachingHistoryRepository`
- `IInspectionResultRepository`
- `IMotionHistoryRepository`

## Data Retention

- Event logs: configurable, default 30 days
- Inspection images: configurable, default keep all in portfolio mode
- DB compaction optional P3

## Path Policy

- Image/result paths stored relative to project app-data root when possible.
- User-chosen export path sanitized.
- No path traversal from recipe/input files.
