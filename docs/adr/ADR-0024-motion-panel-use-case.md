# ADR-0024: Motion Panel Use Case

## Status

Accepted

## Context

Motion command execution already flows through `IMotionCommandUseCase`, but `MotionViewModel` still directly used `IEquipmentController` for snapshot refresh and command availability checks. That left part of the hardware-like Motion workflow in WPF and made Dashboard and Motion command-state behavior inconsistent.

The Motion screen should keep UI-only concerns such as axis collection formatting, operator input parsing, and command button notification. Snapshot retrieval, timeout/cancellation conversion, and interlock availability should be coordinated by the Application layer.

## Decision

- Add `IMotionPanelUseCase` in `VisionCell.Application.Motion`.
- Add `MotionPanelUseCase` to coordinate Motion snapshot refresh and command availability.
- Return `MotionSnapshotRefreshResult` with explicit refreshed, cancelled, timeout, and failed statuses.
- Keep existing `IMotionCommandUseCase` as the execution boundary for Servo/Home/Jog/Move/Stop commands.
- Update `MotionViewModel` to depend on `IMotionPanelUseCase` instead of `IEquipmentController`.
- Register `IMotionPanelUseCase` in App service composition.

## Alternatives Considered

- Add snapshot and availability methods to `IMotionCommandUseCase`: rejected because command execution history and panel state refresh are related but separate responsibilities.
- Keep direct `IEquipmentController` access in WPF: rejected because Motion snapshot and availability are hardware-like orchestration concerns.
- Move Motion input parsing into Application immediately: deferred because parsing text fields and preset selections remains a WPF operator-input concern for this slice.

## Consequences

- MotionView no longer directly refreshes snapshots or evaluates command availability through controller APIs.
- Snapshot timeout, cancellation, and failure states are explicit Application results before WPF status text mapping.
- Motion command execution remains traceable through `IMotionCommandUseCase` and motion command history.
- If snapshot refresh fails before a command, MotionView does not dispatch the command and does not label the run as completed.
- Teaching, Recipe, and Inspection view-model cleanup remain future small slices.

## Requirement Coverage

- FR-003/FR-004: Motion command enabled state now uses an Application interlock boundary.
- FR-060/FR-061/FR-062/FR-063/FR-064: Motion snapshot state and operator command prechecks are coordinated before command dispatch.
- FR-069: Motion history execution remains unchanged through `IMotionCommandUseCase`.
- FR-260: Operator-visible Motion status paths cover snapshot success, cancellation, timeout, and failure.
- NFR-001/NFR-002: WPF remains MVVM-only and no longer directly owns Motion snapshot/availability controller access.
- NFR-003/NFR-004/NFR-006: Timeout, cancellation, explicit result status, and rejection visibility are preserved.
- NFR-TEST-001: Application and App tests cover snapshot refresh, timeout, availability, and no-dispatch-on-timeout behavior.

## Rollback

Remove `IMotionPanelUseCase` and `MotionPanelUseCase`, restore `IEquipmentController` injection in `MotionViewModel`, remove the Motion panel tests, unregister the use case, and remove this ADR and related documentation updates.
