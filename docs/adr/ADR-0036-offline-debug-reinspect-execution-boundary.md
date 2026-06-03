# ADR-0036: Offline Debug Re-inspect Execution Boundary

## Status

Accepted

2026-06-03 follow-up: ADR-0038 adds a simulator/offline metadata comparison use case for `Run Re-inspect`, ADR-0039 persists that metadata comparison history, ADR-0040 surfaces current-vs-historical Recipe policy metadata, and ADR-0041 classifies source-image replay readiness. This ADR's live replay boundary still applies to source-image replay execution, current-vs-historical replay execution, new inspection-result replay persistence, and real camera/motion/vision sequence execution.

## Context

Offline Debug can list persisted inspection result rows, read artifact metadata, preview deterministic BMP artifacts, and safely open overlay/height-map files. The current `InspectionReinspectPreparation` record only carries a minimal source result identity, which is enough to show that preparation happened but not enough for an operator to understand what would be replayed or why execution is unavailable.

FR-222 requires Re-inspect with the current or historical Recipe and comparison against the previous judgment. However, real replay has additional policy that is not implemented yet: recipe version resolution, lot naming, artifact/source-frame availability, sequence runner ownership, equipment state requirements, and real hardware validation boundaries.

## Decision

- Expand `InspectionReinspectPreparation` into a richer Application-layer structure that carries:
  - source result identity and correlation ID
  - lot, Recipe ID/version, previous judgment, previous cycle time, and defect count
  - source image, overlay artifact, and height-map artifact paths
  - preparation timestamp
  - explicit run availability and disabled reason
- Keep actual re-inspection execution disabled until a dedicated replay runner/use case is implemented.
- Add Offline Debug UI distinction between `Prepare Re-inspect` and `Run Re-inspect`.
- `Run Re-inspect` remains command-bound and operator-visible, but disabled with a clear reason.
- Do not route Offline Debug directly to `InspectionRunUseCase` yet. `InspectionRunUseCase.RunAsync` currently owns the live active Recipe and machine sequence path, not historical replay policy.

## Alternatives Considered

- Call `InspectionRunUseCase.RunAsync` directly from Offline Debug: rejected because it would use the active Recipe and live sequence path without tying the run to the selected historical result or comparison policy.
- Hide `Run Re-inspect` until implementation: rejected because operators need to see that preparation is available but execution remains intentionally blocked.
- Store prepared re-inspect state in SQLite immediately: rejected because the current boundary is UI/Application preparation only; persistence belongs with the future replay result workflow.

## Consequences

- Offline Debug can create a traceable, operator-readable re-inspect request context without overstating execution readiness.
- Tests can verify selected-result mapping, disabled execution reason, and UI command separation.
- Actual replay execution, previous-vs-new judgment comparison, and result persistence remain follow-up work.

## Requirement Links

- FR-200: Persisted inspection results remain the source for Offline Debug.
- FR-220/FR-221: Offline Debug can inspect result rows and artifacts.
- FR-222: Re-inspect is advanced to a prepared request boundary, while execution remains documented as unvalidated.
- NFR-004/NFR-006/NFR-008/NFR-009: Failure/disabled states remain explicit and path policy remains outside WPF code-behind.

## Rollback

Restore the prior `InspectionReinspectPreparation` shape, remove `RunReinspectCommand` and the related Offline Debug UI/status bindings, remove the tests, and revert this ADR plus related documentation updates.
