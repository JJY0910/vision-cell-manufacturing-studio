# 02. System Architecture

## Architecture Style

- WPF MVVM + Clean Architecture-ish layered structure
- Simulator-first equipment abstraction
- UseCase 중심 Application layer
- Domain model은 UI/DB/Simulator에 독립
- SQLite persistence
- Optional native vision engine extension

## Solution Projects

```text
VisionCell.sln
├─ src/VisionCell.App
├─ src/VisionCell.Core
├─ src/VisionCell.Application
├─ src/VisionCell.Equipment
├─ src/VisionCell.Motion
├─ src/VisionCell.Vision
├─ src/VisionCell.Persistence
├─ src/VisionCell.Simulator
├─ src/VisionCell.Telemetry
└─ tests/*
```

## Dependency Direction

```text
VisionCell.App
  → VisionCell.Application
  → VisionCell.Core
  → VisionCell.Equipment abstractions
  → VisionCell.Motion abstractions
  → VisionCell.Vision abstractions
  → VisionCell.Persistence interfaces

VisionCell.Simulator implements Equipment/Motion/Camera interfaces.
VisionCell.Persistence implements repositories.
VisionCell.Telemetry implements logging/event sinks.
```

## Layer Responsibilities

### VisionCell.Core

- Value objects: AxisId, Position4D, RecipeId, LotId, CorrelationId
- Enums: MachineMode, AxisState, CommandStatus, Severity, Judgment
- Domain events and error codes
- Validation primitives

### VisionCell.Application

- UseCases: ConnectEquipment, HomeAxis, JogAxis, SaveTeachingPoint, RunInspectionSequence
- Recipe workflows: validate and save Recipe documents, update Recipe index metadata, resolve active Recipe context
- Sequence orchestration
- DTO mapping
- Application-level validation
- Cancellation/timeout coordination

### VisionCell.Equipment

- Controller/Camera/I/O abstractions
- Safety interlock abstraction
- Command request/response models

### VisionCell.Motion

- Axis model
- Motion profile
- Soft limit validation
- Teaching point model

### VisionCell.Vision

- ROI model
- Inspection algorithm contracts and deterministic Phase 1 2D engine
- Defect model
- Overlay generation
- Height map simulation and deterministic Phase 1 3D engine
- AI/native extension points

### VisionCell.Persistence

- SQLite connection factory
- Schema migration
- Repositories: Recipe, Result, Event, Teaching
- File path policy for image/result storage

### VisionCell.Simulator

- In-memory virtual controller
- Axis state simulation
- I/O state simulation
- Camera sample image provider
- Error injection

### VisionCell.App

- WPF Shell
- Views/ViewModels
- Design system
- Command binding
- User feedback
- App service composition for simulator, Application use cases, SQLite repositories, and Recipe document storage

## Command Model

Every equipment-like command uses:

```csharp
public sealed record MachineCommandRequest(
    string CommandName,
    CorrelationId CorrelationId,
    TimeSpan Timeout,
    DateTimeOffset RequestedAt,
    IReadOnlyDictionary<string, string> Parameters);
```

Motion command parameters use typed payload helpers in `VisionCell.Motion.Commands` so Application history, simulator execution, and future hardware adapters consume the same axis, target, profile preset, profile, and tolerance values.

Every command returns:

```csharp
public sealed record MachineCommandResult(
    CommandStatus Status,
    ErrorCode? ErrorCode,
    string Message,
    TimeSpan Elapsed,
    CorrelationId CorrelationId);
```

## Sequence Pipeline

```text
PreCheck
  ├─ Validate active recipe
  ├─ Validate safety interlock
  ├─ Validate equipment connected
  └─ Validate axis homed
MoveToTeachingPoint
GrabImage
Run2DInspection
Run3DInspection
Judge
PersistResult
PublishUiEvents
```

Current implementation status:

- `InspectionRunUseCase` owns the first Application-layer inspection sequence boundary.
- The use case validates active Recipe context, loads the Recipe document, evaluates Run Inspection interlocks, submits a correlated controller command, moves to the Recipe Camera Teaching point through `IMotionCommandUseCase`, and returns ordered timeline state for WPF binding.
- Dashboard exposes simulator Manual/Auto mode transitions through backend `CommandKind.EnterManualMode` and `CommandKind.EnterAutoMode` interlocks.
- Camera grab now flows through `ICameraDevice` and `VirtualCameraDevice`, using Recipe camera settings and returning a correlated synthetic Gray8 frame to the Inspection UI.
- 2D inspection now flows through `IVisionInspectionEngine` and `Deterministic2DInspectionEngine`, using Recipe ROI and 2D parameters before Judge.
- 3D inspection now flows through `SyntheticHeightMapFactory`, `IHeightMapInspectionEngine`, and `DeterministicHeightMapInspectionEngine`, using Recipe ROI and height parameters before Judge.
- Result persistence now flows through Application `IInspectionResultRepository` and Persistence `SqliteInspectionResultRepository`, storing Judge, defect, timing, Recipe, and correlation metadata after Judge.
- Artifact generation now flows through Application `IInspectionArtifactWriter` and Persistence `FileSystemInspectionArtifactWriter`, creating deterministic overlay and height-map BMP files during Persist Result.
- Offline Debug now reads persisted inspection results through Application `IInspectionResultReader` and displays recent result metadata, defect rows, correlation IDs, and artifact paths.
- Simulator motion commands now preserve `MachineCommandRequest.CorrelationId` across success, rejected, timeout, cancelled, and stop results.
- Rich live UI overlay rendering remains a separate follow-up slice.

## Error Handling Policy

- Domain validation returns explicit result object, not random exception.
- Infrastructure exception is caught in Application layer and converted to `SystemEvent` + `ErrorCode`.
- UI displays error banner and event log; it does not swallow errors silently.

## Data Flow

```text
User Button
→ ViewModel Command
→ Application UseCase
→ Equipment/Motion/Vision/Persistence Interface
→ Simulator/Repository Implementation
→ Domain/Application Result
→ ViewModel State Update
→ UI Binding
→ EventLog/SQLite
```

## Threading

- UI thread: View rendering and state binding only.
- Application layer: async use cases.
- Simulator: async delays to mimic hardware latency.
- Inspection: background Task or dedicated service.
- DB: async repository methods.

## Extension Points

| Extension | Interface | Future Implementation |
|---|---|---|
| Real controller | IEquipmentController | TCP/Serial/PLC driver |
| Real motion | IAxisController | Vendor SDK wrapper |
| Real camera | ICameraDevice | GigE/USB camera SDK |
| Native vision | IVisionInspectionEngine | C++ OpenCV DLL/CLI |
| Native height-map vision | IHeightMapInspectionEngine | 3D sensor SDK/OpenCV DLL |
| Inspection artifact storage | IInspectionArtifactWriter | PNG/export package writer |
| AI classifier | IDefectClassifier | ONNX Runtime |
