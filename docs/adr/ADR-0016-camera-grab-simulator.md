# ADR-0016: Camera Grab Simulator Boundary

## Status

Accepted

## Context

`InspectionRunUseCase` can validate active Recipe context, evaluate Run Inspection interlocks, and submit a correlated equipment command, but the sequence still reported Grab Image as skipped. FR-140 and FR-141 require a camera simulator that can return a sample frame and explicit grab timeout/failure results without putting camera logic in WPF code-behind.

## Decision

- Add `ICameraDevice` in `VisionCell.Equipment.Cameras` as the hardware boundary for camera acquisition.
- Add `CameraGrabRequest`, `CameraGrabResult`, `CameraFrame`, and `CameraGrabStatus` contracts with correlation ID, timeout, result status, error code, elapsed time, metadata, and a Gray8 frame payload.
- Implement `VirtualCameraDevice` in `VisionCell.Simulator` with deterministic synthetic Gray8 package imagery plus injected timeout/failure/not-ready paths.
- Inject `ICameraDevice` into `InspectionRunUseCase` and execute Grab Image after Run Inspection controller acceptance.
- Bind `InspectionViewModel` to the grabbed frame and render it in `InspectionView` as the Last Grab image.

## Alternatives Considered

- Keep camera grab inside `VirtualEquipmentController`: rejected because controller command acceptance and camera frame acquisition are separate hardware boundaries.
- Generate the image directly in WPF: rejected because it would place machine-like acquisition logic in the UI layer and make FR-141 harder to test.
- Load a binary sample asset from disk: rejected for this slice to avoid asset path/config churn before result persistence paths are defined.

## Consequences

- Inspection timeline now progresses through Grab Image when camera readiness and Auto interlocks pass.
- Camera timeout/failure/not-ready cases return explicit camera results and fail the sequence before vision/judge/persist steps.
- The current frame is synthetic Gray8 only; 2D/3D algorithms, overlay generation, and result persistence remain future slices.

## Requirement Coverage

- FR-140: Virtual camera returns a sample frame and WPF displays the Last Grab image.
- FR-141: Virtual camera supports timeout and failure result paths with camera error codes.
- FR-180: Inspection sequence advances through Grab Image after controller acceptance.
- FR-181: Timeline reports Grab Image running/success/failure states.
- NFR-004: Camera grab uses the Run Inspection correlation ID.
- NFR-006: Exceptions/failures are converted into explicit result states.
- NFR-TEST-001: Application, Equipment, and App tests cover the new boundary.

## Rollback

Remove `ICameraDevice` and camera grab contracts, unregister `VirtualCameraDevice`, restore `InspectionRunUseCase` to skip camera/vision/judge/persist steps after controller acceptance, remove the Last Grab binding, tests, and this ADR.
