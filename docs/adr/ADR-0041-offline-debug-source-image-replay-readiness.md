# ADR-0041: Offline Debug Source Image Replay Readiness

## Status

Accepted

## Context

Offline Debug can browse historical inspection results, open overlay/height-map artifacts, prepare a Re-inspect context, compare metadata, persist metadata comparison history, and show active-vs-historical Recipe policy metadata. The remaining FR-222 gap is true source-image replay, where the historical source pixels would be loaded and inspected again under an explicit current or historical Recipe policy.

Persisted `inspection_results.source_image_path` may contain legacy `camera-frame://...` references or, after ADR-0042, generated `.source.bmp` artifact paths. Camera-frame references are useful for correlation, but they do not prove that raw source pixels are archived or readable for replay.

## Decision

- Add `IInspectionReinspectSourceImageReadinessUseCase` in the Application layer.
- Classify source image references during `Prepare Re-inspect` without opening files or executing replay.
- Report operator-visible states for:
  - camera-frame references where raw source pixels are not archived
  - missing source image references
  - unsupported URI schemes
  - parent-traversal path references
  - relative source path candidates that still lack a source-image artifact reader
  - archived source BMP artifacts when metadata is available through `IInspectionArtifactReader`
- Bind the readiness summary/detail into `OfflineDebugView` through `OfflineDebugViewModel`.
- Keep `Run Re-inspect` limited to metadata comparison until a source-image replay runner is implemented and validated.

## Alternatives Considered

- Treat `camera-frame://...` as replay-ready: rejected because it records correlation metadata, not archived pixels.
- Let WPF parse or validate source-image paths directly: rejected because WPF should remain MVVM presentation and should not own replay policy.
- Use overlay or height-map artifacts as source replay input: rejected because those are evidence/debug artifacts, not the original source frame contract.

## Consequences

- Operators can see why source-image replay is unavailable for the selected historical result.
- The Re-inspect preparation surface now distinguishes metadata comparison readiness from source replay readiness.
- ADR-0042 adds source pixel archival for newly generated rows, but customer image format loading, replay execution, and new `inspection_results` replay persistence remain follow-up work.

## Requirement Links

- FR-220/FR-221: Offline Debug result and artifact review remains read-only and traceable.
- FR-222: Re-inspect preparation now includes source-image replay readiness state.
- FR-240/NFR-009: Real hardware and live replay validation remain explicitly out of scope.
- NFR-001/NFR-008/NFR-TEST-001: Policy stays outside code-behind, rejects unsafe traversal references, and is covered by tests.

## Rollback

Remove `IInspectionReinspectSourceImageReadinessUseCase`, its App registration, Offline Debug bindings, tests, this ADR, and the related documentation updates.
