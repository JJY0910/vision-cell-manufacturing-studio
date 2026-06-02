# FR-006 FR-140 FR-221: RoiOverlayCanvas Boundary

## Requirement IDs

FR-006 FR-140 FR-160 FR-162 FR-221 NFR-009 NFR-010

## Goal

Implement a read-only shared `RoiOverlayCanvas` for Inspection and Offline Debug image surfaces, following ADR-0034.

## Acceptance Criteria

- [x] Control is placed under `src/VisionCell.App/Shared/Controls/`.
- [x] Control consumes ViewModel-projected overlay items, not persistence records directly.
- [x] Initial implementation is read-only; no edit/drag/resize/save workflow.
- [x] WPF code-behind contains no business logic, file I/O, database, camera, or equipment command dependency.
- [x] ViewModel tests cover overlay projection from inspection/offline debug data.
- [ ] Debug/Release solution build and tests pass on Windows.
- [ ] PR summary includes requirement coverage and explicitly states real camera/panel validation limits.

## Codex Prompt

```text
Read AGENTS.md and ADR-0034. Implement read-only RoiOverlayCanvas in one PR-sized change. Keep MVVM boundaries, add ViewModel projection tests, run Debug/Release build/test, and document validation limits.
```
