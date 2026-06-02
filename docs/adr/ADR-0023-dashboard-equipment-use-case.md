# ADR-0023: Dashboard Equipment Use Case

## Status

Accepted

## Context

Dashboard equipment actions already supported connect, disconnect, snapshot refresh, and Manual/Auto mode transitions, but the WPF ViewModel directly coordinated `IEquipmentController` and `ICommandInterlockService`. That kept business orchestration close to WPF state mapping and made it harder to reuse the same command/result projection for future HMI screens or adapter validation.

The App layer must remain MVVM-focused: it can map state for binding, but equipment command orchestration, timeout/cancellation handling, and backend interlock evaluation should live behind an Application boundary.

## Decision

- Add `IEquipmentDashboardUseCase` in `VisionCell.Application`.
- Move Dashboard equipment command orchestration into `EquipmentDashboardUseCase`.
- Keep UI-specific axis/I/O/event collection mapping in `DashboardViewModel`.
- Return explicit command and snapshot result records that include operator-facing `SystemEvent` values.
- Use `ICommandInterlockService` from the use case for backend command availability checks.
- Register the use case in App service composition and inject it into `DashboardViewModel`.

## Alternatives Considered

- Keep controller/interlock orchestration inside `DashboardViewModel`: rejected because WPF would continue owning hardware-like command flow.
- Create separate use cases for each Dashboard button immediately: deferred because the current Dashboard commands share snapshot refresh and event projection behavior.
- Move all Dashboard state mapping into Application: rejected because WPF collection and selection state are UI concerns.

## Consequences

- Dashboard command execution now follows `User Button -> ViewModel Command -> Application UseCase -> Equipment Interface -> Simulator`.
- Connect, disconnect, refresh, and Manual/Auto mode commands share timeout/cancellation and event projection behavior.
- App tests can construct Dashboard through the same Application boundary used by runtime composition.
- Motion, Teaching, Recipe, and Inspection view-model cleanup can follow the same pattern in smaller future slices.

## Requirement Coverage

- FR-020/FR-021/FR-022: Dashboard equipment connect, disconnect, snapshot, and status refresh flow through an Application boundary.
- FR-003/FR-004: Command enablement uses Application interlock evaluation before WPF binding updates.
- FR-260: Dashboard emits structured `SystemEvent` entries from Application result projection.
- NFR-001/NFR-002: WPF remains MVVM-only and does not directly own equipment command orchestration.
- NFR-003/NFR-004/NFR-006: Command timeout, cancellation, result status, correlation, and user-visible event paths are preserved.
- NFR-TEST-001: Application and App tests cover command success, snapshot timeout, and interlock availability behavior.

## Rollback

Remove `IEquipmentDashboardUseCase` and `EquipmentDashboardUseCase`, restore direct controller/interlock injection in `DashboardViewModel`, remove the Application use case tests, and remove this ADR and related documentation updates.
