# ADR-0038: Offline Debug Metadata Re-inspect Comparison

Status: Accepted
Date: 2026-06-03

## Context

Offline Debug can list persisted inspection results, inspect artifacts, prepare a Re-inspect context, and safely open deterministic overlay/height-map evidence. ADR-0036 intentionally kept `Run Re-inspect` disabled until a dedicated replay runner existed.

The project still has no real equipment, and real camera/motion/vision sequence replay remains outside the current validation scope. However, FR-222 can progress without real hardware by adding an Application-level metadata comparison step that compares the selected historical result against the prepared Re-inspect context. This gives the operator a traceable previous-vs-replayed judgment panel while avoiding any claim that live sequence replay, customer image processing, or result persistence has been validated.

## Decision

- Add `IInspectionReinspectUseCase` in `VisionCell.Application`.
- Implement `InspectionReinspectUseCase` as a simulator/offline metadata comparison over `InspectionReinspectPreparation`.
- Enable `OfflineDebugViewModel.RunReinspectCommand` after preparation.
- Display comparison summary/detail in `OfflineDebugView`.
- Keep replayed result persistence out of scope for this slice.
- Do not call `InspectionRunUseCase` from Offline Debug.
- Do not run live camera, motion, PLC, or vision sequence paths.

2026-06-03 follow-up: ADR-0039 persists metadata comparison history in a separate table, ADR-0041 classifies source-image replay readiness, and ADR-0042 archives source frame artifacts for new inspection rows. This still does not persist a new replay `inspection_results` row or execute source-image replay.

## Consequences

- Offline Debug now has an operator-visible `Run Re-inspect` comparison result.
- The comparison is deterministic and CI-testable without real equipment.
- FR-222 advances from prepare-only to previous-vs-replayed metadata comparison.
- Full source-image replay execution, current-vs-historical replay execution, previous-vs-new persisted result records, and real equipment validation remain follow-up work. Current-vs-historical Recipe policy metadata is addressed by ADR-0040; source-image replay readiness is addressed by ADR-0041; source frame artifact archival is addressed by ADR-0042.

## Requirement Coverage

- FR-220/FR-221: Offline Debug continues to inspect historical result context and artifacts.
- FR-222: Offline Debug can execute a metadata comparison for a prepared Re-inspect context.
- NFR-001/NFR-002: WPF calls an Application use case and keeps sequence policy out of code-behind.
- NFR-006/NFR-009/NFR-TEST-001: The comparison boundary is testable and exposes explicit operator status.

## Rollback

Remove `IInspectionReinspectUseCase`, `InspectionReinspectUseCase`, comparison result bindings, new tests, DI registration, this ADR, and related documentation updates. Restore `Run Re-inspect` to the ADR-0036 disabled boundary.
