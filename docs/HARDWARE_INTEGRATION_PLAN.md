# Hardware Integration Plan

## Scope

This plan defines how `VirtualEquipmentController` can be replaced by a future `RealEquipmentController` without changing WPF ViewModels or Application use cases. It does not implement any real Pemtron, PLC, motion controller, camera, light controller, fieldbus, or vendor SDK connection.

## Current Boundary

- WPF talks to Application use cases.
- Application use cases talk to `IEquipmentController`, `ICameraDevice`, motion command use cases, and repository ports.
- `VirtualEquipmentController` implements `IEquipmentController`.
- `VirtualCameraDevice` implements `ICameraDevice`.

## New Adapter Contracts

The `VisionCell.Equipment.Hardware` namespace defines future real-hardware adapter contracts:

- `IHardwareAdapter`: common adapter identity and status.
- `IMotionControllerAdapter`: axis snapshot reads and motion command execution.
- `ICameraAdapter`: camera readiness and frame acquisition.
- `IPlcIoAdapter`: I/O reads and output writes.
- `HardwareAdapterStatus`: common connected/ready/endpoint/status-message snapshot.

These contracts preserve timeout, cancellation, explicit result paths, and correlation IDs. Vendor-specific SDK objects must not cross these interfaces.

## Future RealEquipmentController Shape

```text
RealEquipmentController : IEquipmentController
  -> IMotionControllerAdapter
  -> ICameraAdapter
  -> IPlcIoAdapter
  -> alarm/event recorder
```

`RealEquipmentController.GetSnapshotAsync` should compose safety, axis, I/O, camera, and alarm snapshots from the adapters. `ExecuteCommandAsync` should route controller and motion commands to the correct adapter while preserving `MachineCommandRequest.CorrelationId`.

## Adapter Responsibilities

### MotionControllerAdapter

- Connect to vendor motion SDK or controller endpoint.
- Read X/Y/Z/Theta state into `AxisSnapshot`.
- Execute Servo On/Off, Home, Jog, Move Absolute, Stop.
- Convert vendor alarms/timeouts/soft-limit errors to `MachineCommandResult` with `ErrorCode`.
- Never block caller threads; all long calls must be async with timeout/cancellation.

### CameraAdapter

- Connect to camera SDK or acquisition endpoint.
- Report readiness through `CameraSnapshot`.
- Execute grab with `CameraGrabRequest` timeout and cancellation.
- Convert SDK frame buffers into `CameraFrame`.
- Convert grab timeout/not-ready/failure to `CameraGrabResult`.

### PlcIoAdapter

- Connect to PLC or remote I/O endpoint.
- Read EStop, Door, Vacuum, AirPressure, CameraReady, ServoAlarm, tower lamp, and output states into `IoSnapshot`.
- Write allowed outputs with explicit `MachineCommandResult`.
- Keep unsafe output writes rejected by backend interlock rules.

## Validation Phases

1. Adapter contract tests with fake adapters.
2. Real adapter dry connection tests on a bench controller with no motion enabled.
3. Read-only snapshot validation for I/O, camera ready, and axis positions.
4. Motion command validation with soft limits and reduced speed.
5. Camera acquisition validation with a fixed target.
6. Alarm and recovery validation, including EStop and servo alarm handling.
7. Long-duration stability, thermal, vibration, and operator HMI checks.

## Not Validated

- No real hardware connection has been performed.
- No vendor SDK, PLC protocol, fieldbus, encoder, trigger timing, safety relay, or camera acquisition path has been validated.
- No production calibration, measurement accuracy, takt time, burn-in, or shop-floor display QA has been completed.

## Acceptance Before Real Hardware Claim

Do not claim production readiness until adapter implementation, bench validation, safety I/O confirmation, camera trigger validation, inspection accuracy evidence, and documented recovery procedures are complete.
