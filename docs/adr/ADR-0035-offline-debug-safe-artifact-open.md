# ADR-0035: Offline Debug Safe Artifact Open Boundary

## Status

Accepted

## Context

Offline Debug can list persisted inspection results, read artifact metadata, decode deterministic BMP previews, and prepare a Re-inspect context. Operators may still need to open the underlying overlay or height-map evidence file in an external viewer during support/debug work.

ADR-0030 explicitly rejected direct OS shell opening for the preview slice because launching files needs a separate path policy and operator confirmation. The implementation must preserve the existing safety posture:

- WPF must not resolve arbitrary file-system paths directly.
- Stored artifact paths are relative display paths under the configured artifact root.
- Unsafe traversal, rooted paths, missing files, and unavailable files must remain operator-visible.
- Opening an artifact is an operator action, not automatic preview behavior.
- Actual re-inspection execution remains a separate workflow.

## Decision

Implement safe artifact opening in a later PR behind an Application-facing boundary.

The implementation boundary is:

- Add an Application record/result shape for artifact open preparation, for example `InspectionArtifactOpenRequest` and `InspectionArtifactOpenResult`.
- Reuse the same relative artifact path safety policy as `IInspectionArtifactReader.ReadMetadataAsync`.
- The Persistence implementation may resolve a safe absolute path, but WPF receives only a status/result message unless a platform opener service is explicitly injected.
- Add an App-layer operator confirmation service call before opening an artifact in the OS shell.
- Add separate Offline Debug commands for overlay and height-map artifact open actions.
- Do not open files automatically during result selection or preview loading.
- Do not support rooted paths, `..`, arbitrary customer directories, or arbitrary file types in this slice.
- Do not execute Re-inspect from the open action.

## Alternatives Considered

- Let WPF call `Process.Start` on the displayed path.
  - Rejected because WPF would own path resolution and traversal risk.
- Expose absolute paths directly to the ViewModel.
  - Rejected because absolute local paths are environment-specific and should not become UI state.
- Treat preview loading as sufficient artifact viewing forever.
  - Rejected because support workflows often need a file handoff, but that handoff needs explicit safety and confirmation.

## Consequences

- The next implementation PR can add artifact open commands without weakening path traversal protection.
- Offline Debug can remain useful for support workflows while keeping operator intent explicit.
- Tests must cover available, missing, unsafe, not-recorded, unavailable, and confirmation-declined paths.
- Real customer image formats, network shares, and external viewer availability remain unvalidated until a field environment exists.

## Requirement Coverage

- FR-221/FR-222: Offline Debug artifact viewing can expand from in-app preview to safe external open.
- FR-260: Inspection evidence can be handed to support/debug workflows.
- NFR-006: Open failures remain operator-visible.
- NFR-008: Path traversal and arbitrary absolute path opening stay rejected.
- NFR-009: WPF remains MVVM-only and does not own filesystem policy.
- NFR-010: Operator confirmation is required before launching an external viewer.

## Rollback

If the open workflow is considered unsafe or unnecessary, keep Offline Debug on in-app preview only, remove the open commands and Application/Persistence boundary types, and leave this ADR as rejected or superseded.
