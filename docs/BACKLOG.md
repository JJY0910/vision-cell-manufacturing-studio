# Backlog

## P0 Implementation Backlog

- [ ] FR-001 WPF Shell layout
- [ ] FR-020 Equipment connect/disconnect
- [ ] FR-040 Safety interlock baseline
- [ ] FR-060 Axis state display
- [ ] FR-061 Axis Home
- [ ] FR-062 Axis Jog
- [ ] FR-100 Teaching point save/go-to
- [ ] FR-120 Recipe CRUD
- [ ] FR-140 Camera simulator grab
- [ ] FR-160 2D inspection baseline
- [ ] FR-180 Inspection sequence
- [ ] FR-200 SQLite result logging

## P1 Quality Backlog

- [ ] Offline Debug Station
- [ ] Synthetic 3D height map inspection
- [ ] Recipe version history
- [ ] Failure injection panel
- [ ] Motion command history
- [ ] CSV report export
- [ ] Error code catalog
- [ ] FR-004 command interlock baseline implemented; requires future visual QA and hardware adapter validation
- [ ] Shell clock/status ticker injectable service
- [ ] Dashboard visual quality review at 1366x768 and 1920x1080
- [ ] MotionView command history binding after SQLite motion command history repository
- [ ] Offline Debug Station remains out of Phase 1 implementation scope

## Codex-discovered Improvements

Codex must append discovered improvements here with:

```text
Date:
Source:
Problem:
Proposed improvement:
Requirement impact:
Priority:
```

Date: 2026-06-01
Source: Phase 1 WPF Shell / Dashboard implementation
Problem: FR-004 command enabled conditions now have a Core/Application/Dashboard/backend baseline, but feature-specific Motion/Inspection/Recipe command handlers and hardware adapter validation are still pending.
Proposed improvement: Extend the baseline command state objects into Motion, Inspection, and Recipe views as those command handlers are implemented, and add adapter-level tests for EStop, Door Open, Servo Off, Auto mode, and active recipe prerequisites.
Requirement impact: FR-004, FR-040, FR-041, FR-042, FR-083, FR-122, FR-180
Priority: P1

Date: 2026-06-01
Source: Phase 1 Shell status bar
Problem: Shell exposes a formatted clock value, but no UI timer has been introduced yet because the first phase focused on simulator state and navigation.
Proposed improvement: Add an injectable clock/status ticker service so Shell time and heartbeat age update without blocking the UI thread.
Requirement impact: FR-003, FR-021
Priority: P2

Date: 2026-06-01
Source: FR-060 motion command hardening
Problem: Simulator motion commands now return explicit success/rejected/timeout/cancelled results, but there is still no persisted motion command history table writer or MotionView log binding.
Proposed improvement: Add an application use case that records `motion_command_history` entries and exposes the latest command results in MotionView without placing business logic in WPF code-behind.
Requirement impact: FR-063, FR-064, FR-067, FR-069, FR-200, NFR-004
Priority: P1

Date: 2026-06-01
Source: FR-063 Application motion use case
Problem: The Application layer can now create traceable motion command requests and record them through a history port, but no SQLite repository or MotionView binding exists yet.
Proposed improvement: Implement the Persistence-layer SQLite `motion_command_history` repository with idempotent schema migration, then bind recent command history into MotionView through a view-model state object.
Requirement impact: FR-063, FR-069, FR-200, NFR-004
Priority: P1

Date: 2026-06-01
Source: FR-069 SQLite motion command history
Problem: SQLite motion command history persistence now stores request/result records, but no MotionView state reads and displays the latest rows yet.
Proposed improvement: Add an Application query use case or read port for recent motion command history and bind it to MotionView with refresh/error state.
Requirement impact: FR-063, FR-069, FR-200, NFR-004
Priority: P1
