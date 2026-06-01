# ADR-0020: Inspection Result Persistence

## Status

Accepted

## Context

Inspection now executes active Recipe validation, Move To Camera, Grab Image, 2D inspection, synthetic 3D inspection, and Judge. The Persist Result timeline step still needs an Application/Persistence boundary so FR-200 can record inspection evidence without placing SQLite details in WPF or Vision code.

The implementation must keep the Application layer dependent only on repository contracts. SQLite schema changes must be additive, and image/overlay artifact generation should remain a follow-up because the current slice is focused on result metadata, defect rows, and sequence timing evidence.

## Decision

- Add `IInspectionResultRepository` and `IInspectionResultReader` to `VisionCell.Application`.
- Add `InspectionResultSaveRequest`, `InspectionResultRecord`, and `InspectionDefectRecord` to carry Recipe, lot, judgment, defect, timing, and artifact-path metadata.
- Add SQLite migration `005_inspection_results` with `inspection_results` and `defects` tables.
- Implement `SqliteInspectionResultRepository` in `VisionCell.Persistence`.
- Execute Persist Result in `InspectionRunUseCase` after 2D/3D Judge succeeds.
- Return `InspectionRunStatus.ResultPersistenceFailed` if persistence fails, and keep cancellation flowing through the active inspection token.
- Register the repository in WPF App composition while keeping WPF unaware of SQLite commands.

## Alternatives Considered

- Keep Persist Result skipped until overlay rendering exists: rejected because FR-200 SQLite metadata and defect persistence can be delivered independently.
- Store one JSON blob only: rejected because defect rows need queryable type, score, ROI, bbox, and message fields for Offline Debug and reporting follow-ups.
- Let `InspectionRunUseCase` write SQLite directly: rejected because Application must depend on ports, not persistence implementation details.

## Consequences

- Inspection sequence completion now creates a persisted result ID.
- Judge, defect summary, per-step timing, parameters, and 2D/3D defects are available through a read repository.
- Overlay and height-map artifact paths remain nullable until rendering/export work is added.
- A database failure now marks the Persist Result step failed and returns an explicit result status instead of silently accepting the run.

## Requirement Coverage

- FR-180/FR-181: The inspection sequence now executes Persist Result after Judge.
- FR-182: Cancellation propagates through the persistence call.
- FR-200: SQLite result and defect records are created by an additive migration and repository.
- NFR-004: Correlation ID and step timings are persisted with the result.
- NFR-006: Persistence failures return an operator-visible failure status and timeline message.
- NFR-008: Paths are stored as generated relative/URI-like metadata; no absolute user path is hard-coded.
- NFR-TEST-001: Application and Persistence tests cover success, failure, insert, readback, ordering, and limit behavior.

## Rollback

Remove the inspection result repository contracts, unregister the SQLite repository, remove migration `005_inspection_results`, restore `InspectionRunUseCase` to skip Persist Result, and remove the related tests and documentation.
