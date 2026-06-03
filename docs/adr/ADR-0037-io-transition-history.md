# ADR-0037: I/O Transition History

Date: 2026-06-03

## Status

Accepted

## Context

FR-084 requires I/O state changes to be persisted with timestamp and source. The current EquipmentView can display simulator I/O and invoke fault injection through `IEquipmentFaultInjectionUseCase`, but the I/O changes produced by EStop, Door, AirPressure, Vacuum, CameraReady, ServoAlarm, and Clear All were only visible in the refreshed snapshot and event list.

Real PLC integration is intentionally out of scope. The project still needs a traceable repository boundary so later `IPlcIoAdapter` and `RealEquipmentController` work can reuse the same persistence contract without WPF owning database writes.

## Decision

- Add `IoTransitionRecord` under `VisionCell.Equipment.Io`.
- Add Application port `IEquipmentIoTransitionRepository`.
- Add SQLite table `io_transition_history` and `SqliteEquipmentIoTransitionRepository`.
- Have `EquipmentFaultInjectionUseCase` compare simulator I/O snapshots before and after a successful fault injection command, then save only changed bit value or forced-state transitions.
- Register the SQLite repository in WPF App composition.

## Consequences

- Simulator fault injection now records source, correlation ID, operator memo, timestamp, bit name, address, direction, previous/current value, and previous/current forced state.
- WPF remains MVVM-only and does not perform SQLite writes.
- Snapshot comparison is best-effort for history only. A pre-injection snapshot timeout skips transition persistence but does not block the simulator fault command.
- Real PLC scan polling, output-write history, retention policy, and bench hardware validation remain follow-up work.

## 2026-06-03 Follow-up

`EquipmentViewModel` now reads recent `IEquipmentIoTransitionRepository` rows and `EquipmentView` displays them as a read-only I/O transition history. This keeps WPF on the Application repository port and does not add PLC polling, output writes, or retention cleanup.

## Requirement Coverage

- FR-080: transition rows use the same I/O bit names and addresses shown in the monitor.
- FR-081: simulator fault toggles produce persisted transition records.
- FR-082: transition records include bit name, address, direction, and forced-state changes.
- FR-084: state changes have timestamp and source persistence.
- NFR-004/NFR-TEST-001: Application and Persistence tests cover the boundary.

## Rollback

Remove `IoTransitionRecord`, `IEquipmentIoTransitionRepository`, `NoopEquipmentIoTransitionRepository`, `SqliteEquipmentIoTransitionRepository`, the `io_transition_history` schema migration, App DI registration, use-case transition comparison, tests, and related documentation updates.
