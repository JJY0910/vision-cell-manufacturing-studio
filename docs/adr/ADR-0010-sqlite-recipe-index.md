# ADR-0010: SQLite Recipe Index

Status: Accepted
Date: 2026-06-01

## Context

Recipe JSON documents can now be saved and loaded, but RecipeView still needs a fast list/query path. Scanning files for every view refresh would mix file-system behavior into UI flows and would not expose active/valid state clearly.

## Decision

Add an Application port, `IRecipeIndexRepository`, and a SQLite implementation, `SqliteRecipeIndexRepository`.

Add migration `004_recipes` for the `recipes` table with recipe id, version, product name, document path, checksum, active state, validation state, validation summary, and timestamps. The table uses a unique `(recipe_id, version)` constraint so saves can upsert metadata for a Recipe version.

## Alternatives

- Query Recipe JSON files directly from RecipeView: rejected because UI should not own file-system scanning or JSON parsing.
- Combine JSON document save and SQLite index update in one service now: deferred until RecipeView workflows define save/activate semantics.
- Store only JSON without SQLite: rejected because FR-123/FR-124 need list, validation, and version metadata.

## Consequences

- Recipe metadata can be listed without loading full JSON documents.
- Recipe validation state can be shown in UI lists.
- JSON document save and SQLite index update are not yet an atomic unit.

## Requirement Impact

- FR-120: Adds Recipe index persistence.
- FR-123: Version metadata is queryable by recipe id/version.
- FR-124: Validation state is persisted for UI display.
- NFR-004: Metadata supports audit-friendly Recipe traceability.
- NFR-TEST-001: Adds Persistence coverage for insert, upsert, lookup, and list behavior.

## Rollback

Remove `IRecipeIndexRepository`, `RecipeIndexEntry`, `SqliteRecipeIndexRepository`, migration `004_recipes`, tests, related docs, and this ADR. Recipe JSON document storage remains intact.
