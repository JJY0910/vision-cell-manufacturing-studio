# 03. WPF UI/UX Specification

## UI 방향

목표는 예쁜 웹 대시보드가 아니라, 실제 검사장비 HMI처럼 보이는 고밀도·고신뢰 UI다.

## Window

- Main window: `ShellWindow.xaml`
- 기본 해상도: 1920x1080
- 최소 해상도: 1366x768
- Startup behavior: `WindowStartupLocation=CenterScreen`, `WindowState=Maximized`, `MinWidth=1366`, `MinHeight=768`.
- Layout: Grid with TopBar / SideNav / Workspace / EventLog

```text
┌──────────────────────────────────────────────────────────────────────┐
│ TopBar: Logo, Mode, Connection, Active Recipe, Alarm, Time            │
├───────────────┬──────────────────────────────────────────────────────┤
│ SideNav       │ Workspace                                            │
│ Dashboard     │                                                      │
│ Equipment     │                                                      │
│ Motion        │                                                      │
│ Teaching      │                                                      │
│ Recipe        │                                                      │
│ Inspection    │                                                      │
│ Alarm         │                                                      │
│ OfflineDebug  │                                                      │
│ Reports       │                                                      │
│ Settings      │                                                      │
├───────────────┴──────────────────────────────────────────────────────┤
│ Bottom Event Log                                                     │
└──────────────────────────────────────────────────────────────────────┘
```

## Design Tokens

File: `src/VisionCell.App/Themes/DesignTokens.xaml`

- Font family: Segoe UI
- Background: near-black equipment console or neutral gray
- Status colors:
  - Ready: Green
  - Warning: Amber
  - Alarm: Red
  - Disconnected: Gray
  - Moving/Inspecting: Blue
- Spacing tokens: 4, 8, 12, 16, 24, 32
- Corner radius: 6 or 8
- Button height: 36, primary action 44

## Reusable Controls

| Control | File | Purpose |
|---|---|---|
| StatusPill | Shared/Controls/StatusPill.xaml | Connected, Ready, Alarm 상태 표시 |
| KpiCard | Shared/Controls/KpiCard.xaml | Cycle time, yield, count 표시 |
| AxisCard | Shared/Controls/AxisCard.xaml | axis position/state/commands |
| IoBitIndicator | Shared/Controls/IoBitIndicator.xaml | I/O bit 표시 |
| SequenceTimeline | Shared/Controls/SequenceTimeline.xaml | 검사 step 표시 |
| ImageViewport | Shared/Controls/ImageViewport.xaml | 원본/overlay 이미지 표시 |
| RoiOverlayCanvas | Shared/Controls/RoiOverlayCanvas.cs | read-only ROI/defect overlay display |
| RecipeEditorField | Shared/Controls/RecipeEditorField.xaml | Recipe editor label/input field |
| EventLogGrid | Shared/Controls/EventLogGrid.xaml | system event 표시 |
| ErrorBanner | Shared/Controls/ErrorBanner.xaml | alarm/error 표시 |
| CommandBar | Shared/Controls/CommandBar.xaml | 화면별 primary commands |

Implementation status:

- `StatusPill` and `EventLogGrid` are implemented as shared controls.
- `KpiCard` is implemented and reused by Motion, Recipe, and Alarm summary bands.
- `AxisCard` is implemented and reused by Dashboard and Motion axis snapshots.
- `IoBitIndicator` is implemented and reused by Dashboard and Equipment I/O monitor rows.
- `SequenceTimeline` is implemented and reused by InspectionView sequence step display.
- `ImageViewport` is implemented and reused by Inspection and OfflineDebug image preview surfaces, including optional read-only overlay binding.
- `ErrorBanner` is implemented and reused by Alarm, Inspection, OfflineDebug, and Recipe alert status surfaces.
- `CommandBar` is implemented and reused by Dashboard, Equipment, Motion, Teaching, Recipe, Inspection, OfflineDebug, and Alarm screen command headers.
- CommandBar action buttons use the shared `Button.HmiCommand` style; screens may keep explicit `MinWidth` only for longer operator labels.
- Secondary module action buttons use `Button.HmiCommand.Compact` so editor/detail-panel actions keep the same disabled-tooltip and spacing behavior.
- `RoiOverlayCanvas` is implemented as a read-only shared overlay surface for ViewModel-projected image-space ROI/defect rectangles.
- `RecipeEditorField` is implemented and reused by the Recipe editor metadata, camera, Teaching, and ROI input fields.

## Screens

### DashboardView

File:

```text
src/VisionCell.App/Modules/Dashboard/Views/DashboardView.xaml
src/VisionCell.App/Modules/Dashboard/ViewModels/DashboardViewModel.cs
```

Sections:

- Equipment Overview
- Safety Summary
- Axis Snapshot
- I/O Snapshot
- Active Recipe
- Last Inspection Result
- Recent Events

