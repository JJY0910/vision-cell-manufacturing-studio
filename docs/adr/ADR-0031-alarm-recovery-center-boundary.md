# ADR-0031: Alarm Recovery Center Boundary

Status: Accepted
Date: 2026-06-02

2026-06-03 follow-up: `AlarmView` now shows protocol-spec recovery guidance for the selected alarm code. This is operator guidance only; acknowledgement remains separate from hardware reset confirmation.

## Context

Priority 2 requires an Alarm / Fault / Recovery Center with an `EquipmentAlarm` domain model, SQLite persistence, WPF operator view, and links from Motion/Camera/Inspection failures. Existing `AlarmSnapshot` represents the controller's current alarm state and `SystemEvent` represents transient event-log rows, but neither supports operator acknowledgement or recovery memo history.

## Decision

- Add `EquipmentAlarm` in `VisionCell.Core.Alarms` with alarm code, severity, equipment area, message, occurred time, optional acknowledged time, optional action memo, and optional correlation ID.
- Add Application ports/use cases: `IEquipmentAlarmRepository`, `IEquipmentAlarmRecorder`, and `IAlarmCenterUseCase`.
- Add `SqliteEquipmentAlarmRepository` and migration `006_equipment_alarms`.
- Add WPF `AlarmView` and `AlarmViewModel` through Shell navigation.
- Display recovery guidance in the selected alarm detail from the documented alarm/error code catalog.
- Record non-cancelled Motion command failures through `MotionCommandUseCase`.
- Record controller start, Camera grab, 2D/3D inspection, and result persistence failures through `InspectionRunUseCase`.

## Alternatives

- Store alarms only as `SystemEvent` rows: rejected because events do not carry acknowledgement/recovery memo state.
- Extend simulator `AlarmSnapshot`: rejected because snapshot state is current equipment state, not a durable operator recovery history.
- Add a real PLC alarm adapter now: rejected because real hardware adapter work belongs to the Hardware Adapter Boundary priority.

## Consequences

- Alarm history is traceable and queryable from SQLite.
- The WPF operator can review recovery guidance, acknowledge alarms, and store recovery notes without code-behind business logic.
- Schema migration count increases from 5 to 6.
- This PR records simulated/Application failure paths only. It does not validate real PLC, motion controller, camera, safety relay, or fieldbus alarm sources.

## Requirement impact

- FR-022: command timeout/cancellation failure paths now have durable alarm recording for non-cancelled failures.
- FR-043: Alarm Reset remains simulator-current-state only; AlarmView guidance and acknowledgement are operator recovery history and not hardware reset.
- FR-067: motion timeout/failure injection can produce alarm records.
- FR-141: camera timeout/failure paths can produce alarm records.
- FR-184: inspection sequence failures can be represented as alarm records.
- FR-201: alarm records complement structured event/result traceability.
- FR-241: AlarmView begins the error/alarm catalog surface with code, area, severity, message, recovery guidance, and recovery memo.
- NFR-002/NFR-004/NFR-006/NFR-007/NFR-009 remain supported.

## Rollback

Remove the Core alarm domain files, Application alarm ports/use cases, SQLite alarm repository and migration, WPF Alarm module/navigation registration, failure recorder calls, tests, and related documentation updates.
