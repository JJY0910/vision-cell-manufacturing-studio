# ADR-0021: Inspection Artifact Generation

## Status

Accepted

## Context

FR-200 result persistence stores nullable overlay and height-map artifact paths, but the inspection sequence must create actual operator/debug artifacts before Offline Debug and report workflows can use those paths. The implementation must keep WPF free of file-writing behavior and keep Vision algorithms independent from path policy.

## Decision

- Add `IInspectionArtifactWriter` to `VisionCell.Application`.
- Add artifact DTOs for Gray8 source frames, height maps, ROIs, and defect records.
- Implement `FileSystemInspectionArtifactWriter` in `VisionCell.Persistence`.
- Render deterministic BMP artifacts:
  - overlay BMP from the Gray8 source frame, ROI rectangles, defect boxes, and pass/fail banner.
  - height-map BMP from normalized synthetic height-map values.
- Write artifacts under a configured local artifact root and return relative paths shaped like `inspection-artifacts/yyyyMMdd/{resultId}.overlay.bmp`.
- Execute artifact generation inside the Persist Result step before SQLite save so the persisted row contains artifact paths.

## Alternatives Considered

- Store artifact bytes in SQLite: rejected because image/debug artifacts are easier to inspect, export, and clean up as files.
- Generate overlays in WPF: rejected because artifact generation is part of inspection evidence persistence, not UI rendering.
- Add PNG encoding now: rejected to avoid adding a new imaging dependency; uncompressed BMP is deterministic and sufficient for the first persistence artifact slice.

## Consequences

- Successful inspection runs now populate `overlay_image_path` and `height_map_path`.
- 2026-06-03 follow-up: ADR-0042 also populates `source_image_path` with a generated source BMP artifact path for new inspection rows.
- Artifact writer failures are treated as Persist Result failures and do not silently accept an incomplete evidence record.
- The generated overlay is a deterministic evidence artifact, not the final rich HMI image viewport overlay.
- BMP files are larger than PNG; compression/export polish remains a follow-up for report packaging.

## Requirement Coverage

- FR-160/FR-162: 2D/3D defect evidence is visualized in generated artifacts.
- FR-180/FR-181: Persist Result now writes artifact evidence during the sequence.
- FR-200: SQLite result rows receive artifact paths backed by generated files.
- NFR-004: Result ID and correlation metadata flow into the artifact write request.
- NFR-008: The writer confines output under a configured root and stores relative paths only.
- NFR-TEST-001: Application and Persistence tests cover artifact success/failure, BMP generation, relative paths, ROI, and defect rendering.

## Rollback

Remove `IInspectionArtifactWriter`, unregister `FileSystemInspectionArtifactWriter`, restore `InspectionRunUseCase` to save nullable artifact paths, and remove the artifact tests and documentation.
