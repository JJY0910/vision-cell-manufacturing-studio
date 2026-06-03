# Hardware Integration Plan

## Scope

This plan defines how `VirtualEquipmentController` can be replaced by a future `RealEquipmentController` without changing WPF ViewModels or Application use cases. It does not implement any real Pemtron, PLC, motion controller, camera, light controller, fieldbus, or vendor SDK connection.

## Current Boundary

- WPF talks to Application use cases.
- Application use cases talk to `IEquipmentController`, `ICameraDevice`, motion command use cases, and repository ports.
- `VirtualEquipmentController` implements `IEquipmentController`.
- `VirtualCameraDevice` implements `ICameraDevice`.
- WPF App composition registers `EquipmentRuntimeProfile.Virtual` by default.
- A `RealHardware` runtime profile is explicitly rejected at startup until the adapter implementation and validation phases below are complete.
- `RealHardwareReadinessGate` lists the evidence missing from a rejected real-hardware profile request so the startup guard is auditable instead of a generic block.

## New Adapter Contracts

The `VisionCell.Equipment.Hardware` namespace defines future real-hardware adapter contracts:

- `IHardwareAdapter`: common adapter identity and status.
- `IMotionControllerAdapter`: axis snapshot reads and motion command execution.
- `ICameraAdapter`: camera readiness and frame acquisition.
- `IPlcIoAdapter`: I/O reads and output writes.
- `HardwareAdapterStatus`: common connected/ready/endpoint/status-message snapshot.

These contracts preserve timeout, cancellation, explicit result paths, and correlation IDs. Vendor-specific SDK objects must not cross these interfaces.

## Adapter Boundary Matrix

`HardwareAdapterBoundaryCatalog` lists the three required adapter boundaries that must remain visible before any future `RealHardware` profile can be considered. The Settings screen displays this matrix as read-only evidence guidance; it does not enable real hardware.

| Adapter role | Interface | Current provider | Future adapter name | Required evidence |
|---|---|---|---|---|
| Motion Controller | `IMotionControllerAdapter` | `VirtualEquipmentController` motion simulator | `MotionControllerAdapter` | Motion adapter bench validation |
| Camera | `ICameraAdapter` | `VirtualCameraDevice` | `CameraAdapter` | Camera adapter bench validation |
| PLC I/O | `IPlcIoAdapter` | `VirtualEquipmentController` simulator I/O | `PlcIoAdapter` | PLC I/O adapter bench validation |

Every future adapter row must preserve timeout, cancellation, explicit `MachineCommandResult` or acquisition result paths, and correlation IDs. Any change to the row set or required evidence must update this plan, the Settings adapter matrix, and the readiness tests together.

## Future RealEquipmentController Shape

```text
RealEquipmentController : IEquipmentController
  -> IMotionControllerAdapter
  -> ICameraAdapter
  -> IPlcIoAdapter
  -> alarm/event recorder
```

`RealEquipmentController.GetSnapshotAsync` should compose safety, axis, I/O, camera, and alarm snapshots from the adapters. `ExecuteCommandAsync` should route controller and motion commands to the correct adapter while preserving `MachineCommandRequest.CorrelationId`.

The future controller must be enabled through an explicit `EquipmentRuntimeProfile` change. It must not replace the virtual profile by default, and it must not be enabled before bench validation evidence exists.

## RealHardware Readiness Gate

The current App composition evaluates `RealHardwareReadinessEvidence.Unvalidated` whenever a `RealHardware` runtime profile is requested. The gate requires all of the following evidence before any future code path may even be considered for enabling:

- `RealEquipmentController` implementation.
- Motion adapter bench validation.
- Camera adapter bench validation.
- PLC I/O adapter bench validation.
- Safety reset validation.
- Review evidence for this hardware integration plan.

The gate does not enable real hardware by itself. It only makes the missing evidence explicit in the rejection message. Real equipment remains blocked until implementation and validation are completed in controlled follow-up work.

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

The PLC and remote I/O portion of phases 2, 3, and any later output-write checks must follow `docs/BENCH_PLC_IO_VALIDATION_CHECKLIST.md`. The checklist is evidence-gating only; it does not enable `RealHardware` mode or add a real PLC adapter.

## Not Validated

- No real hardware connection has been performed.
- No vendor SDK, PLC protocol, fieldbus, encoder, trigger timing, safety relay, or camera acquisition path has been validated.
- No production calibration, measurement accuracy, takt time, burn-in, or shop-floor display QA has been completed.
- The current readiness gate uses `Unvalidated` evidence by design, so `RealHardware` profile selection remains blocked.

## Acceptance Before Real Hardware Claim

Do not claim production readiness until adapter implementation, bench validation, safety I/O confirmation, camera trigger validation, inspection accuracy evidence, and documented recovery procedures are complete.
