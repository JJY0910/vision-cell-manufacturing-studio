# ADR-0044: Hardware Adapter Readiness Matrix

Status: Accepted
Date: 2026-06-03

## Context

Priority 3 requires a hardware adapter boundary that allows the virtual controller path to be replaced by future real equipment integration without connecting real equipment in this slice. `IEquipmentController`, Motion/Camera/PLC adapter interfaces, `EquipmentRuntimeProfile`, and `RealHardwareReadinessGate` already block unvalidated real-hardware activation, but Settings did not show the adapter roles as a first-class matrix.

## Decision

- Add `HardwareAdapterBoundaryCatalog` in `VisionCell.Equipment.Hardware`.
- Keep the catalog limited to read-only adapter requirements for:
  - Motion Controller: `IMotionControllerAdapter` -> `MotionControllerAdapter`
  - Camera: `ICameraAdapter` -> `CameraAdapter`
  - PLC I/O: `IPlcIoAdapter` -> `PlcIoAdapter`
- Surface the catalog in `SettingsViewModel` and `SettingsView` as an adapter boundary matrix.
- Keep `RealHardware` runtime profile rejection unchanged until all readiness evidence is available.
- Do not implement `RealEquipmentController`, vendor SDK wrappers, PLC protocol, fieldbus communication, camera trigger wiring, or safety relay reset behavior in this slice.

## Alternatives

- Keep the adapter matrix in documentation only: rejected because Settings should show the same readiness boundary operators see when reviewing runtime scope.
- Add placeholder real adapter classes: rejected because placeholders could imply a usable real hardware path without validation.
- Put adapter-specific vendor fields into WPF Settings now: rejected until endpoint details and bench procedures are known.

## Consequences

- The three required adapter roles are testable and visible from one catalog.
- Settings can display current simulator providers, planned adapter names, and missing bench evidence without enabling real hardware.
- The boundary still depends on future implementation and bench validation before any real equipment claim.

## Requirement impact

- FR-020/FR-021/FR-022: future controller connect, snapshot, and command paths have a more visible adapter boundary.
- FR-080: PLC I/O path readiness remains explicit before any real I/O claim.
- FR-140/FR-141: camera acquisition readiness remains explicit before any real camera claim.
- FR-240: Settings now surfaces read-only hardware adapter readiness scope.
- NFR-002/NFR-004/NFR-007/NFR-009 remain supported.

## Rollback

Remove `HardwareAdapterBoundaryCatalog`, the Settings adapter matrix binding, related tests, and the documentation updates in this ADR and `docs/HARDWARE_INTEGRATION_PLAN.md`.
