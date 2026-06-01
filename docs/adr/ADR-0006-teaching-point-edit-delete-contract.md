# ADR-0006: Teaching Point Edit/Delete Contract

Status: Accepted
Date: 2026-06-01

## Context

FR-104 requires Teaching edit history. Save Current Position now appends Created history rows, but update and delete workflows still do not have explicit Application contracts. Adding edit/delete directly in WPF would put mutation rules in the UI and make before/after traceability hard to test.

## Decision

Extend `ITeachingPointUseCase` with explicit update and delete operations:

- `UpdateAsync(TeachingPointUpdateRequest, CancellationToken)`
- `DeleteAsync(TeachingPointDeleteRequest, CancellationToken)`

The Application layer loads the existing Teaching Point first, validates duplicate names and domain rules, performs the repository mutation, and writes a `teaching_history` row:

- update writes `TeachingHistoryAction.Updated` with before and after JSON.
- delete writes `TeachingHistoryAction.Deleted` with before JSON.

The repository port adds `DeleteAsync(Guid, CancellationToken)`. Existing `SaveAsync` remains the persistence operation for create/update upsert, while Application use cases decide whether the mutation is create or update.

## Alternatives

- Add edit/delete logic only in WPF: rejected because it would bypass use-case validation and traceability tests.
- Add separate SQLite-specific unit-of-work now: deferred because active recipe ownership and update/delete UI are still evolving.
- Add a soft-delete flag to the schema now: deferred because current requirements only require history traceability, not restore/undelete behavior.

## Consequences

- Update/delete behavior is testable without WPF.
- Persistence remains simple and compatible with the existing `teaching_points` schema.
- Point mutation and history append are still not an atomic database transaction.
- WPF edit/delete commands can be added in a follow-up PR without changing the Application contract.

## Requirement Impact

- FR-100: Teaching Points remain validated through the same Motion domain model.
- FR-101: Existing list and Go To workflows remain unchanged.
- FR-104: Update/delete now have before/after history paths.
- FR-120/FR-121: `recipe_id` remains nullable until active recipe ownership is introduced.
- FR-200/NFR-004: History rows improve traceability.
- NFR-009: Public contract change is recorded by this ADR.

## Rollback

Remove the new update/delete request/result files, `ITeachingPointUseCase` methods, repository delete port, SQLite delete implementation, tests, related docs, and this ADR. Save Current Position and Go To behavior from prior PRs remain valid.
