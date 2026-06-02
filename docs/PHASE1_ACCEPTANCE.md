# Phase 1 Acceptance

## Shell and MVVM

- [x] `ShellWindow` provides top status bar, left navigation, main workspace, and bottom event log.
- [x] Navigation is driven by ViewModel command binding.
- [x] WPF code-behind is limited to `InitializeComponent`.
- [x] Shared design tokens exist in `Themes/DesignTokens.xaml`.
- [ ] Full visual QA pass at 1366x768 and 1920x1080 is complete.

## Dashboard

- [x] Dashboard shows connection status.
- [x] Dashboard shows Machine Mode and Alarm status.
- [x] Dashboard shows Axis X/Y/Z/Theta state.
- [x] Dashboard shows I/O bit state.
- [x] Dashboard shows recent system events.
- [x] Dashboard exposes command interlock availability baseline.
- [x] Dashboard equipment command orchestration flows through an Application use case.
- [ ] Dashboard polish review is complete on a real display.

## Motion

- [x] Motion snapshot refresh and command availability flow through an Application use case.
- [x] Motion command execution remains traceable through Application command history.

## Teaching

- [x] Teaching Go To snapshot/interlock orchestration flows through an Application use case.
- [x] Teaching selected-point history reads flow through an Application use case.

## Recipe

- [x] Recipe list, save, and activation workflows flow through an Application use case.
- [x] RecipeView avoids direct Recipe index repository orchestration.

## Inspection

- [x] Inspection active Recipe precheck flows through the Inspection Application use case.
- [x] Inspection run orchestration and timeline state flow through the Inspection Application use case.

## Offline Debug

- [x] Offline Debug reads result rows through an Application result reader.
- [x] Offline Debug reads artifact availability metadata through an Application artifact reader.

## Simulator

- [x] Simulator supports Connect.
- [x] Simulator supports Disconnect.
- [x] Simulator exposes snapshot state for Safety, Axis, I/O, Camera, Alarm, and Timestamp.
- [x] Hardware-like commands return explicit result status.
- [x] Hardware-like commands support timeout and cancellation.
- [x] Backend command entry can reject disabled commands with code/message.
- [x] Motion command timeout/cancellation paths are implemented beyond connection baseline with correlated success/rejected/timeout/cancelled/stop results.

## Tests

- [x] Core tests exist.
- [x] Motion tests exist.
- [x] Equipment tests exist.
- [x] App ViewModel tests exist.
- [x] Application command interlock tests exist.
- [ ] P0/P1 coverage is complete for Motion, I/O, Safety, Recipe, and Inspection.

## CI and Git

- [x] GitHub Actions CI uses `windows-latest`.
- [x] CI restores, builds, and tests the solution in Release.
- [x] `.gitignore` excludes WPF/.NET/Visual Studio artifacts.
- [x] Git repository has been initialized locally.
- [x] GitHub remote is connected.
- [x] `main` and `develop` have been pushed to GitHub.

## SDK

- [x] `global.json` targets .NET 8 LTS deterministically.
- [x] Local machine has .NET 8 SDK installed.
- [x] Full local restore/build/test passes with .NET 8 SDK.