Required visual:

```text
[Controller Connected] [Camera Ready] [Mode: Manual] [Alarm: None]
[KPI: Today Pass] [KPI: Fail] [KPI: Avg Cycle] [KPI: Last Result]
[Axis X/Y/Z/T cards]
[Safety Door/EStop/Servo/Air/Vacuum]
```

### EquipmentView

- Connect/Disconnect
- Heartbeat
- Simulator profile
- Failure injection
- Controller diagnostics

Implementation status:

- `EquipmentViewModel` refreshes the simulator equipment snapshot through `IEquipmentDashboardUseCase`.
- `EquipmentView` uses the shared `CommandBar` for refresh/snapshot status and displays controller mode, safety/interlock summary, camera/alarm status, active fault count, forced I/O count, I/O monitor rows, fault state rows, recent fault events, and recent read-only I/O transition history.
- Equipment fault-injection buttons use the shared compact HMI command style so EStop, Door, AirPressure, Vacuum, CameraReady, ServoAlarm, and Clear All controls keep consistent operator sizing and disabled tooltips.
- Simulator fault injection for EStop, Door, AirPressure, Vacuum, CameraReady, ServoAlarm, and Clear All flows through `IEquipmentFaultInjectionUseCase`; WPF does not call simulator internals directly.
- I/O transition history exposes a manual refresh action and visible latest-row status while remaining simulator-only.

### MotionView

- Axis position grid
- Servo On/Off
- Home all / Home axis
- Jog step selection
- Jog +/- buttons
- Move absolute
- Soft limit display
- Motion log
- Recent motion command history from `motion_command_history`
- Snapshot refresh and command status/correlation feedback
- Axis cards display position, target, motion state, homing, servo, alarm, and soft-limit range from the latest controller snapshot.
- Current implementation executes simulator-backed Servo On/Off, Home All, typed Jog +/- axis steps, typed Move Absolute X/Y/Z/Theta/profile preset/profile/tolerance targets, and Stop through `IMotionCommandUseCase`.

### TeachingView

- Current position panel
- Teaching point list
- Save current position
- Go to selected point
- Teaching role/tolerance
- Edit history

Implementation status:

- TeachingView binds to `TeachingViewModel` for refresh, Save Current Position, and Go To commands.
- The view shows point name, role, position, tolerance, updated time, memo, and save input fields for role/tolerance/memo.
- TeachingView supports selected-point edit/delete commands through the Application contract.
- TeachingView asks for confirmation before deleting a selected point.
- TeachingView shows recent selected-point history rows from `teaching_history`, including timestamp, action, recipe, and before/after JSON summary.
- TeachingView has an Active Recipe input that is written into Teaching history rows for save/update/delete until RecipeView owns active recipe selection.
- Full Recipe ownership controls remain follow-up work.

### RecipeView

- Recipe browser
- Metadata editor
- Teaching mapping
- ROI editor
- Vision params
- Validation result
- Version/history

Implementation status:

- RecipeView binds to `RecipeViewModel` for SQLite Recipe index refresh.
- The view shows indexed recipe id, version, product name, active state, validation state, updated time, checksum, document path, and validation summary.
- RecipeView has a metadata/camera/teaching/ROI editor and Save Recipe command backed by the Application Recipe library use case.
- RecipeView uses reusable `RecipeEditorField` controls for editor inputs so label, textbox styling, automation name, and tooltip behavior stay consistent.
- Recipe clone/export controls and full multi-row Teaching/ROI editing remain follow-up work.

### InspectionView

- Start/Stop/Pause/Reset
- Sequence timeline
- Image viewer original/overlay
- Last Grab viewer displays the latest simulator camera frame after a successful Grab Image step.
- Last Grab viewer can show read-only 2D/3D defect overlays projected from the accepted inspection run result.
- Current result and defect table
- Cycle time breakdown
- Lot summary

### OfflineDebugView

- Result search filters
- Result list
- Original/overlay viewer
- Historical params
- Re-inspect panel
- Current vs previous result comparison

Implementation status:

- `OfflineDebugViewModel` loads recent persisted inspection result rows through `IInspectionResultReader` and artifact availability/preview/open-preparation data through `IInspectionArtifactReader`.
- `OfflineDebugView` shows result count, pass/fail counts, defect total, recent result rows, selected result metadata, source/overlay/height-map paths, artifact availability status, overlay/height-map previews, safe operator-confirmed overlay/height-map open commands, read-only defect overlays, Re-inspect preparation status, metadata comparison results, and selected defect rows.
- The Re-inspect panel shows read-only readiness rows for metadata comparison, source-image replay, Recipe policy, replay persistence, and real sequence execution so unimplemented or unvalidated replay boundaries remain operator-visible.
- External artifact open commands require an injected confirmation service and artifact viewer service; WPF code-behind does not resolve paths or launch processes.
- `InspectionReinspectPreparation` carries source lot/Recipe/judgment/cycle/defect/artifact context for the selected result.
- Source-image replay, current-vs-historical Recipe policy, replay result persistence, and actual camera/motion/vision sequence execution remain follow-up work.

