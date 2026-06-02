# ADR-0028: Inspection Precheck Use Case Boundary

## Status

Accepted

## Context

InspectionView already used `IInspectionRunUseCase` for Run Inspection sequence orchestration, timeline updates, and operator cancellation. Active Recipe precheck remained the one Inspection screen path that WPF coordinated directly through `IActiveRecipeContext`.

The App layer should bind precheck results and display operator state, while the Application layer should own Recipe prerequisite reads for the Inspection workflow. Keeping both precheck and run behind one Inspection use-case boundary makes the screen easier to test and keeps WPF from depending directly on Recipe context ports.

## Decision

- Add `PrecheckActiveRecipeAsync` to `IInspectionRunUseCase`.
- Implement the method in `InspectionRunUseCase` by delegating to `IActiveRecipeContext.GetActiveAsync`.
- Update `InspectionViewModel` to call `IInspectionRunUseCase.PrecheckActiveRecipeAsync`.
- Remove direct `IActiveRecipeContext` injection from `InspectionViewModel`.

## Alternatives Considered

- Keep direct active Recipe context injection in WPF: rejected because it leaves Inspection prerequisite orchestration split across UI and Application layers.
- Add a separate Inspection precheck use case: rejected for this slice because precheck is a prerequisite of the existing Inspection run workflow and shares the same Application dependency.
- Move all Inspection result-to-UI formatting into Application: deferred because WPF-specific image source creation and row formatting remain UI responsibilities.

## Consequences

- InspectionView no longer depends directly on `IActiveRecipeContext`.
- Active Recipe precheck and Run Inspection now share the same Application use-case boundary.
- Application tests cover precheck delegation to active Recipe context.
- App tests cover precheck binding through the run use case, including repository-unavailable operator feedback.

## Requirement Coverage

- FR-180/FR-181: Inspection sequence prerequisites and run orchestration remain behind the Application Inspection boundary.
- FR-200/FR-260: Inspection prerequisite state remains operator-visible before persisted result workflows.
- NFR-001/NFR-002: WPF remains MVVM-only and no longer coordinates active Recipe context reads directly for Inspection.
- NFR-004/NFR-006: Active Recipe precheck errors remain surfaced through status text.
- NFR-TEST-001: Application and App tests cover the revised Inspection precheck path.

## Rollback

Remove `PrecheckActiveRecipeAsync` from `IInspectionRunUseCase`, restore direct `IActiveRecipeContext` injection in `InspectionViewModel`, remove the new tests and ADR, and revert related documentation updates.
