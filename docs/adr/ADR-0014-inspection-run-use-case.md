# ADR-0014: Inspection Run Use Case

Status: Accepted
Date: 2026-06-01

## Context

InspectionView can resolve active Recipe context and block missing or invalid Recipe startup, but the Run Inspection button still does not cross the Application boundary. FR-180, FR-181, and FR-182 need a testable sequence orchestration point that can validate active Recipe state, evaluate equipment interlocks, expose step status, and support operator cancellation without placing business logic in WPF.

## Decision

Add an Application-layer inspection run contract:

- `IInspectionRunUseCase` owns Run Inspection orchestration for the WPF screen.
- `InspectionRunUseCase` reads `IActiveRecipeContext`, reads the equipment snapshot, evaluates `RunInspection` interlocks, and submits a correlated `MachineCommandRequest` to `IEquipmentController`.
- `InspectionRunResult` returns explicit status, message, Recipe metadata, command request/result, and ordered sequence step records.
- `InspectionSequenceStepRecord` reports `Pending`, `Running`, `Success`, `Failed`, `Skipped`, and `Cancelled` states for UI timeline binding.
- WPF `InspectionViewModel` binds the use case result and exposes Stop as cancellation of the active run token.

The current slice marks camera grab, 2D/3D inspection, judge, and persistence steps as skipped after controller acceptance. Those steps remain separate implementation slices.

## Alternatives

- Call `IEquipmentController.ExecuteCommandAsync` directly from `InspectionViewModel`: rejected because WPF would own sequence orchestration and interlock interpretation.
- Implement camera, vision algorithms, result persistence, and overlay rendering in the same PR: rejected because it would combine too many hardware-like contracts and DB behavior into one review.
- Persist inspection result rows immediately: deferred until the result schema and image artifact path policy are implemented together.

## Consequences

- Run Inspection has a stable Application contract and can be unit-tested without WPF.
- UI can render step timeline state and cancellation feedback without blocking the UI thread.
- The simulator still needs a future Auto mode workflow and real camera/vision/result slices before the full FR-180 acceptance path is complete.

## Requirement Impact

- FR-122: active Recipe context is enforced at inspection startup.
- FR-180: Run Inspection now reaches an Application sequence boundary.
- FR-181: sequence step state is exposed for timeline binding.
- FR-182: Stop requests cancellation of the active inspection run.
- FR-200: result persistence remains deferred and explicitly skipped.
- NFR-004: correlated command request/result is returned by the use case.
- NFR-006: WPF remains MVVM and does not own sequence business logic.
- NFR-TEST-001: Application and App tests cover accepted, rejected, and cancelled paths.

## Rollback

Remove `VisionCell.Application.Inspection`, unregister `IInspectionRunUseCase`, restore `InspectionViewModel` to active Recipe precheck-only behavior, remove the tests, and revert this ADR.
