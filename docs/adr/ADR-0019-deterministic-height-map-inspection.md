# ADR-0019: Deterministic Height-Map Inspection

## Status

Accepted

## Context

Inspection now runs deterministic 2D inspection and reaches Judge, but the 3D timeline step still reports skipped. FR-162 requires synthetic height-map inspection for Lift, Dent, and LeadBent-like defects using Recipe height parameters. The implementation must keep Vision independent from WPF, equipment, SQLite, and file-system concerns.

## Decision

- Add `VisionHeightMap`, `HeightMapInspectionParameters`, `HeightMapInspectionRequest`, and `IHeightMapInspectionEngine` to `VisionCell.Vision`.
- Implement `DeterministicHeightMapInspectionEngine` for Phase 1 synthetic 3D inspection.
- Add `SyntheticHeightMapFactory` to create deterministic height maps from Gray8 camera frames and Recipe expected height.
- Execute Inspect 3D in `InspectionRunUseCase` after Inspect 2D.
- Continue to run 3D when 2D returns `Judgment.Fail`; stop only when either engine returns `Judgment.Invalid` or throws.
- Compute final Judge from both 2D and 3D results: any Fail result makes the Judge message Fail.

## Alternatives Considered

- Keep 3D skipped until a real depth sensor contract exists: rejected because FR-162 explicitly allows synthetic height-map inspection.
- Reuse the 2D request type for height maps: rejected because 2D frames and 3D maps have different units and parameter semantics.
- Generate height maps in `InspectionRunUseCase`: rejected because synthetic map generation is Vision behavior, not Application orchestration.

## Consequences

- The inspection timeline now executes Inspect 3D and Judge after camera grab.
- `InspectionRunResult` carries both 2D `VisionResult` and 3D `HeightMapResult`.
- Default simulator runs should pass 3D because the generated synthetic height-map variation stays within Recipe height tolerances.
- The 3D engine is deterministic simulator evidence, not production metrology.
- Overlay generation and SQLite result persistence remain follow-up slices.

## Requirement Coverage

- FR-162: Synthetic height-map inspection detects Lift, Dent, and LeadBent-like defects.
- FR-180/FR-181: Auto inspection sequence advances through Inspect 3D and Judge timeline steps.
- FR-182: Cancellation flows through the active inspection token and height-map engine contract.
- NFR-004: The Run Inspection correlation ID is carried into the height-map inspection request.
- NFR-006: Invalid height maps and invalid ROI boundaries surface explicit failure messages.
- NFR-TEST-001: Vision and Application tests cover pass/fail/invalid 3D paths and final Judge aggregation.

## Rollback

Remove the height-map contracts, `DeterministicHeightMapInspectionEngine`, and `SyntheticHeightMapFactory`; unregister them from App composition; restore `InspectionRunUseCase` to skip Inspect 3D; remove tests and this ADR.
