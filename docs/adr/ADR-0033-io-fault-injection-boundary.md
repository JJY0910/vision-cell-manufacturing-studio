# ADR-0033: I/O Fault Injection Boundary

Status: Accepted
Date: 2026-06-02

## Context

Priority 4 requires an I/O Monitor with simulator fault injection for EStop, Door, Vacuum, AirPressure, CameraReady, and ServoAlarm states. Existing Dashboard binding already reads `EquipmentSnapshot.Io`, and Alarm Center can persist recoverable alarm records, but there was no explicit Application boundary for operator-triggered simulator faults.

## Decision

Add a simulator-only fault injection boundary:

- `VisionCell.Equipment.Faults.IEquipmentFaultInjector` applies fault state changes with timeout, cancellation, and `MachineCommandResult`.
- `EquipmentFaultInjectionUseCase` creates correlated `MachineCommandRequest` records, projects `SystemEvent` output, refreshes the equipment snapshot, and records active injected faults through `IEquipmentAlarmRecorder`.
- `VirtualEquipmentController` implements `IEquipmentFaultInjector` and updates Safety, Camera, Axis Alarm, I/O bits, and active `AlarmSnapshot`.
- WPF `EquipmentViewModel` uses Application use cases only. It does not call simulator internals directly.

## Alternatives

- Add new values to `CommandKind` and route faults through `IEquipmentController.ExecuteCommandAsync`. This would mix real controller commands with simulator test controls and make future real adapter replacement noisier.
- Keep fault injection in WPF only. This would violate MVVM/Application boundary expectations and would not provide traceable command results.

## Consequences

- Fault injection remains clearly simulator-only while still following equipment command timeout/cancellation/result rules.
- Active injected faults can appear in Alarm Center through the same alarm recorder used by Motion, Camera, and Inspection failures.
- No real PLC, safety relay, servo drive, camera trigger, or fieldbus path is validated by this ADR.
- Future real hardware adapters should not implement this interface as a production control path; they should expose diagnostic inputs through the hardware adapter plan instead.

## Requirement Impact

FR-040, FR-041, FR-042, FR-044, FR-067, FR-080, FR-081, FR-082, FR-083, FR-184, FR-201, NFR-001, NFR-002, NFR-004, NFR-006, NFR-007, NFR-009

## Rollback

Remove `VisionCell.Equipment.Faults`, `EquipmentFaultInjectionUseCase`, and the EquipmentView bindings, then restore `VirtualEquipmentController` to fixed normal I/O snapshot values.
