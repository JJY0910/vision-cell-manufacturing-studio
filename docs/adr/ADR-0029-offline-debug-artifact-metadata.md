# ADR-0029: Offline Debug Artifact Metadata

## Status

Accepted

## Context

Inspection result persistence stores overlay and height-map artifact paths, and Offline Debug can display those strings. Operators still have no quick signal that the referenced files exist, were rejected by the path safety policy, or are missing from local storage.

WPF must not perform direct file I/O for artifact checks. The same root and path traversal policy used by the artifact writer should be reused for read-only metadata checks.

## Decision

- Add `IInspectionArtifactReader` and `InspectionArtifactMetadata` contracts to the Application inspection boundary.
- Implement read-only metadata lookup in `FileSystemInspectionArtifactWriter`, which now also implements `IInspectionArtifactReader`.
- Resolve only relative artifact paths under the configured artifact root and reject rooted paths, parent traversal, and invalid path segments.
- Register `IInspectionArtifactReader` in App composition.
- Update `OfflineDebugViewModel` to load overlay and height-map metadata while refreshing result rows.
- Update `OfflineDebugView` to display artifact availability summary and selected artifact status.

## Alternatives Considered

- Let WPF check `File.Exists`: rejected because it would place file-system-specific behavior in the UI layer.
- Store artifact existence in SQLite during result save: rejected because files can be moved or removed after persistence, so Offline Debug needs live read metadata.
- Render artifact previews immediately: deferred to keep this slice read-only and focused on safe metadata.

## Consequences

- Offline Debug now shows available/missing/not-recorded/unsafe/unavailable artifact states.
- The artifact reader reuses the artifact root policy and does not expose absolute local paths.
- Persistence tests cover available, missing, not-recorded, and unsafe artifact metadata paths.
- App tests cover Offline Debug artifact status binding.

## Requirement Coverage

- FR-200/FR-220/FR-221: Offline Debug can inspect persisted result artifact references with live availability state.
- FR-260: Result evidence remains operator-visible in a support/debug workflow.
- NFR-001/NFR-002: WPF stays MVVM-only and avoids direct file-system checks.
- NFR-004/NFR-006: Missing or unsafe artifact states are surfaced to operators.
- NFR-SEC-001: Rooted paths and traversal attempts are rejected before file metadata access.
- NFR-TEST-001: Persistence and App tests cover the new artifact metadata path.

## Rollback

Remove `IInspectionArtifactReader` and metadata records, restore `FileSystemInspectionArtifactWriter` to writer-only behavior, remove Offline Debug artifact status bindings, delete the new tests and ADR, and revert related documentation updates.
