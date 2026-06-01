# ADR-0009: Recipe JSON Document Store

Status: Accepted
Date: 2026-06-01

## Context

ADR-0008 introduced a typed Recipe definition and validator, but FR-120 also requires Recipe files that can be saved and loaded. File-system behavior must not leak into Core, and Recipe JSON persistence needs path traversal protection before it is exposed through WPF.

## Decision

Add an Application port, `IRecipeDocumentStore`, with explicit load/save result objects. Implement `JsonRecipeDocumentStore` in Persistence.

The store:

- validates Recipe definitions with `RecipeValidator` before saving.
- stores documents under a configured root directory only.
- generates file names as `{RecipeId}.v{Version}.recipe.json`.
- accepts only safe recipe id characters and semantic versions in file names.
- returns explicit status values for validation failure, invalid file name, missing file, invalid JSON document, and storage failures.

## Alternatives

- Put JSON I/O in Application: rejected because file-system behavior belongs outside the Application use-case contract.
- Allow caller-supplied file paths: rejected because it increases path traversal risk and weakens Recipe naming consistency.
- Save invalid Recipes and surface validation later: rejected because FR-124 requires clear validation feedback before persistence.

## Consequences

- Recipe JSON round-trip is testable without WPF.
- RecipeView can later call through an Application-facing port rather than owning file I/O.
- SQLite Recipe indexing and active recipe app settings remain separate follow-up work.

## Requirement Impact

- FR-120: Adds the first concrete Recipe file persistence path.
- FR-121: Round-trips typed Teaching, ROI, camera, vision, and sequence data.
- FR-124: Invalid Recipes return validation issues before save.
- NFR-008: File names are constrained to prevent path traversal.
- NFR-TEST-001: Adds Persistence coverage for round-trip and rejection paths.

## Rollback

Remove `IRecipeDocumentStore`, result/status records, `JsonRecipeDocumentStore`, tests, related docs, and this ADR. Recipe validation contract from ADR-0008 remains intact.
