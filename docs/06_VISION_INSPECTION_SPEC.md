# 06. Vision Inspection Specification

## 목표

실제 카메라/3D 센서 없이도 머신비전 검사장비의 핵심 흐름을 재현한다.

## Inspection Inputs

- Recipe
- Source image
- ROI definitions
- 2D algorithm params
- synthetic height map params
- optional AI/native engine config

## Defect Types

| Type | Dimension | Description |
|---|---|---|
| Missing | 2D | 부품 또는 영역 누락 |
| Offset | 2D | 기준 위치 대비 오프셋 초과 |
| Scratch | 2D | 표면 선형 결함 |
| Lift | 3D | 높이 과다, 들뜸 |
| Dent | 3D | 높이 부족, 함몰 |
| LeadBent | 3D | 리드/탭 휨 유사 결함 |
| Unknown | AI | classifier 확장 |

## 2D Algorithm Baseline

Phase 1 implementation: `Deterministic2DInspectionEngine` in `VisionCell.Vision`.

1. ROI crop
2. grayscale
3. blur
4. threshold/edge
5. contour detection
6. expected area/position comparison
7. defect bbox generation
8. overlay rendering

Current deterministic engine scope:

- Input: Gray8 `VisionImageFrame`, Recipe ROI list, and Recipe 2D parameters.
- Missing: dark-area ratio exceeds `MissingAreaThreshold`.
- Scratch: horizontal or vertical dark-line score exceeds `ScratchThreshold`.
- Offset: isolated foreground centroid offset exceeds `OffsetTolerancePx`.
- Invalid ROI: result is `Judgment.Invalid` and the inspection sequence stops before Judge.

## 3D Synthetic Height Map

Phase 1 implementation: `SyntheticHeightMapFactory` and `DeterministicHeightMapInspectionEngine` in `VisionCell.Vision`.

Representation:

```csharp
public sealed record HeightMap(
    int Width,
    int Height,
    float[] Values,
    string Unit);
```

Rules:

- expected height: recipe parameter
- tolerance: low/high
- ROI 평균/최대/gradient를 계산
- threshold 초과 시 Lift/Dent/LeadBent

Current deterministic engine scope:

- Input: synthetic `VisionHeightMap`, Recipe ROI list, and Recipe height parameters.
- Lift: ROI max height exceeds `ExpectedHeight + HeightToleranceHigh`.
- Dent: ROI min height is below `ExpectedHeight - HeightToleranceLow`.
- LeadBent: local adjacent height gradient exceeds derived lead-bent gradient tolerance.
- Invalid ROI or non-finite height-map value: result is `Judgment.Invalid` and the sequence stops before Judge.

## Inspection Result

```csharp
public sealed record InspectionResult(
    Guid Id,
    string LotId,
    string RecipeId,
    string RecipeVersion,
    Judgment Judgment,
    IReadOnlyList<Defect> Defects,
    TimeSpan CycleTime,
    string SourceImagePath,
    string OverlayImagePath,
    DateTimeOffset InspectedAt,
    CorrelationId CorrelationId);
```

## Overlay Requirements

- ROI rectangle
- defect bbox
- defect label
- score
- Pass/Fail banner
- timestamp/recipe optional text

## Native C++ Option

Native C++ is not required in Phase 1 but should remain possible.

Candidate CLI:

```powershell
VisionCell.NativeVision.exe inspect `
  --image assets\samples\images\pkg_normal.png `
  --recipe assets\recipes\pkg_memory_module.recipe.json `
  --output artifacts\inspection\result.json
```

Candidate result:

```json
{
  "judgment": "FAIL",
  "cycleTimeMs": 123,
  "defects": [
    { "type": "OFFSET", "score": 0.87, "bbox": [120, 90, 240, 160] }
  ],
  "overlayImage": "artifacts/inspection/result_overlay.png"
}
```

## AI Extension Option

Interface:

```csharp
public interface IDefectClassifier
{
    Task<DefectClassification> ClassifyAsync(ImageFrame frame, Roi roi, CancellationToken cancellationToken);
}
```

ONNX integration is P2. Placeholder must not block P0/P1.

## Acceptance Cases

| Case | Expected |
|---|---|
| normal image | PASS |
| missing ROI image | FAIL Missing |
| offset image | FAIL Offset |
| scratch image | FAIL Scratch |
| height lift map | FAIL Lift |
| invalid recipe | validation error |
| camera grab timeout | sequence fails with CAM-001 |

## Implementation status

- InspectionView has an active Recipe precheck surface backed by `IActiveRecipeContext`.
- Run Inspection is routed through `IInspectionRunUseCase`, which rejects missing, invalid, or unavailable active Recipe context before any machine-like sequence can start.
- `InspectionRunUseCase` loads the active Recipe document, reads the equipment snapshot, evaluates Run Inspection interlocks, submits a correlated `MachineCommandRequest`, executes `SequenceMoveToCamera`, and returns ordered timeline steps to WPF.
- The simulator can now transition from Manual to Auto mode through Dashboard once servo, homing, safety, camera, and I/O interlocks are satisfied.
- `InspectionRunUseCase` executes Grab Image through `ICameraDevice` after Move To Camera and surfaces the correlated simulator frame in InspectionView Last Grab.
- `VirtualCameraDevice` returns deterministic synthetic Gray8 frames and explicit timeout/failure/not-ready results for FR-140/FR-141 tests.
- `InspectionRunUseCase` converts the grabbed Gray8 frame and Recipe ROI parameters into `VisionInspectionRequest`, runs `IVisionInspectionEngine`, and records Inspect 2D plus Judge timeline states.
- `Deterministic2DInspectionEngine` covers Phase 1 Missing, Scratch, Offset, and invalid ROI decisions for simulator evidence.
- `InspectionRunUseCase` now creates a synthetic height map from the grabbed Gray8 frame, runs `IHeightMapInspectionEngine`, and records Inspect 3D before the final Judge.
- `DeterministicHeightMapInspectionEngine` covers Phase 1 Lift, Dent, LeadBent, invalid height-map, and invalid ROI decisions for FR-162 evidence.
- `InspectionRunUseCase` persists final Judge, defect summary, per-step timings, Recipe metadata, generated lot ID, source image URI, and 2D/3D defect records through `IInspectionResultRepository` after Judge succeeds.
- Stop Inspection requests cancellation for the active run token through `InspectionViewModel`.
- Overlay rendering and image/height-map artifact file generation remain follow-up work; the persisted overlay and height-map paths are nullable until that slice lands.
