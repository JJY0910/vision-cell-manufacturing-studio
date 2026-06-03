# 09. Test Strategy

## Test Pyramid

```text
Many: Unit tests for Core/Application/Motion/Vision
Some: Integration tests for Persistence/Simulator
Few: WPF ViewModel tests and manual UI verification
```

## Test Projects

```text
tests/VisionCell.Core.Tests
tests/VisionCell.Application.Tests
tests/VisionCell.Equipment.Tests
tests/VisionCell.Motion.Tests
tests/VisionCell.Vision.Tests
tests/VisionCell.Persistence.Tests
tests/VisionCell.App.Tests
```

## Required Tests by Area

### Core

- Value object validation
- Error code mapping
- Machine mode transition rules

### Motion

- Home success
- Jog within limit
- Jog beyond soft limit rejected
- Move while servo off rejected
- Timeout result handling

### Equipment

- Connect/disconnect
- EStop blocks command
- Door open blocks Auto
- I/O toggle updates snapshot
- Fault injection updates EStop, Door, AirPressure, Vacuum, CameraReady, ServoAlarm, I/O forced state, and alarm snapshot

### Recipe

- Valid recipe passes
- Missing teaching fails
- ROI outside image fails
- Version validation

### Vision

- Normal image PASS
- Missing defect FAIL
- Offset defect FAIL
- Scratch defect FAIL
- Invalid ROI returns invalid judgment
- Height lift FAIL
- Height dent FAIL
- Height local gradient FAIL LeadBent
- Invalid height-map ROI returns invalid judgment
- Overlay output generated

### Application Sequence

- Inspection run Application boundary accepts active valid Recipe with ready interlocks and emits step timeline state.
- Inspection run rejects missing/invalid active Recipe before controller execution.
- Inspection run rejects missing/invalid active Recipe document before snapshot or controller execution.
- Inspection run rejects failed Run Inspection interlocks before controller execution.
- Inspection run dispatches Sequence Move To Camera from the Recipe Camera Teaching point and rejects missing Camera Teaching points.
- Stop/cancel path returns explicit cancelled result and cancelled/skipped step state.
- Camera grab success path returns a correlated synthetic frame and updates the Inspection timeline.
- Camera timeout failure path returns CAM-001 and skips downstream vision/judge/persist steps.
- 2D inspection success path runs the Vision engine, records Inspect 2D, and records Judge Pass/Fail timeline state.
- Invalid Vision result returns an explicit `VisionInspectionFailed` status and skips Judge/Persist.
- 3D inspection success path creates a synthetic height map, runs the height-map engine, records Inspect 3D, and aggregates final Judge from 2D and 3D results.
- Invalid height-map result returns an explicit `HeightMapInspectionFailed` status and skips Judge/Persist.
- Persist result success path saves Judge, defect, timing, Recipe, and correlation metadata after Judge.
- Persist result success path writes overlay and height-map artifact paths before SQLite save.
- Persist result failure path returns explicit `ResultPersistenceFailed` status and failed Persist Result timeline state for artifact or repository failures.
- Camera, 2D/3D inspection, controller start, and result persistence failure paths record alarm candidates through `IEquipmentAlarmRecorder`.
- App composition registers the virtual equipment runtime profile and rejects real-hardware runtime profiles until hardware validation exists.
- Real-hardware runtime profile rejection lists missing readiness evidence through `RealHardwareReadinessGate`.

### Persistence

- Migrations apply
- Insert/query event
- Insert/query inspection result
- Insert/query inspection defects
- Recent inspection result ordering and limit
- Inspection artifact writer creates overlay and height-map BMP files with relative paths
- Inspection artifact open preparation returns a safe resolved path only for available supported BMP artifacts under the configured root
- Inspection artifact open preparation rejects missing, not-recorded, traversal/rooted, unsupported-type, and unavailable artifact paths
- Repository handles relative path
- Insert/query/acknowledge equipment alarms

### WPF ViewModels

- Command enabled conditions
- State update after snapshot
- Error event displayed
- Navigation updates selected screen
- Priority HMI screens use the shared `CommandBar` title/command surface so Dashboard, Equipment, Motion, Teaching, Recipe, Inspection, and Alarm keep a consistent operator header.
- CommandBar action buttons use the shared HMI command style and avoid one-off height/margin values across Dashboard, Equipment, Motion, Teaching, Recipe, Inspection, Offline Debug, and Alarm.
- Module action buttons are checked so secondary Alarm, Recipe, and Teaching actions also use shared HMI command styles instead of local height-only styling.
- Equipment fault injection updates I/O monitor rows, active fault count, forced I/O count, fault state rows, disabled command reason, and event status
- Equipment fault injection buttons use the shared compact HMI command style and avoid one-off height/margin values.
- Equipment fault injection persists changed simulator I/O bit transitions with source, correlation ID, timestamp, and forced-state change metadata
- EquipmentView reads recent I/O transition history through the Application repository port and displays empty/non-empty states, status text, and a manual refresh action
- Bench PLC I/O validation uses `docs/BENCH_PLC_IO_VALIDATION_CHECKLIST.md` as a manual evidence gate for future adapter work; automated tests do not prove real PLC wiring, safety relay behavior, or output-write safety.
- Offline Debug result refresh shows loaded, empty, and repository failure states
- Offline Debug artifact open commands require confirmation, call the injected viewer service only after a ready preparation result, and do not open on missing/unsafe paths or declined confirmation
- Offline Debug Re-inspect preparation maps source result lot, Recipe, judgment, cycle time, defect, and artifact context; `Run Re-inspect` executes and persists an Application metadata comparison without live camera/motion/vision sequence replay
- Offline Debug Re-inspect readiness rows keep metadata comparison, source-image replay, Recipe policy, metadata history persistence, and real sequence execution boundaries visible in XAML/ViewModel tests.
- AlarmView refresh, acknowledgement state updates, disabled acknowledge reason, selected alarm recovery hint, recovery memo edit locking, and read-only recovery boundary rows

## Manual UI Acceptance

- Launch app
- Connect simulator
- Servo On
- Home axes
- Jog X/Y/Z/T
- Save teaching point
- Load recipe
- Start inspection
- See result overlay
- Open Offline Debug
- Re-inspect result
- Export CSV

## CI

GitHub Actions should run on `windows-latest`:

```powershell
dotnet restore .\VisionCell.sln
dotnet build .\VisionCell.sln -c Release --no-restore
dotnet test .\VisionCell.sln -c Release --no-build
```

## Local Windows App Control Compatibility

`tests/Directory.Build.props` enables optimization for Debug test assemblies only. This keeps local `dotnet test .\VisionCell.sln -c Debug --no-build` reliable on Windows Smart App Control / App Control for Business systems that may block freshly generated non-optimized test DLLs. Product project Debug builds remain unchanged.

## Quality Gates

- No build errors.
- P0/P1 tests pass.
- No skipped test without reason.
- No WPF business logic in code-behind.
- No blocking calls on UI thread.
