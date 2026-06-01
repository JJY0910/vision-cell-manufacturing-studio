# ADR-0017: Recipe Camera Move In Inspection Sequence

## Status

Accepted

## Context

Camera grab now runs through `ICameraDevice`, but `InspectionRunUseCase` still skips Move To Camera and uses default camera settings. FR-180 expects the sequence to progress through Recipe, Safety, Move, Grab, Inspect, Judge, and Save. The move must not reuse operator-facing `MoveAbsolute` rules directly because those rules intentionally require Manual mode and a stopped sequence.

## Decision

- Add `CommandKind.SequenceMoveToCamera` for internal Auto sequence motion.
- Evaluate `SequenceMoveToCamera` with Auto mode, loaded Recipe, active sequence, servo, homing, safety, axis idle, and soft-limit interlocks.
- Load the active Recipe document through `IRecipeDocumentStore` before the sequence starts.
- Select the first Recipe Teaching point with role `Camera`, convert its coordinates/tolerance into an `AbsoluteMoveTarget`, and execute it through `IMotionCommandUseCase`.
- Pass Recipe camera exposure, gain, and light settings into `CameraGrabRequest`.

## Alternatives Considered

- Reuse `MoveAbsolute` inside Auto mode: rejected because it would weaken the manual motion interlock contract.
- Set `ManualMode = true` only for the move request: rejected because it would hide the real machine mode from backend validation.
- Move directly through `IEquipmentController`: rejected because it would skip the Application motion use case and command history boundary.

## Consequences

- Inspection timeline now reports Load Recipe, Safety, Start Sequence, Move To Camera, and Grab Image as executable steps.
- Motion command history captures the internal sequence move with a parent Run Inspection correlation ID parameter.
- Missing or invalid Recipe documents and missing Camera Teaching points stop the sequence before motion or grab.
- Vision algorithms, judge, overlay rendering, and result persistence remain follow-up slices.

## Requirement Coverage

- FR-100/FR-102: Recipe Camera Teaching point is consumed by inspection motion.
- FR-121/FR-122: Active Recipe metadata and JSON document are both required before inspection execution.
- FR-140: Camera grab uses Recipe camera settings after motion completes.
- FR-180/FR-181: Sequence advances through Move To Camera and reports step status.
- FR-182: Cancellation still flows through the active use case token.
- NFR-004: Internal motion includes the parent Run Inspection correlation ID.
- NFR-006: Document/motion failures are converted into explicit result states.
- NFR-TEST-001: Interlock, Application, and simulator tests cover the new path.

## Rollback

Remove `SequenceMoveToCamera`, restore `InspectionRunUseCase` to skip Move To Camera before grab, remove the added document/motion dependencies and tests, then delete this ADR.
