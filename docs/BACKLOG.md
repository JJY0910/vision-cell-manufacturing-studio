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
- [ ] FR-004 command interlock matrix follow-up implementation
- [ ] Shell clock/status ticker injectable service
- [ ] Dashboard visual quality review at 1366x768 and 1920x1080
- [ ] Motion command timeout/cancellation hardening
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
Problem: FR-004 command enabled conditions and backend interlock validation are only represented at the connection/dashboard level; full Motion/Inspection/Recipe command matrices still need feature-specific implementation.
Proposed improvement: Add per-screen command state objects and backend validation tests for EStop, Door Open, Servo Off, Auto mode, and active recipe prerequisites.
Requirement impact: FR-004, FR-040, FR-041, FR-042, FR-083, FR-122, FR-180
Priority: P1

Date: 2026-06-01
Source: Phase 1 Shell status bar
Problem: Shell exposes a formatted clock value, but no UI timer has been introduced yet because the first phase focused on simulator state and navigation.
Proposed improvement: Add an injectable clock/status ticker service so Shell time and heartbeat age update without blocking the UI thread.
Requirement impact: FR-003, FR-021
Priority: P2
