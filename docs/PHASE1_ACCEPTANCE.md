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
- [ ] Dashboard polish review is complete on a real display.

## Simulator

- [x] Simulator supports Connect.
- [x] Simulator supports Disconnect.
- [x] Simulator exposes snapshot state for Safety, Axis, I/O, Camera, Alarm, and Timestamp.
- [x] Hardware-like commands return explicit result status.
- [x] Hardware-like commands support timeout and cancellation.
- [ ] Motion command timeout/cancellation paths are fully implemented beyond connection baseline.

## Tests

- [x] Core tests exist.
- [x] Motion tests exist.
- [x] Equipment tests exist.
- [x] App ViewModel tests exist.
- [ ] P0/P1 coverage is complete for Motion, I/O, Safety, Recipe, and Inspection.

## CI and Git

- [x] GitHub Actions CI uses `windows-latest`.
- [x] CI restores, builds, and tests the solution in Release.
- [x] `.gitignore` excludes WPF/.NET/Visual Studio artifacts.
- [x] Git repository has been initialized locally.
- [ ] GitHub remote is connected.
- [ ] `main` and `develop` have been pushed to GitHub.

## SDK

- [x] `global.json` targets .NET 8 LTS deterministically.
- [ ] Local machine has .NET 8 SDK installed.
- [ ] Full local restore/build/test passes with .NET 8 SDK.