### AlarmView

- Alarm / Fault / Recovery records
- Code, severity, equipment area, message, correlation ID
- Occurred/acknowledged time
- Operator action memo
- Acknowledge command

Implementation status:

- `AlarmViewModel` loads recent persisted alarm rows through `IAlarmCenterUseCase`.
- `AlarmView` shows active/critical/acknowledged counts, recent alarm rows, selected alarm detail, protocol-spec recovery hint, and recovery action memo input.
- Acknowledge writes `acknowledgedAt` and `actionMemo` through the Application use case, exposes a disabled reason when no active alarm is selected or the selected alarm is already acknowledged, and locks action memo editing when the memo cannot be saved.
- Hardware reset remains separate from AlarmView acknowledgement.

### UI QA / HMI Polish

Implementation status:

- Dashboard, Motion, Teaching, Recipe, Inspection, and Alarm operator command buttons now expose tooltips for primary actions and refresh/acknowledge commands.
- Motion and Teaching command editors use wrapping field groups instead of fixed-width single-row grids so operator inputs remain reachable when the workspace is constrained.
- InspectionView now has vertical scrolling so sequence timeline and image evidence remain reachable in smaller windows.
- HMI GridView tables use a shared `ListView.HmiGrid` style with explicit horizontal and vertical scrollbars for long alarm, recipe, teaching, motion, and I/O rows.
- Shell top status uses compact status chips for current screen, mode, controller, active Recipe, and alarm summary.
- Shell startup/layout QA keeps the window maximized on launch, constrains the workspace with clipping/stretch behavior, gives the navigation rail its own vertical scroll area, and keeps Dashboard and Equipment refresh commands in shared `CommandBar` surfaces.
- Priority HMI CommandBar action buttons use the shared HMI command button style instead of one-off height/margin values.
- Alarm, Recipe, and Teaching secondary action buttons use shared compact HMI command styling rather than local height values.
- Equipment fault injection uses shared compact command buttons instead of one-off sizing, while preserving existing ViewModel command bindings.
- HMI theme polish keeps DataGrid and GridView headers on the dark HMI palette, enables tooltips on disabled command buttons, strengthens navigation hover/focus/selected contrast, and adds reusable empty-state panels for Motion, Teaching, Recipe, Equipment, Alarm, Offline Debug, Reports, and Settings surfaces.
- Bottom Event Log uses a shared dark HMI `DataGrid.HmiGrid` style with consistent headers, rows, and scrollbars.
- This pass is a local WPF layout quality slice. It does not claim final shop-floor monitor, touch-panel, or real equipment HMI acceptance.

### ReportsView

- Lot summary
- Defect Pareto
- Cycle time table
- CSV export

Implementation status:

- `ReportsView` currently shows an operator-readable scope state instead of a blank page.
- CSV export and lot summary remain the dedicated FR-203/FR-204 Reports MVP follow-up.

### SettingsView

- Paths
- Simulator latency/failure rate
- DB/log retention
- Theme
- Error code catalog

Implementation status:

- `SettingsView` currently shows read-only runtime scope for simulator-only equipment and virtual-camera mode instead of a blank page.
- `SettingsView` shows a read-only Real Hardware readiness gate with missing adapter/bench evidence surfaced from the same runtime guard that blocks `RealHardware` profile selection.
- Persisted path/profile/retention settings remain the dedicated FR-240 Settings MVP follow-up.

## UX Rules

- 위험 동작은 항상 disabled state와 backend validation 둘 다 있어야 한다.
- Primary action은 화면당 1~2개로 제한한다.
- 경고/알람은 EventLog + Banner 둘 다 표시한다.
- Auto sequence 중에는 Jog/Teaching/Recipe edit을 제한한다.
- 모든 수치 입력은 단위와 범위를 표시한다.
- 데이터 저장 성공/실패는 toast 또는 event로 표시한다.

## UI Acceptance Checklist

- [ ] 모든 화면은 SideNav에서 접근 가능하다.
- [ ] 화면 전환 시 상태가 유지된다.
- [ ] Dashboard에서 전체 장비 상태가 5초 안에 이해된다.
- [ ] MotionView에서 axis jog를 실수 없이 수행할 수 있다.
- [ ] TeachingView에서 현재 위치 저장 흐름이 명확하다.
- [ ] InspectionView에서 Pass/Fail과 defect 위치가 즉시 보인다.
- [ ] OfflineDebugView에서 과거 결과 재검사가 가능하다.
