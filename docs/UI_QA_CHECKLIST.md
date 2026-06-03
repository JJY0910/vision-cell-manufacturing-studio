# UI QA Checklist

Last updated: 2026-06-03

Scope: local WPF layout and XAML regression coverage for the HMI theme/empty-state polish slice. This checklist does not claim real equipment, touch panel, multi-monitor, or operator acceptance validation.

## Dashboard

- [x] Top status chips are visible in the Shell top bar.
- [x] Connect, Disconnect, Manual, Auto, and Refresh commands wrap instead of clipping at constrained width.
- [x] Axis Snapshot remains in the Dashboard workspace.
- [x] I/O Snapshot uses the shared dark GridView style.
- [x] Event Log uses the shared dark DataGrid style.

## Equipment

- [x] Disconnected/fault injection state remains operator-visible through `InjectionStatus`.
- [x] Fault injection commands expose disabled reason/status via tooltip.
- [x] Fault State table uses the shared dark GridView style.
- [x] I/O Monitor uses an empty-state panel before a snapshot is available.
- [x] Fault Events uses an empty-state panel before event rows are available.
- [x] I/O Transitions exposes visible status and a refresh action.

## Motion

- [x] Motion command input groups wrap inside the workspace.
- [x] Profile, velocity, accel, decel, jerk, and tolerance fields remain in wrapping field groups.
- [x] Disabled motion commands expose interlock reason via tooltip.
- [x] Axis Snapshot uses an empty-state panel before an axis snapshot is available.
- [x] Recent Motion Commands uses an empty-state panel before history rows are available.

## Teaching

- [x] Active recipe context remains visible in the save-position panel.
- [x] Teaching point list uses the shared dark GridView style.
- [x] Selected point history uses the shared dark GridView style.
- [x] `Teaching Points` and point count are separated in a Grid header.
- [x] `Selected Point History` and history status are separated in a Grid header.

## Recipe

- [x] Editor fields remain grouped with shared Recipe editor field controls.
- [x] Save Recipe status remains visible through the command bar/status text.
- [x] Recipe Index uses an empty-state panel before records are loaded.
- [x] `Recipe Index` and status text are separated in a Grid header.
- [x] Active Recipe KPI remains visible.

## Inspection

- [x] Precheck status is visible in the KPI band.
- [x] Last Grab viewport remains visible.
- [x] Timeline/result area remains available below the KPI band.
- [x] Run/stop command buttons wrap in the command bar.

## Alarm

- [x] No alarm state is visible through an empty-state panel.
- [x] Alarm table uses the shared dark GridView style.
- [x] Acknowledge command remains bound through the ViewModel.
- [x] Acknowledge command exposes a disabled reason and the action memo editor locks when the selected alarm cannot be acknowledged.
- [x] Recovery action memo remains visible in the detail panel.

## Offline Debug

- [x] Result list uses the shared dark GridView style.
- [x] No-results state is visible through an empty-state panel.
- [x] Artifact preview surfaces remain available.
- [x] Open overlay, open height-map, load artifacts, and prepare re-inspect commands expose selection-required tooltips.
- [x] Defect rows use an empty-state panel when a selected result has no defects.
- [x] Re-inspect metadata comparison is visible; source-image replay and replay result persistence remain follow-up work.

## Reports

- [x] Reports is no longer blank.
- [x] Reports clearly states that CSV export and lot summary belong to FR-203/FR-204 follow-up work.

## Settings

- [x] Settings is no longer blank.
- [x] Settings clearly states simulator-only equipment, virtual-camera mode, and real hardware not connected.
- [x] Settings surfaces the read-only Real Hardware readiness gate and missing evidence list.

## General

- [x] DataGrid headers stay on the dark HMI palette.
- [x] GridView headers stay on the dark HMI palette.
- [x] Disabled button tooltips remain visible.
- [x] Navigation selected, hover, and focus states use distinct dark/accent styling.
- [x] No WPF code-behind business logic was added.
- [ ] Physical 1920x1080 HMI panel validation remains unverified.
- [ ] Physical 1366x768 HMI panel validation remains unverified.
- [ ] Touch operation remains unverified.
- [ ] Real equipment workflow remains unverified.
