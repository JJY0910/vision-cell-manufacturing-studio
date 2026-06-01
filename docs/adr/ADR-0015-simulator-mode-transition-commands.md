# ADR-0015: Simulator Mode Transition Commands

Status: Accepted
Date: 2026-06-01

## Context

Inspection startup now evaluates `RunInspection` through `InspectionRunUseCase`, but the simulator only reported connected equipment as Manual mode. That meant the WPF workflow could not reach the Auto interlock needed before Run Inspection, even when servo, homing, safety, camera, and I/O states were otherwise ready.

## Decision

Add explicit mode transition commands to the shared command contract:

- `CommandKind.EnterManualMode`
- `CommandKind.EnterAutoMode`

`CommandInterlockRules` validates mode changes before backend execution. Enter Auto requires connected equipment, idle controller, safety OK, EStop released, door closed, servo on, all required axes homed, axis idle, no axis alarm, camera connected, I/O ready, no active alarm, and no running sequence. Enter Manual requires connected equipment, idle controller, stopped sequence, and idle axis.

`VirtualEquipmentController` stores simulator mode state and exposes it through `EquipmentSnapshot`. Dashboard binds Manual and Auto commands using the existing MVVM command pattern and structured `SystemEvent` logging.

## Alternatives

- Treat Run Inspection as entering Auto implicitly: rejected because it hides an operator-visible machine state transition.
- Add a generic `SetMode` command with string payload: deferred because explicit enum command kinds keep interlock rules and UI labels easier to test.
- Require active Recipe before Enter Auto: rejected for this slice because Recipe context is currently owned by Recipe/Inspection workflows, while Enter Auto is an equipment readiness transition. Run Inspection still enforces active Recipe separately.

## Consequences

- Operators can transition the simulator to Auto from Dashboard once setup interlocks are satisfied.
- `InspectionRunUseCase` can pass the Auto-mode interlock in the real WPF simulator flow after servo/homing readiness.
- The command contract grew, so future hardware adapters must implement or explicitly reject these mode commands.

## Requirement Impact

- FR-004: Dashboard buttons expose enabled/disabled mode command state.
- FR-040: Enter Auto remains blocked by safety/alarm states.
- FR-041: Run Inspection still rejects door-open/safety failure through interlocks.
- FR-180: Auto-mode precondition can now be reached in the simulator.
- FR-181: Inspection timeline can progress beyond Auto-mode rejection when setup state is ready.
- NFR-004: mode command results are converted to structured `SystemEvent` entries.
- NFR-006: WPF remains MVVM command-bound.
- NFR-TEST-001: Application, Equipment, and App tests cover mode command rules and state transitions.

## Rollback

Remove the new command kinds, interlock rules, simulator mode state handling, Dashboard commands, tests, docs updates, and this ADR. The simulator will return to connected-as-Manual-only behavior.
