# ADR-0039: Offline Debug Re-inspect History Persistence

Status: Accepted
Date: 2026-06-03

## Context

ADR-0038 added an Offline Debug metadata comparison for a prepared Re-inspect context, but the result was shown only in the current WPF ViewModel session. This left the operator without a persisted audit trail for previous-vs-replayed metadata comparisons.

The project still has no real equipment or source-image replay runner. Persisting the metadata comparison must not imply that a live camera, motion, PLC, vision sequence, customer image replay execution, or new `inspection_results` row has been executed.

## Decision

- Add `IInspectionReinspectComparisonRepository` and `IInspectionReinspectComparisonReader` in the Application inspection boundary.
- Add SQLite table `inspection_reinspect_comparisons` through migration id `008_inspection_reinspect_comparisons`.
- Register `SqliteInspectionReinspectComparisonRepository` in WPF composition.
- Let `InspectionReinspectUseCase` persist metadata comparison results when a repository is configured.
- Add a read-only Re-inspect History section in Offline Debug.
- Keep source-image replay execution, current-vs-historical replay execution, and new inspection-result persistence out of scope.

## Consequences

- Offline Debug now has a persisted metadata comparison history that is CI-testable without real hardware.
- The history is stored separately from `inspection_results` so it does not claim a new inspection run was executed.
- Operators can distinguish a persisted metadata comparison from source-image replay readiness, unimplemented source-image replay execution, or real sequence execution.

## Requirement Coverage

- FR-220/FR-221: Offline Debug continues to inspect historical results and artifacts.
- FR-222: Re-inspect metadata comparisons are persisted and visible as history.
- FR-260: The change is traceable through this ADR, tests, and PR workflow.
- NFR-001/NFR-006/NFR-008/NFR-009/NFR-TEST-001: Persistence stays behind Application ports, SQLite schema is documented, WPF remains MVVM, and tests cover Application/Persistence/ViewModel/XAML behavior.

## Rollback

Remove the comparison repository ports, SQLite repository, migration/table initialization, WPF history bindings, tests, this ADR, and related documentation updates. `InspectionReinspectUseCase` can return to the ADR-0038 in-session-only comparison behavior.
