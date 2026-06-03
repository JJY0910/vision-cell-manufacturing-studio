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
  is_valid INTEGER NOT NULL DEFAULT 0,
  validation_summary TEXT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  UNIQUE(recipe_id, version)
);
```

Implementation note:

- `VisionCell.Persistence` initializes `recipes` through migration id `004_recipes`.
- `SqliteRecipeIndexRepository` upserts Recipe index rows by `(recipe_id, version)` and lists newest Recipe metadata first.
- `SqliteRecipeIndexRepository` uses the existing `is_active` column for local active Recipe selection. The active switch verifies the target row exists before clearing any previous active row and sets at most one indexed Recipe active.

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
- Teaching save/update/delete requests write `recipe_id` into history rows when an active recipe id is provided; it remains nullable until RecipeView owns active recipe selection.

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

Implementation note:

- `VisionCell.Persistence` initializes `inspection_results` and `defects` through migration id `005_inspection_results`.
- `SqliteInspectionResultRepository` implements `IInspectionResultRepository` and `IInspectionResultReader` for FR-200 result logging.
- The repository stores Recipe ID/version, lot ID, final Judge, defect summary, source image URI, optional overlay/height-map artifact paths, cycle time, step timings JSON, parameters JSON, and per-defect bounding boxes.
- `FileSystemInspectionArtifactWriter` creates overlay and height-map BMP files before result save. Current sequence rows populate `overlay_image_path` and `height_map_path` with relative paths under `inspection-artifacts/yyyyMMdd/`.
- `FileSystemInspectionArtifactWriter` also implements `IInspectionArtifactReader` so Offline Debug can read live artifact existence, size, modified-time metadata, deterministic BMP preview pixels, and safe external-open preparation without direct WPF file I/O.
- Safe external-open preparation resolves only supported overlay/height-map BMP artifact paths under the configured artifact root; rooted paths, traversal, missing files, not-recorded paths, and unsupported artifact types return operator-visible statuses instead of launching a viewer.
- The columns remain nullable so failed or legacy partial records can still be represented without a destructive migration.

### inspection_reinspect_comparisons

```sql
CREATE TABLE IF NOT EXISTS inspection_reinspect_comparisons (
  id TEXT PRIMARY KEY,
  source_result_id TEXT NOT NULL,
  replay_correlation_id TEXT NOT NULL,
  lot_id TEXT NOT NULL,
  recipe_id TEXT NOT NULL,
  recipe_version TEXT NOT NULL,
  previous_judgment TEXT NOT NULL,
  replayed_judgment TEXT NOT NULL,
  previous_defect_count INTEGER NOT NULL,
  replayed_defect_count INTEGER NOT NULL,
  previous_cycle_time_ms INTEGER NOT NULL,
  replayed_cycle_time_ms INTEGER NOT NULL,
  status TEXT NOT NULL,
  compared_at TEXT NOT NULL,
  persistence_status TEXT NOT NULL,
  message TEXT NOT NULL,
  FOREIGN KEY(source_result_id) REFERENCES inspection_results(id)
);
```

Implementation note:

- `VisionCell.Persistence` initializes `inspection_reinspect_comparisons` through migration id `008_inspection_reinspect_comparisons`.
- `SqliteInspectionReinspectComparisonRepository` implements `IInspectionReinspectComparisonRepository` and `IInspectionReinspectComparisonReader`.
- The table stores Offline Debug metadata comparison history only. It does not claim a new `inspection_results` row, source-image replay, or live camera/motion/vision sequence execution.

### equipment_alarms

```sql
CREATE TABLE IF NOT EXISTS equipment_alarms (
  id TEXT PRIMARY KEY,
  code TEXT NOT NULL,
  severity TEXT NOT NULL,
  area TEXT NOT NULL,
  message TEXT NOT NULL,
  correlation_id TEXT NULL,
  occurred_at TEXT NOT NULL,
  acknowledged_at TEXT NULL,
  action_memo TEXT NULL
);
```

Implementation note:

- `VisionCell.Persistence` initializes `equipment_alarms` through migration id `006_equipment_alarms`.
- `SqliteEquipmentAlarmRepository` implements `IEquipmentAlarmRepository` for save, latest-first list, and acknowledgement update.
- Motion, Camera, Inspection, and result persistence failures can write alarm rows through Application `IEquipmentAlarmRecorder`.
- `SqliteEquipmentIoTransitionRepository` implements `IEquipmentIoTransitionRepository` for simulator fault-injection I/O transition save and latest-first list.
- `acknowledged_at` and `action_memo` are operator recovery state, not proof that a real controller alarm was reset.

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
- `SqliteTeachingPointRepository` implements `ITeachingPointRepository` for list, ID lookup, case-insensitive name lookup, save/upsert, and delete.
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

### io_transition_history

```sql
CREATE TABLE IF NOT EXISTS io_transition_history (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  address TEXT NOT NULL,
  direction TEXT NOT NULL,
  previous_value INTEGER NOT NULL,
  current_value INTEGER NOT NULL,
  previous_forced INTEGER NOT NULL,
  current_forced INTEGER NOT NULL,
  source TEXT NOT NULL,
  correlation_id TEXT NULL,
  operator_memo TEXT NULL,
  changed_at TEXT NOT NULL
);
```

Implementation note:

- `VisionCell.Persistence` initializes `io_transition_history` through migration id `007_io_transition_history`.
- `SqliteEquipmentIoTransitionRepository` stores simulator fault-injection I/O bit transitions latest-first by `changed_at`.
- This table does not represent real PLC scan polling, output-write audit, debounce timing, or safety relay validation.

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
- `IEquipmentAlarmRepository`
- `IEquipmentIoTransitionRepository`

## Data Retention

- Event logs: configurable, default 30 days
- Inspection images: configurable, default keep all in portfolio mode
- DB compaction optional P3

## Path Policy

- Image/result paths stored relative to project app-data root when possible.
- User-chosen export path sanitized.
- No path traversal from recipe/input files.
