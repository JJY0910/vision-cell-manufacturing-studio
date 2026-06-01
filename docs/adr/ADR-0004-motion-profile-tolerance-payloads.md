# ADR-0004: Motion Profile and Tolerance Payloads

Status: Accepted
Date: 2026-06-01

## Context

ADR-0003 introduced typed jog and absolute move target payloads, but Move Absolute still used simulator defaults for velocity profile and arrival tolerance. FR-065 requires motion profile handling, and real equipment adapters need the same traceable request payload that MotionView operators enter.

## Decision

Extend `AbsoluteMoveTarget` with request-level profile and tolerance fields:

- `Velocity`
- `Acceleration`
- `Deceleration`
- `Jerk`
- `ArrivalTolerance`

The payload remains stored in `MachineCommandRequest.Parameters`, parsed by `MotionCommandParameterParser`, dispatched by `IMotionCommandUseCase`, and persisted through motion command history. MotionView exposes the values as operator-editable fields. The simulator validates positive finite values and applies the profile values to the resulting axis snapshots while preserving each axis profile unit.

## Alternatives

- Keep profile values as simulator constants: rejected because command history would not capture the operator-selected move profile.
- Create per-axis profile payloads now: deferred because FR-065 can be advanced with a request-level profile first, while per-axis override semantics need recipe and hardware adapter acceptance criteria.
- Add a DB schema column for profile values: rejected because the existing request JSON already preserves the typed payload without a schema change.

## Consequences

- Move Absolute records now include target, profile, and tolerance values in one correlated request.
- Simulator success paths surface the requested velocity and tolerance in the command result message.
- Invalid profile/tolerance values are rejected before state changes.
- Future real adapters can consume the same payload keys without WPF-specific coupling.

## Requirement Impact

- FR-063: Move Absolute carries target and profile payloads.
- FR-065: Motion profile fields are represented in the command request path.
- FR-067: Invalid profile values return explicit rejected results.
- FR-069: Motion history persists profile and tolerance payloads.
- NFR-004: Request/result traceability covers executed profile data.

## Rollback

Remove the profile/tolerance fields from `AbsoluteMoveTarget`, parser validation, MotionView inputs, simulator profile application, tests, and this ADR. The ADR-0003 target-only payload path remains valid.
