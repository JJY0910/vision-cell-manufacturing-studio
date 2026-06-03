# ADR-0032: Hardware Adapter Boundary

Status: Accepted
Date: 2026-06-02

## Context

The project is simulator-first, but future work must allow `VirtualEquipmentController` to be replaced by real equipment integration without changing WPF ViewModels or Application use cases. Priority 3 asks for Motion, Camera, and PLC I/O adapter design while explicitly avoiding real equipment connection in this slice.

## Decision

- Keep `IEquipmentController` as the Application-facing equipment boundary.
- Add `VisionCell.Equipment.Hardware` adapter contracts:
  - `IHardwareAdapter`
  - `IMotionControllerAdapter`
  - `ICameraAdapter`
  - `IPlcIoAdapter`
  - `HardwareAdapterStatus`
- Document the future `RealEquipmentController` composition in `docs/HARDWARE_INTEGRATION_PLAN.md`.
- Do not implement a real hardware controller, vendor SDK wrapper, fieldbus connection, or PLC protocol in this PR.

2026-06-03 follow-up:

- Add `EquipmentRuntimeProfile` in WPF App composition.
- Register the virtual runtime profile by default.
- Explicitly reject `RealHardware` profile selection until `RealEquipmentController` implementation and bench validation evidence exist.

## Alternatives

- Add `RealEquipmentController` now with placeholder failures: rejected because it could look like an implemented hardware path without validation.
- Put vendor-specific details into `IEquipmentController`: rejected because it would leak adapter details into Application use cases.
- Keep the plan document-only: rejected because lightweight contracts give tests and future code a stable boundary.

## Consequences

- Future real equipment work has clear adapter seams while WPF/Application remain stable.
- Adapter contracts enforce timeout, cancellation, explicit result paths, and correlation preservation.
- Runtime composition cannot accidentally switch from virtual equipment to unvalidated real hardware.
- The project still has no real hardware validation.

## Requirement impact

- FR-020/FR-021/FR-022: real controller connection, snapshot, and command paths now have a planned adapter boundary.
- FR-080: PLC I/O monitor data has a planned adapter boundary.
- FR-140/FR-141: camera acquisition and failure paths have a planned adapter boundary.
- FR-240: equipment profile/path settings can target adapter endpoints in future work.
- NFR-002/NFR-004/NFR-007/NFR-009 remain supported.

## Rollback

Remove `VisionCell.Equipment.Hardware` contracts, adapter contract tests, `docs/HARDWARE_INTEGRATION_PLAN.md`, this ADR, and related documentation updates.
