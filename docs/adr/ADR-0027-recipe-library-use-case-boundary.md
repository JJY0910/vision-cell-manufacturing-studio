# ADR-0027: Recipe Library Use Case Boundary

## Status

Accepted

## Context

RecipeView already used `IRecipeLibraryUseCase` for Recipe save, but recent index reads and selected Recipe activation still flowed through `IRecipeIndexRepository` directly from WPF. That left Recipe library orchestration split between the UI layer and Application layer.

WPF should bind, format, and surface operator state. Application should own Recipe library repository-facing reads, validation, and activation coordination so UI screens do not depend directly on persistence-facing Recipe ports.

## Decision

- Add `ListRecentAsync` and `ActivateAsync` to `IRecipeLibraryUseCase`.
- Implement recent-list validation and selected Recipe identity validation in `RecipeLibraryUseCase` before delegating to `IRecipeIndexRepository`.
- Update `RecipeViewModel` to use `IRecipeLibraryUseCase` for list, save, and activate workflows.
- Remove direct `IRecipeIndexRepository` injection from `RecipeViewModel`.

## Alternatives Considered

- Keep direct repository injection in WPF: rejected because it keeps Recipe library orchestration split across UI and Application layers.
- Add a separate Recipe activation use case: rejected for this slice because list/save/activate are all Recipe library workflows over the same document/index boundary.
- Move all Recipe editor validation and row formatting into Application: deferred because WPF-specific input parsing and visual row state remain UI responsibilities.

## Consequences

- RecipeView no longer depends directly on the Recipe index repository.
- Recipe index list and activation behavior remain operator-visible with the same status/error paths.
- Application tests cover list limit validation, activation identity validation, and repository delegation.
- WPF still owns editor text parsing, selection state, and Recipe index row presentation.

## Requirement Coverage

- FR-120/FR-121: Recipe list and save workflows stay behind the Application Recipe library boundary.
- FR-122: Active Recipe selection now flows through the same Application Recipe library boundary.
- FR-200/FR-260: Recipe metadata remains persistence-backed and operator-visible.
- NFR-001/NFR-002: WPF remains MVVM-only and no longer coordinates Recipe repository reads directly.
- NFR-004/NFR-006: Recipe list and activation errors remain surfaced through status text.
- NFR-TEST-001: Application and App tests cover the revised Recipe library use-case path.

## Rollback

Remove `ListRecentAsync` and `ActivateAsync` from `IRecipeLibraryUseCase`, restore direct `IRecipeIndexRepository` injection in `RecipeViewModel`, remove the new tests and ADR, and revert related documentation updates.
