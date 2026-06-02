# ADR-0034: RoiOverlayCanvas Boundary

Status: Accepted
Date: 2026-06-02

## Context

`ImageViewport` now provides a reusable surface for Inspection and Offline Debug images, but ROI and defect overlays still need a dedicated WPF boundary. The overlay control can affect operator interpretation of defect location, ROI selection, and future re-inspection behavior, so it should not be added as an incidental visual element.

The project must preserve MVVM boundaries:

- WPF code-behind must not contain inspection, file I/O, database, or hardware logic.
- Image artifact decoding remains in Application/Persistence/ViewModel paths already covered by Offline Debug preview work.
- Real camera, real panel scaling, and production operator validation are still not available in the current environment.

## Decision

Implement `RoiOverlayCanvas` in a later PR as a presentation-only shared WPF control under `src/VisionCell.App/Shared/Controls/`.

The control boundary is:

- Input is a ViewModel-projected overlay model, not Core/Persistence records directly.
- Overlay items use image-space coordinates: `X`, `Y`, `Width`, `Height`, optional `Label`, optional `ScoreText`, optional `State`.
- The control maps image-space rectangles onto the rendered viewport using the same image dimensions supplied by the ViewModel.
- Initial scope is read-only ROI/defect display for Inspection and Offline Debug.
- Edit, drag, resize, save-to-recipe, and teaching feedback flows require a separate ADR or explicit acceptance update.
- The control must expose no file I/O, SQLite, camera, inspection engine, or equipment command dependency.
- Code-behind may contain dependency properties and rendering math only; business decisions stay in ViewModels/Application services.

## Alternatives

- Draw overlays directly inside `InspectionView.xaml` and `OfflineDebugView.xaml`.
  - Rejected because it duplicates coordinate mapping and makes future visual QA harder.
- Put overlay records directly in Core domain objects.
  - Rejected because WPF rendering concerns should not leak into Core.
- Implement editable ROI drawing immediately.
  - Rejected for this slice because editing changes recipe workflow expectations and requires separate validation.

## Consequences

- The next implementation PR can stay UI-focused and testable with ViewModel projection tests.
- Offline Debug can later show persisted defect boxes without changing artifact loading behavior.
- Inspection can later show current run ROI/defect overlays without adding camera or inspection logic to WPF code-behind.
- Actual physical panel scaling, live camera alignment, and operator acceptance remain unvalidated until hardware/panel access exists.

## Requirement Impact

- FR-006: WPF UI QA and reusable controls.
- FR-140: Inspection image acquisition display surface.
- FR-160/FR-162: 2D/3D inspection defect visualization boundary.
- FR-221: Offline Debug artifact preview surface.
- NFR-009: Maintainable MVVM UI composition.
- NFR-010: Traceable validation and acceptance evidence.

## Rollback

If the overlay control causes layout or interpretation risk, remove the `RoiOverlayCanvas` usage from the affected views and keep `ImageViewport` as the plain image display surface. Since this ADR does not implement runtime behavior, rollback for this planning slice is documentation-only.
