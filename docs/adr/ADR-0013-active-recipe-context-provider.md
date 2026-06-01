# ADR-0013: Active Recipe Context Provider

## Status

Accepted

## Date

2026-06-01

## Context

RecipeView can now mark one indexed Recipe as active through the SQLite Recipe index. Teaching, inspection sequence startup, and future command interlocks also need the active Recipe ID/version, but those workflows must not depend on SQLite-specific repository details or duplicate the active-row interpretation.

Directly injecting `IRecipeIndexRepository` into every workflow would make each caller repeat no-active, invalid-active, and storage-failure handling. It would also blur the Application boundary because each caller would need to decide whether an active but invalid Recipe is usable.

## Decision

Add an Application-layer active Recipe context provider:

- `IActiveRecipeContext.GetActiveAsync` returns an `ActiveRecipeContextResult`.
- The result status is one of `Success`, `NotSelected`, `InvalidRecipe`, or `RepositoryUnavailable`.
- Cancellation is propagated to callers rather than converted into a normal result.
- The provider uses `IRecipeIndexRepository.FindActiveAsync` as its source of truth.

An active Recipe row must be valid before the provider returns `Success`. Invalid active rows are reported explicitly so operator workflows can block inspection startup or Teaching ownership with a clear message.

## Consequences

- RecipeView, Teaching, and inspection workflows can share the same active Recipe interpretation.
- Persistence remains behind Application ports.
- UI and sequence code can show actionable status text without parsing repository exceptions.
- This ADR does not load the JSON Recipe document; it only exposes the active indexed metadata needed for ownership and precondition checks.

## Requirement Coverage

- FR-120 Recipe CRUD
- FR-122 Active Recipe selection
- FR-180 Inspection sequence prerequisites
- FR-200 Traceable persistence context
- NFR-004 Application boundary
- NFR-TEST-001 Regression coverage
