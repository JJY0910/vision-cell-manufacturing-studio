# ADR-0026: Teaching History Use Case Boundary

## Status

Accepted

## Context

TeachingView already used `ITeachingPointUseCase` for list, save, update, delete, and Go To workflows. Selected point history was the remaining Teaching read path that WPF coordinated directly through `ITeachingHistoryRepository`. That exposed a repository port to the UI layer and kept part of the Teaching workflow outside the use-case boundary.

The App layer should bind and format selected history rows, but the Application layer should own repository-facing Teaching history reads.

## Decision

- Add `ListHistoryAsync` to `ITeachingPointUseCase`.
- Implement the method in `TeachingPointUseCase` by validating the selected Teaching point id and list limit before calling `ITeachingHistoryRepository.ListByPointAsync`.
- Update `TeachingViewModel` to call `ITeachingPointUseCase.ListHistoryAsync`.
- Remove direct `ITeachingHistoryRepository` injection from `TeachingViewModel`.

## Alternatives Considered

- Keep direct repository injection in WPF: rejected because it keeps persistence-facing orchestration in the UI layer.
- Add a separate `ITeachingHistoryUseCase`: rejected for this slice because selected Teaching history is naturally scoped to `ITeachingPointUseCase` and shares the same repository dependencies.
- Move all TeachingView refresh orchestration into Application: deferred to avoid a broad ViewModel rewrite.

## Consequences

- TeachingView no longer depends directly on the Teaching history repository.
- Selected point history remains operator-visible with the same status/error behavior.
- Application tests now cover history read delegation and limit propagation.
- WPF still owns history row formatting and selected-point UI state.

## Requirement Coverage

- FR-100/FR-103/FR-104: Teaching point history remains attached to the selected Teaching workflow.
- FR-200/FR-260: Teaching history read paths remain persistence-backed and operator-visible.
- NFR-001/NFR-002: WPF remains MVVM-only and no longer coordinates Teaching history repository reads directly.
- NFR-004/NFR-006: History read errors remain visible through status text.
- NFR-TEST-001: Application and App tests cover the revised Teaching history use-case path.

## Rollback

Remove `ListHistoryAsync` from `ITeachingPointUseCase`, restore direct `ITeachingHistoryRepository` injection in `TeachingViewModel`, remove the new tests and ADR, and revert related documentation updates.
