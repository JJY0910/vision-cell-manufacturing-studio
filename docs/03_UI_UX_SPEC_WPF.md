# 03. WPF UI/UX Specification

## UI 방향

목표는 예쁜 웹 대시보드가 아니라, 실제 검사장비 HMI처럼 보이는 고밀도·고신뢰 UI다.

## Window

- Main window: `ShellWindow.xaml`
- 기본 해상도: 1920x1080
- 최소 해상도: 1366x768
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
| RoiOverlayCanvas | Shared/Controls/RoiOverlayCanvas.xaml | ROI drawing/selection |
| EventLogGrid | Shared/Controls/EventLogGrid.xaml | system event 표시 |
| ErrorBanner | Shared/Controls/ErrorBanner.xaml | alarm/error 표시 |
| CommandBar | Shared/Controls/CommandBar.xaml | 화면별 primary commands |

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
- Recipe clone/export controls and full multi-row Teaching/ROI editing remain follow-up work.

### InspectionView

- Start/Stop/Pause/Reset
- Sequence timeline
- Image viewer original/overlay
- Last Grab viewer displays the latest simulator camera frame after a successful Grab Image step.
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

### ReportsView

- Lot summary
- Defect Pareto
- Cycle time table
- CSV export

### SettingsView

- Paths
- Simulator latency/failure rate
- DB/log retention
- Theme
- Error code catalog

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
