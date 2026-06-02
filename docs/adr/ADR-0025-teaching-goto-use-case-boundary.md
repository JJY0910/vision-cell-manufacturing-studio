# ADR-0025: Teaching Go To Use Case Boundary

## Status

Accepted

## Context

Teaching point save/update/delete/list workflows already flow through `ITeachingPointUseCase`, and Save Current Position reads equipment snapshots inside the Application layer. Go To Teaching Point was inconsistent: `TeachingViewModel` read `IEquipmentController` snapshots directly, converted them to an interlock context, and then passed that context to the Application use case.

That left hardware-like snapshot orchestration inside WPF and made the Go To path less reusable for future HMI surfaces or adapter validation.

## Decision

- Change `TeachingPointGoToRequest` to carry `SnapshotTimeout` and `CommandTimeout`.
- Move Go To snapshot retrieval and `EquipmentSnapshotInterlockContextFactory` conversion into `TeachingPointUseCase.GoToAsync`.
- Remove direct `IEquipmentController` injection from `TeachingViewModel`.
- Keep TeachingViewModel responsible for selected-point state, confirmation, operator input parsing, and status text.
- Preserve `IMotionCommandUseCase` as the motion execution boundary for the final Move Absolute command.

## Alternatives Considered

- Keep passing `InterlockContext` from WPF: rejected because WPF would continue owning equipment snapshot orchestration.
- Add a separate Teaching panel use case only for snapshot reads: rejected because Go To already belongs to `ITeachingPointUseCase` and needs the selected teaching point repository lookup.
- Move all Teaching history loading into this slice: deferred to keep the contract change small and focused.

## Consequences

- Go To Teaching Point now follows `ViewModel -> TeachingPointUseCase -> Equipment snapshot -> Interlock context -> MotionCommandUseCase`.
- Snapshot unavailable failures are returned as explicit `TeachingPointOperationStatus.SnapshotUnavailable` results.
- WPF no longer directly reads equipment snapshots in TeachingViewModel.
- The Go To request contract changed, so callers now provide snapshot and command timeouts rather than a prebuilt interlock context.
- Teaching history query cleanup remains a future small slice.

## Requirement Coverage

- FR-003/FR-004: Go To command prechecks now use Application-generated interlock context.
- FR-060/FR-063: Teaching Go To reads current equipment snapshot before motion dispatch.
- FR-100/FR-103/FR-104: Teaching point Go To remains a use-case-driven workflow.
- FR-200/FR-260: Teaching history and operator-visible result paths remain preserved.
- NFR-001/NFR-002: WPF remains MVVM-only and no longer directly owns Go To equipment snapshot reads.
- NFR-003/NFR-004/NFR-006: Timeout, cancellation, snapshot failure, and command result paths remain explicit.
- NFR-TEST-001: Application and App tests cover the revised request contract, snapshot-based interlock context, and snapshot failure path.

## Rollback

Restore `TeachingPointGoToRequest` to carry `InterlockContext`, restore `IEquipmentController` injection and snapshot reads in `TeachingViewModel`, remove the new tests and ADR, and revert related documentation updates.
