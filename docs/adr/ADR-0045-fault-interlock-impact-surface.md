# ADR-0045: Fault Interlock Impact Surface

Status: Accepted
Date: 2026-06-03

## Context

Priority 4 requires the I/O Monitor and Fault Injection workflow to connect simulator fault state to interlocks and alarms. Fault injection already updates simulator I/O bits, fault state rows, alarm recording, and I/O transition history. Operators still need a compact way to see which high-risk equipment commands are blocked after an injected fault without switching screens.

## Decision

- Add an interlock impact projection to `EquipmentViewModel`.
- Build the projection from the latest `EquipmentSnapshot` through `EquipmentSnapshotInterlockContextFactory` and `IEquipmentDashboardUseCase.GetCommandAvailability`.
- Display Servo On, Home, Jog, Move Absolute, Enter Auto, Run Inspection, and Reset Alarm readiness in `EquipmentView`.
- Keep the projection read-only and simulator-backed; it does not execute commands or bypass backend interlock rules.

## Alternatives

- Duplicate interlock rules in WPF: rejected because backend rules must remain the source of truth.
- Add new fault-specific command handlers: rejected because the existing fault injection use case already owns command execution and alarm recording.
- Show impact only on Dashboard: rejected because Equipment is where operators inject simulator faults and monitor I/O state.

## Consequences

- Fault injection now has an immediate operator-visible connection to command interlocks.
- The Equipment screen can show why an injected EStop or similar fault blocks follow-up equipment commands.
- The implementation remains limited to simulator snapshots and existing Application/Core interlock contracts.
- Real PLC wiring, safety relay behavior, hardware reset, and field operator validation remain unverified.

## Requirement impact

- FR-040/FR-041/FR-042: safety/interlock fault states are surfaced next to impacted commands.
- FR-043/FR-044: fault injection remains connected to alarm/event state and now exposes interlock impact.
- FR-080/FR-081/FR-082/FR-083/FR-084: I/O monitor and transition history remain visible while fault impact is shown from the same snapshot.
- FR-184/FR-201: inspection and alarm readiness remain protected by backend interlock and alarm paths.
- NFR-002/NFR-004/NFR-007/NFR-TEST-001 remain supported.

## Rollback

Remove the interlock impact collection from `EquipmentViewModel`, remove the `EquipmentView` interlock impact table, and revert the related tests and documentation updates.
