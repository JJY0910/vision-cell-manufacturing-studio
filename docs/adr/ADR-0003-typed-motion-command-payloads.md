# ADR-0003: Typed Motion Command Payloads

Status: Accepted
Date: 2026-06-01

## Context

MotionView can execute simulator-backed Servo, Home, Jog, Move Absolute, and Stop commands through `IMotionCommandUseCase`, and command history now stores request/result pairs. Jog and Move Absolute still depended on fixed simulator defaults, so operator-entered axis, direction, step, and target values were not the authoritative command payload at the controller boundary.

Real equipment adapters need the same traceable request object that the Application layer records, otherwise the command log can drift from the command that was executed.

## Decision

Introduce typed motion command payload helpers in `VisionCell.Motion.Commands`:

- `JogMotionTarget` serializes axis, direction, and step into `MachineCommandRequest.Parameters`.
- `AbsoluteMoveTarget` serializes X/Y/Z/Theta absolute targets into `MachineCommandRequest.Parameters`.
- `MotionCommandParameterParser` parses persisted/request parameter dictionaries back into typed targets.
- `IEquipmentController.ExecuteCommandAsync` gains a request-aware overload that accepts the correlated `MachineCommandRequest`.
- `MotionCommandUseCase` passes the exact recorded request to the controller.
- The simulator consumes typed jog/move payloads, validates soft limits, and returns explicit rejected results for invalid targets.
- MotionView exposes operator-entered jog axis/step and absolute target fields while keeping command execution in the view-model/Application use case path.

The original timeout-based controller overload remains as a compatibility shim so existing call sites can migrate gradually.

## Alternatives

- Keep simulator presets until real adapters are introduced: rejected because it would make MotionView command history misleading and delay the Application/controller contract.
- Pass ad hoc dictionaries directly from WPF to the simulator: rejected because the typed payload format should live outside WPF and remain reusable by real adapters.
- Add full motion profile and tolerance DTOs now: deferred because velocity, acceleration, and arrival tolerance need acceptance criteria across UI, simulator, and future adapter behavior.

## Consequences

- Command history now records the same typed payload that the controller executes.
- MotionView operator inputs can drive simulator axis movement without WPF code-behind logic.
- Real controller adapters can implement the request-aware overload and consume the same payload keys.
- Profile/tolerance support remains a separate backlog item.

## Requirement Impact

- FR-062: Jog supports operator-selected axis, direction, and step.
- FR-063: Move Absolute supports operator-entered X/Y/Z/Theta targets.
- FR-066: Soft-limit rejection remains controller-enforced for typed targets.
- FR-069: Motion history stores typed command parameters.
- NFR-004: Correlated request/result logging stays aligned with executed payloads.

## Rollback

Remove the typed payload records/parser, revert `IEquipmentController` and `MotionCommandUseCase` to the timeout-only overload, restore MotionView preset command controls, and remove simulator typed payload handling.
