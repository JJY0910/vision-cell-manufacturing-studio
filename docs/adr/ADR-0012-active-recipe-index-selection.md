# ADR-0012: Active Recipe Index Selection

## Status

Accepted

## Date

2026-06-01

## Context

Recipe JSON documents and the SQLite Recipe index now support save, lookup, and recent-list workflows. FR-122 also requires the equipment workflow to know which Recipe is active before Teaching ownership, inspection sequences, and operator HMI states can rely on Recipe context.

The `recipes` table already contains an `is_active` column. Adding a second app-settings-only active Recipe pointer would create duplicate state and make RecipeView, TeachingView, and inspection startup disagree unless every caller kept both stores synchronized.

## Decision

Use the existing SQLite `recipes.is_active` field as the authoritative local active Recipe index state.

`IRecipeIndexRepository` will expose:

- `FindActiveAsync` to query the currently active Recipe metadata row.
- `SetActiveAsync` to mark one existing `(recipe_id, version)` row active and clear all other active flags.

`SetActiveAsync` must first verify that the target row exists. If the row is missing or the caller passes blank identifiers, it returns `false` and preserves the previous active Recipe state. This prevents a failed operator selection or stale UI row from clearing the machine context.

No schema migration is required because `is_active` already exists.

## Consequences

- RecipeView can add an activation command without introducing a parallel settings store.
- Teaching history can later read the active Recipe through the same Application port before writing Recipe context.
- Inspection startup can query active Recipe metadata before loading the JSON document.
- Cross-process concurrency remains limited to SQLite transaction semantics; richer multi-client coordination is out of scope for the local WPF workstation.

## Requirement Coverage

- FR-120 Recipe CRUD
- FR-122 Active Recipe selection
- FR-123 Recipe metadata index
- FR-124 Recipe validation state
- NFR-004 Structured persistence boundaries
- NFR-TEST-001 Regression coverage
