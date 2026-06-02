# ADR-0022: Offline Debug Result Browser

## Status

Accepted

## Context

Inspection results, defects, and artifact paths are now persisted through FR-200. Offline Debug still used a placeholder view, so operators had no WPF surface to load historical result rows or inspect the saved overlay and height-map artifact paths. The first Offline Debug slice should stay read-only and reuse the existing Application result reader boundary.

## Decision

- Inject `IInspectionResultReader` into `OfflineDebugViewModel`.
- Add a refresh command that loads the latest inspection result rows without blocking the UI thread.
- Show result KPIs, a recent result list, selected result metadata, artifact paths, and defect rows in `OfflineDebugView`.
- Keep the workflow read-only for this slice; re-inspection and file opening are separate follow-up features.
- Surface empty, cancellation, and repository failure states through `StatusText`.

## Alternatives Considered

- Query SQLite directly from WPF: rejected because App must consume Application-layer ports rather than persistence details.
- Open artifact files from the first slice: deferred because safe file launching and missing-file handling should be designed as a separate UI command boundary.
- Add re-inspection immediately: deferred because it needs parameter replay and image loading semantics beyond result browsing.

## Consequences

- Offline Debug can now load historical inspection results from SQLite through the same reader used by future reporting/debug workflows.
- The selected result exposes source, overlay, and height-map paths but does not open or render those files yet.
- The UI remains MVVM-only; code-behind stays limited to view initialization.

## Requirement Coverage

- FR-202: A result detail surface now shows result metadata, artifact paths, timing, and defect list.
- FR-220/FR-221: Offline Debug can load past results and display overlay/height-map path evidence.
- FR-200: The UI consumes persisted SQLite result records through `IInspectionResultReader`.
- NFR-004: Correlation ID is visible in selected result detail.
- NFR-006: Empty, cancelled, and failed refresh states are operator-visible.
- NFR-TEST-001: App tests cover loaded, empty, and reader failure states.

## Rollback

Restore the Offline Debug placeholder ViewModel/View, remove the reader injection and tests, and remove this ADR and related docs.
