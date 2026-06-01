# ADR-0011: Recipe Library Save Use Case

Status: Accepted
Date: 2026-06-01

## Context

Recipe JSON documents and the SQLite Recipe index now exist as separate Persistence capabilities. If RecipeView or another UI workflow calls both directly, WPF would own save ordering, validation handling, checksum generation, and index failure behavior.

FR-120, FR-121, FR-123, and FR-124 need a single Application boundary that can save a valid Recipe document and then update metadata for list/query workflows.

## Decision

Add `IRecipeLibraryUseCase` in `VisionCell.Application.Recipes`.

The use case:

- validates `RecipeDefinition` with `RecipeValidator` before storage.
- saves valid documents through `IRecipeDocumentStore`.
- computes a SHA-256 checksum from a deterministic Application-level JSON representation.
- upserts `RecipeIndexEntry` through `IRecipeIndexRepository`.
- returns explicit result status for validation, document storage, and index failures.

Active Recipe selection remains outside this use case until app settings and activation semantics are defined.

## Alternatives

- Let RecipeView call both Persistence services directly: rejected because WPF must not own persistence orchestration.
- Put checksum generation in the SQLite repository: rejected because checksum describes the Recipe document payload, not the index table.
- Add activation now: deferred because active Recipe uniqueness and inspection precheck behavior require a separate contract.

## Consequences

- Recipe save workflows gain one Application boundary for document + index consistency.
- RecipeView can later bind save/import commands without knowing file or SQLite details.
- Document save and index update are still not a database transaction across file system and SQLite; failures are reported explicitly.

## Requirement Impact

- FR-120: Adds Application-level Recipe save orchestration.
- FR-121: Keeps validation before persistence.
- FR-123: Writes version metadata into the Recipe index.
- FR-124: Stores validation state for list display.
- NFR-004: Persists checksum and metadata for traceability.
- NFR-008: Reuses the safe document store boundary.
- NFR-TEST-001: Adds Application tests for success and failure paths.

## Rollback

Remove `IRecipeLibraryUseCase`, its request/result/status records, `RecipeLibraryUseCase`, related tests, docs, and this ADR. Existing Recipe validation, JSON document store, and SQLite index repository remain intact.
