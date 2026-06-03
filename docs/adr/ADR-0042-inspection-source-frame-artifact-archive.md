# ADR-0042: Inspection Source Frame Artifact Archive

## Status

Accepted

## Context

Offline Debug source-image replay readiness can now classify source references, but new inspection results still need archived source pixels before any future source-image replay runner can be meaningful. Persisted `inspection_results.source_image_path` already exists, so this can be improved without a SQLite schema migration.

The current artifact writer already receives the grabbed Gray8 source frame, creates deterministic overlay and height-map BMP evidence, and returns relative artifact paths under `inspection-artifacts/yyyyMMdd/`.

## Decision

- Extend `InspectionArtifactWriteResult` with `SourceImagePath`.
- Make `FileSystemInspectionArtifactWriter` write `{resultId}.source.bmp` from the grabbed Gray8 frame before saving the inspection result.
- Save the generated source BMP relative path into `inspection_results.source_image_path`.
- Keep source image archive files under the same artifact root/path traversal policy as overlay and height-map files.
- Let `IInspectionReinspectSourceImageReadinessUseCase` use `IInspectionArtifactReader` metadata when available, so archived source BMP rows are reported as archived source input while still noting that replay execution is not implemented.

## Alternatives Considered

- Continue storing only `camera-frame://...` references: rejected because that preserves correlation but not replayable source pixels.
- Store source image bytes in SQLite: rejected for the same reasons as overlay/height-map artifacts; image evidence is easier to inspect, export, and manage as files.
- Enable `Run Re-inspect` from the archived source BMP immediately: rejected because the source-image replay runner, Recipe replay policy, and acceptance criteria are still separate work.

## Consequences

- New inspection rows now point to deterministic source BMP artifacts.
- Offline Debug can report archived source input availability for newly generated rows.
- Existing legacy rows with `camera-frame://...` references remain valid historical records and are still classified as transient frame references.
- Source-image replay execution, customer image format loading, and new replayed `inspection_results` persistence remain follow-up work.

## Requirement Links

- FR-200: Persisted inspection rows now include source artifact evidence.
- FR-220/FR-221: Offline Debug can reason about archived source artifact readiness.
- FR-222: Future Re-inspect has a durable source-image input boundary.
- FR-240/NFR-009: This does not validate real camera hardware or real replay execution.
- NFR-004/NFR-006/NFR-008/NFR-TEST-001: Correlated artifact paths remain deterministic, relative, and tested.

## Rollback

Remove `SourceImagePath` from `InspectionArtifactWriteResult`, stop writing `{resultId}.source.bmp`, restore `InspectionRunUseCase` to write the prior camera-frame reference, remove the related tests, and revert this ADR plus documentation updates.
