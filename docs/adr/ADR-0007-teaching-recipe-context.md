# ADR-0007: Teaching History Recipe Context

Status: Accepted
Date: 2026-06-01

## Context

FR-120 and FR-121 require recipe-owned setup data, while FR-104 requires Teaching edit history. The `teaching_history` table already has a nullable `recipe_id`, but the Teaching use-case requests still write null for create/update/delete rows. Full Recipe CRUD and app-settings based active recipe selection are broader work, but history rows need a safe path to preserve recipe context as soon as the operator or Recipe workflow provides it.

## Decision

Add optional `RecipeId` fields to the Teaching mutation requests:

- `TeachingPointSaveRequest`
- `TeachingPointUpdateRequest`
- `TeachingPointDeleteRequest`

The Application layer passes that value into `TeachingHistoryEntry.Create` for Created, Updated, and Deleted rows. The existing `TeachingHistoryEntry` normalization keeps blank recipe values as null and trims non-empty values.

TeachingView exposes an Active Recipe input and forwards the trimmed value through the request DTOs. This is a temporary UI boundary until RecipeView owns active recipe selection through app settings.

## Alternatives

- Add `recipe_id` to `teaching_points` now: deferred because the current schema has no Recipe aggregate or active recipe repository yet.
- Implement full Recipe CRUD first: rejected for this slice because it would delay closing the existing Teaching history traceability gap.
- Keep recipe id null until RecipeView is complete: rejected because the request and history schema can already carry the context without a database migration.

## Consequences

- Teaching history rows can carry recipe context for create/update/delete operations.
- Existing callers remain source-compatible because `RecipeId` is optional and defaults to null.
- The active recipe input is not a full Recipe management implementation.
- Point mutation and history append are still not an atomic database transaction.

## Requirement Impact

- FR-104: Teaching history rows now include recipe context when provided.
- FR-120/FR-121: Teaching workflow can associate setup history with a recipe id before full Recipe CRUD lands.
- FR-200/NFR-004: Traceability improves without changing SQLite schema.
- NFR-009: Public request-contract change is recorded by this ADR.

## Rollback

Remove the optional `RecipeId` fields, revert the use-case history calls to null recipe id, remove the TeachingView Active Recipe input, remove related tests/docs, and delete this ADR.
