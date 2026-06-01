# ADR-0018: Deterministic 2D Inspection Engine

## Status

Accepted

## Context

Inspection can now load the active Recipe, move to the Recipe Camera Teaching point, and grab a correlated simulator frame. FR-160, FR-161, FR-163, and FR-180 require the next sequence slice to consume Recipe ROI and vision parameters, run a 2D inspection, and expose a Judge result without putting vision logic into WPF or equipment adapters.

## Decision

- Add `IVisionInspectionEngine` to `VisionCell.Vision` with a cancellable `InspectAsync` contract.
- Add WPF- and equipment-independent `VisionImageFrame`, `VisionRoi`, `VisionInspectionParameters`, `VisionInspectionRequest`, and `VisionInspectionResult` contracts.
- Implement `Deterministic2DInspectionEngine` as the Phase 1 baseline for Gray8 frames.
- Detect baseline 2D defects with deterministic rules:
  - Missing: ROI dark-area ratio exceeds the Recipe missing threshold.
  - Scratch: a dark horizontal or vertical line score exceeds the Recipe scratch threshold.
  - Offset: an isolated foreground centroid exceeds the Recipe offset tolerance.
- Inject the engine into `InspectionRunUseCase` after camera grab and before Judge.
- Treat `Judgment.Invalid` as a sequence failure, while `Judgment.Pass` and `Judgment.Fail` are successful Judge outcomes.
- Keep 3D inspection, overlay rendering, and result persistence as explicit follow-up slices.

## Alternatives Considered

- Put quick image checks in `InspectionRunUseCase`: rejected because Application should orchestrate rather than own vision algorithms.
- Use `CameraFrame` directly in `VisionCell.Vision`: rejected because Vision must not depend on equipment contracts.
- Delay Judge until persistence is available: rejected because operators need visible Pass/Fail sequence feedback before FR-200 result storage.

## Consequences

- `InspectionRunUseCase` now completes Load Recipe, Safety, Start Sequence, Move To Camera, Grab Image, Inspect 2D, and Judge.
- WPF timeline can show `Judge: Pass` or `Judge: Fail` from the Application result.
- `Persist Result` remains skipped until FR-200.
- The engine is deterministic and suitable for simulator and unit-test evidence, but it is not a production metrology algorithm.
- RecipeView default ROI is aligned with the simulator package area so newly created default Recipes can pass through the baseline 2D slice.

## Requirement Coverage

- FR-140/FR-141: Camera frames are converted into Vision frames after grab.
- FR-160/FR-161/FR-163: 2D Missing, Offset, and Scratch baseline defects are evaluated from Recipe ROI parameters.
- FR-180/FR-181: Inspection sequence advances through Inspect 2D and Judge timeline steps.
- FR-182: Cancellation still flows through the active inspection token and engine contract.
- NFR-004: The Run Inspection correlation ID is carried into the Vision request.
- NFR-006: Invalid ROI and unsupported inspection states surface explicit result messages.
- NFR-TEST-001: Vision and Application tests cover Pass, Missing, Scratch, Offset, invalid ROI, and invalid vision result handling.

## Rollback

Remove `IVisionInspectionEngine` and deterministic engine contracts, unregister the engine from App composition, restore `InspectionRunUseCase` to skip Inspect/Judge after camera grab, revert RecipeView default ROI changes, remove tests, and delete this ADR.
