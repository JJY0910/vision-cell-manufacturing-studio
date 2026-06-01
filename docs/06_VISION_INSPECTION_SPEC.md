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

1. ROI crop
2. grayscale
3. blur
4. threshold/edge
5. contour detection
6. expected area/position comparison
7. defect bbox generation
8. overlay rendering

## 3D Synthetic Height Map

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
- `InspectionRunUseCase` reads the equipment snapshot, evaluates Run Inspection interlocks, submits a correlated `MachineCommandRequest`, and returns ordered timeline steps to WPF.
- The simulator can now transition from Manual to Auto mode through Dashboard once servo, homing, safety, camera, and I/O interlocks are satisfied.
- `InspectionRunUseCase` executes Grab Image through `ICameraDevice` after controller acceptance and surfaces the correlated simulator frame in InspectionView Last Grab.
- `VirtualCameraDevice` returns deterministic synthetic Gray8 frames and explicit timeout/failure/not-ready results for FR-140/FR-141 tests.
- Stop Inspection requests cancellation for the active run token through `InspectionViewModel`.
- 2D/3D inspection execution, result persistence, and overlay rendering remain follow-up work and are currently reported as skipped timeline steps after camera grab.
