# ADR-0002: SQLite Motion History Bootstrap

Status: Accepted
Date: 2026-06-01

## Context

The Application layer now exposes `IMotionCommandHistoryRepository`, but there is no Persistence implementation. `docs/08_DATABASE_SPEC.md` already defines `motion_command_history`, and the application needs a real SQLite-backed repository before MotionView can show command history or before command traceability can be demonstrated end to end.

## Decision

Add a small SQLite bootstrap in `VisionCell.Persistence`:

- `SqliteConnectionFactory` opens SQLite connections and creates the parent database directory when needed.
- `SqliteSchemaInitializer` creates `schema_version` and `motion_command_history` idempotently.
- `SqliteMotionCommandHistoryRepository` implements `IMotionCommandHistoryRepository`.
- Repository rows store `request_json`, `result_json`, `elapsed_ms`, `correlation_id`, `command_name`, optional `axis_id`, and `created_at`.

## Alternatives

- Delay SQLite until all persistence repositories are designed: rejected because motion command traceability is a high-value P1 gap and can be implemented safely as a narrow slice.
- Use an in-memory history repository only: rejected because it would not prove the SQLite traceability path required by the database spec.
- Introduce a full migration framework now: deferred because the Persistence project is still early; the initializer keeps this schema idempotent and can later be moved behind a migration runner.

## Consequences

- Motion command history can be stored in SQLite without WPF involvement.
- Tests can verify schema initialization and insert/read behavior using temporary DB files.
- The current schema bootstrap is intentionally narrow; future repositories should converge on a shared migration runner before broader DB work.

## Requirement Impact

- FR-063: command request/result records can be stored.
- FR-069: move history table data source exists.
- FR-200: SQLite traceability foundation is extended.
- NFR-004: persisted records include correlation ID.
- NFR-008: DB paths stay caller-provided and no DB files are committed.

## Rollback

Remove the SQLite factory, schema initializer, repository, tests, and this ADR. The Application history port remains available for another Persistence implementation.
