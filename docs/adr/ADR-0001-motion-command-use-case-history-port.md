# ADR-0001: Motion Command Use Case and History Port

Status: Accepted
Date: 2026-06-01

## Context

Motion commands currently execute through `IEquipmentController.ExecuteCommandAsync`, and the simulator returns explicit command results for success, rejection, timeout, and cancellation. The next layer needs an Application use case that can create a traceable command request, call the equipment controller, and hand the request/result pair to a persistence boundary without making WPF or simulator code own history behavior.

## Decision

Add an Application-layer motion command use case and a history repository port:

- `MotionCommandUseCase` creates a `MachineCommandRequest` with a correlation ID, command name, timeout, timestamp, and parameters.
- The use case supports Servo On, Servo Off, Home, Jog, Move Absolute, and Stop command kinds.
- The controller result is normalized to the request correlation ID before history is recorded.
- `IMotionCommandHistoryRepository` stores a `MotionCommandHistoryEntry`.
- SQLite implementation remains a later Persistence-layer PR so this PR does not introduce schema/migration behavior.

## Alternatives

- Call `IEquipmentController` directly from WPF view-models: rejected because command traceability and orchestration would leak into UI code.
- Add SQLite repository and migration in the same PR: rejected for PR size and because the current Persistence project has not established migration structure yet.
- Change `IEquipmentController.ExecuteCommandAsync` to accept `MachineCommandRequest`: deferred until typed motion request DTOs are introduced.

## Consequences

- Motion commands gain an Application orchestration point that is testable without WPF.
- Command history persistence can be added by implementing the repository port.
- The current controller interface still has a temporary mismatch because it returns its own result correlation ID, so the use case normalizes the result for history consistency.

## Requirement Impact

- FR-022: command timeout/cancellation path remains controller-driven.
- FR-063: absolute move command request/result can be captured for history.
- FR-064: duplicate motion rejection result can be captured for history.
- FR-067: timeout/cancelled results can be captured for history.
- FR-069: future move history table/view can consume the repository port.
- NFR-004: request/result correlation ID is centralized in the Application use case.

## Rollback

Remove the Application motion use case, history repository port, tests, and this ADR. Existing simulator command execution can continue through `IEquipmentController` directly.
