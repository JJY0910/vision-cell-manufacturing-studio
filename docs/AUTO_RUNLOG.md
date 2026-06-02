# Auto Run Log

## 2026-06-01 11:49 - PR #1
- Branch: `feature/fr-004-command-interlock`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/1
- Squash/Merge commit: `85fcec7bc21278b2c16f38e2cd52e84a1c28d347`
- Scope: Implemented the FR-004 command interlock state model across Core/Application/Dashboard/Simulator validation.
- Files changed: 22 files, including `docs/COMMAND_INTERLOCK_MATRIX.md`, Core interlock models, Application interlock service, Dashboard command availability state, Simulator validation, and related tests.
- Validation: GitHub Actions `build-test` completed successfully.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Feature-specific Motion/Inspection/Recipe command handlers still need to consume the command availability model.
- Next selected work: `feature/fr-060-motion-command-hardening` for motion command timeout, cancellation, and reject result hardening.

## 2026-06-01 13:37 - PR #2
- Branch: `feature/fr-060-motion-command-hardening`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/2
- Squash/Merge commit: `167400a910237d1d0e602ab1ba460049d15cf8b9`
- Scope: Hardened simulator motion command results for Servo/Home/Jog/Move Absolute/Stop success, rejected, timeout, and cancellation paths.
- Files changed: 7 files, including `VirtualEquipmentController`, `MachineCommandResult`, motion/interlock docs, backlog, and equipment tests.
- Validation: Local Debug/Release restore, build, and test passed with 38 tests. GitHub Actions `build-test` passed in 1m19s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Motion commands are not yet orchestrated through an Application use case, and history is not yet persisted to `motion_command_history`.
- Next selected work: `feature/fr-063-motion-usecase-history` for Application motion use case and history repository port.

## 2026-06-01 13:52 - PR #3
- Branch: `feature/fr-063-motion-usecase-history`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/3
- Squash/Merge commit: `daecf30b36a47366e7735c2f09867847a7f58205`
- Scope: Added Application-layer motion command orchestration, request/result correlation normalization, and history repository port.
- Files changed: 11 files, including ADR-0001, Application motion use case/DTOs/history port, Core DB error code, docs, and Application tests.
- Validation: Local Debug/Release restore, build, and test passed with 43 tests. GitHub Actions `build-test` passed in 1m22s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: SQLite repository and MotionView history binding are still pending.
- Next selected work: `feature/fr-069-motion-history-sqlite` for SQLite motion command history repository and idempotent schema bootstrap.

## 2026-06-01 14:04 - PR #4
- Branch: `feature/fr-069-motion-history-sqlite`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/4
- Squash/Merge commit: `c93a366f778d0a4746207e32c1ea88d94231cf8b`
- Scope: Added SQLite motion command history schema bootstrap, repository, readback tests, ADR-0002, and database/backlog docs.
- Files changed: 9 files, including Persistence SQLite factory/schema/repository, persistence tests, ADR-0002, DB spec, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 47 tests. GitHub Actions `build-test` passed in 2m6s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: MotionView still needed a read-bound command history panel.
- Next selected work: `feature/fr-069-motion-history-view` for MotionView history reader binding.

## 2026-06-01 14:17 - PR #5
- Branch: `feature/fr-069-motion-history-view`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/5
- Squash/Merge commit: `53b2b20968020e90efb9f4ca5c6065011e37a251`
- Scope: Added Application/Persistence read path for recent motion command history and bound MotionView to SQLite-backed command records.
- Files changed: 8 files, including MotionView/MotionViewModel history state, history item view-model, Persistence read mapping, App DI, UI spec, backlog, and app/persistence tests.
- Validation: Local Debug/Release restore, build, and test passed with 49 tests. GitHub Actions `build-test` passed in 1m46s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: MotionView could display persisted command history but still needed operator controls to execute Servo/Home/Jog/Move/Stop through the Application use case.
- Next selected work: `codex/feature/fr-063-motion-command-controls` for MotionView operator command controls.

## 2026-06-01 14:31 - PR #6
- Branch: `codex/feature/fr-063-motion-command-controls`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/6
- Squash/Merge commit: `ba8714661b174d835dac620bd7f6021cbdd1d9d6`
- Scope: Added MotionView operator controls for Servo On/Off, Home All, Jog X +1, Move preset, Stop, snapshot refresh, and command correlation/status feedback through `IMotionCommandUseCase`.
- Files changed: 10 files, including the shared snapshot interlock context factory, MotionView/MotionViewModel command controls, App DI, app tests, UI/motion docs, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 52 tests. GitHub Actions `build-test` passed in 1m48s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Jog and Move Absolute still dispatch simulator-backed fixed parameters; typed operator-entered axis/target DTOs remain pending.
- Next selected work: `codex/feature/fr-060-motion-axis-grid` for MotionView axis position and soft-limit state display.

## 2026-06-01 14:45 - Local validation note
- Branch: `codex/feature/fr-060-motion-axis-grid`
- Scope: Local Windows Smart App Control blocked freshly generated Debug test assemblies for `VisionCell.Core.Tests` and `VisionCell.Persistence.Tests` with `0x800711C7`.
- Evidence: CodeIntegrity events 3033/3077 reported that Debug test DLLs did not meet Enterprise signing level requirements.
- Mitigation: Added `tests/Directory.Build.props` to optimize Debug test assemblies only; product project Debug builds remain unchanged.
- Requirement impact: FR-260 validation reliability; no runtime product behavior change.

## 2026-06-01 14:54 - PR #7
- Branch: `codex/feature/fr-060-motion-axis-grid`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/7
- Squash/Merge commit: `bf37c788d4e15f1141e2ff8be3eb635223947ebb`
- Scope: Added MotionView axis snapshot cards for position, target, moving/homed/servo state, alarm, and soft-limit range from the latest controller snapshot.
- Files changed: 7 files, including Motion axis status view-model, MotionView axis card UI, app tests, UI/motion docs, backlog, runlog, and Debug test validation props.
- Validation: Local Debug/Release restore, build, and test passed with 55 tests. GitHub Actions `build-test` passed in 1m27s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: MotionView still needed typed operator-entered jog and absolute move payloads instead of simulator presets.
- Next selected work: `codex/feature/fr-063-typed-motion-targets` for typed MotionView command payloads through the Application/controller boundary.

## 2026-06-01 15:12 - PR #8
- Branch: `codex/feature/fr-063-typed-motion-targets`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/8
- Squash/Merge commit: `627b780950b6a6e1ecebd27a9e55616e5c5cfcd5`
- Scope: Added typed Jog and Move Absolute target payloads through MotionView, Application use case, controller request boundary, simulator execution, and motion command history.
- Files changed: 21 files, including Motion command payload helpers, MotionView command inputs, simulator handling, SQLite axis extraction, tests, ADR-0003, specs, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 56 tests. GitHub Actions `build-test` passed in 1m55s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Motion profile/tolerance fields were still fixed by defaults and needed typed operator input.
- Next selected work: `codex/feature/fr-065-motion-profile-tolerance` for request-level velocity, acceleration, deceleration, jerk, and arrival tolerance payloads.

## 2026-06-01 15:22 - PR #9
- Branch: `codex/feature/fr-065-motion-profile-tolerance`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/9
- Squash/Merge commit: `ccc73ea001612b5913c6bbfbac63659b13f2e556`
- Scope: Added request-level Move Absolute velocity, acceleration, deceleration, jerk, and arrival tolerance payloads with MotionView inputs, simulator validation/application, tests, ADR-0004, specs, backlog, and runlog.
- Files changed: 14 files, including Motion command payloads, MotionView profile inputs, simulator profile application, app/equipment tests, ADR-0004, specs, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 58 tests. GitHub Actions `build-test` passed in 1m53s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Profile presets and recipe/teaching reuse remained open.
- Next selected work: `codex/feature/fr-065-motion-profile-presets` for built-in Fine/Standard/Fast profile presets and traceable preset payload names.

## 2026-06-01 15:32 - PR #10
- Branch: `codex/feature/fr-065-motion-profile-presets`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/10
- Squash/Merge commit: `a5be6fedf8613b8fcf9d7b2efb69c49e7cccf992`
- Scope: Added built-in Fine/Standard/Fast motion profile presets, MotionView preset selection, traceable `ProfilePreset` payloads, simulator validation, tests, ADR-0005, specs, backlog, and runlog.
- Files changed: 15 files, including profile preset model, MotionView selector binding, simulator result message/validation, app/equipment tests, ADR-0005, specs, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 60 tests. GitHub Actions `build-test` passed in 1m25s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Recipe-level profile persistence, teaching defaults, and per-axis overrides remain open.
- Next selected work: `codex/test/fr-065-motion-profile-preset-coverage` for Motion-level preset payload coverage and traceability doc cleanup.

## 2026-06-01 15:40 - PR #11
- Branch: `codex/test/fr-065-motion-profile-preset-coverage`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/11
- Squash/Merge commit: `7f3b1e162a70b6c58fbd64634f5b5b7c2f975b71`
- Scope: Added Motion-layer tests for built-in profile preset defaults and `AbsoluteMoveTarget` `ProfilePreset` payload preservation, plus traceability doc cleanup.
- Files changed: 3 files, including Motion tests, `docs/COMMAND_INTERLOCK_MATRIX.md`, and `docs/AUTO_RUNLOG.md`.
- Validation: Local Debug/Release restore, build, and test passed with 62 tests. GitHub Actions `build-test` passed in 1m39s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Teaching point domain, persistence, and Go To workflow remained open.
- Next selected work: `codex/feature/fr-100-teaching-point-model` for Teaching Point role/position/tolerance validation.

## 2026-06-01 15:49 - PR #12
- Branch: `codex/feature/fr-100-teaching-point-model`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/12
- Squash/Merge commit: `79446098e4591b43b7c283198c9c29d302e04c35`
- Scope: Added Motion-layer Teaching Point role, position, tolerance, validation issue, and creation result models with soft-limit and tolerance tests.
- Files changed: 10 files, including `src/VisionCell.Motion/Teaching/*`, Motion tests, Teaching spec, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 69 tests. GitHub Actions `build-test` passed in 1m45s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Application save/go-to boundary, SQLite persistence, and WPF TeachingView binding remained open.
- Next selected work: `codex/feature/fr-100-teaching-usecase` for Save Current Position and Go To Application use case boundaries.

## 2026-06-01 15:59 - PR #13
- Branch: `codex/feature/fr-100-teaching-usecase`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/13
- Squash/Merge commit: `9ffa00eb5130be89c9372ba439ff0d5aef241e90`
- Scope: Added Application-layer Teaching Point Save Current Position and Go To use case boundaries, repository port, explicit result statuses, and tests.
- Files changed: 12 files, including `src/VisionCell.Application/Teaching/*`, Application tests, Teaching spec, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 75 tests. GitHub Actions `build-test` passed in 1m37s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: SQLite persistence and WPF TeachingView binding remained open.
- Next selected work: `codex/feature/fr-100-teaching-sqlite-repository` for concrete Teaching Point persistence.

## 2026-06-01 16:08 - PR #14
- Branch: `codex/feature/fr-100-teaching-sqlite-repository`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/14
- Squash/Merge commit: `b43c89815ec7f8bb3b2d13d057d89b2db38117e8`
- Scope: Added `teaching_points` SQLite schema bootstrap, `SqliteTeachingPointRepository`, and persistence coverage for ID lookup, case-insensitive name lookup, save/upsert, and schema migration count.
- Files changed: 7 files, including SQLite schema initializer, teaching repository, persistence tests, DB spec, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 79 tests. GitHub Actions `build-test` passed in 1m58s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: WPF TeachingView binding, App DI registration, edit history wiring, and recipe-level teaching ownership remain open.
- Next selected work: `codex/feature/fr-101-teaching-list-query` for list query support before WPF binding.

## 2026-06-01 16:16 - PR #15
- Branch: `codex/feature/fr-101-teaching-list-query`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/15
- Squash/Merge commit: `33912e8920d31dc83e54b13f46413865f3d59566`
- Scope: Added Teaching Point list query support to Application and Persistence contracts, SQLite updated-time ordering, and list query tests.
- Files changed: 10 files, including Application/Persistence teaching contracts, SQLite repository, tests, Teaching/DB specs, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 83 tests. GitHub Actions `build-test` passed in 1m44s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: WPF TeachingView binding and App DI registration remained open.
- Next selected work: `codex/feature/fr-101-teaching-view-binding` for operator-visible refresh/save/go-to controls.

## 2026-06-01 16:26 - PR #16
- Branch: `codex/feature/fr-101-teaching-view-binding`
- PR: https://github.com/JJY0910/vision-cell-manufacturing-studio/pull/16
- Squash/Merge commit: `c0fd3eeff079ae63c6a2e6837a7b2401461a82e0`
- Scope: Bound TeachingView to refresh, Save Current Position, and Go To selected point through the Application/Persistence boundary.
- Files changed: 9 files, including App DI, TeachingViewModel, TeachingView XAML, App tests, UI/Teaching docs, backlog, and runlog.
- Validation: Local Debug/Release restore, build, and test passed with 85 tests. GitHub Actions `build-test` passed in 1m33s.
- GitHub checks: `build-test` success; optional `codex-review` skipped.
- Risks: Teaching edit history, active recipe ownership, delete/update UI, and import/export remain open.
- Next selected work: `codex/feature/fr-104-teaching-history-contract` for edit history traceability contract.

## 2026-06-01 16:38 - In progress
- Branch: `codex/feature/fr-104-teaching-history-sqlite`
- Scope: Add SQLite schema bootstrap and repository coverage for append-only Teaching History rows.
- Validation target: Persistence tests, full Debug/Release build/test, GitHub Actions after PR creation.
- Risks: Use-case integration and active recipe ownership remain intentionally outside this small persistence slice.

## 2026-06-01 16:42 - In progress
- Branch: `codex/feature/fr-104-teaching-save-history`
- Scope: Wire `ITeachingHistoryRepository` into Save Current Position so successful Teaching Point creation records a Created history snapshot.
- Validation target: Application/App tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 33 tests; Debug/Release solution build and test passed with 93 tests; WPF hidden launch smoke passed.
- Risks: Update/delete history, active recipe ownership, and transactional point+history commits remain follow-up work.

## 2026-06-01 17:26 - In progress
- Branch: `codex/feature/fr-104-teaching-edit-delete-contract`
- Scope: Add ADR-backed Teaching Point update/delete Application contract, SQLite delete support, and history rows for Updated/Deleted actions.
- Validation target: Application/Persistence/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 38 tests; Persistence targeted tests passed with 15 tests; App targeted tests passed with 13 tests; Debug/Release solution build and test passed with 99 tests; WPF hidden launch smoke passed.
- Risks: WPF edit/delete controls, active recipe ownership, and transactional point+history commits remain follow-up work.

## 2026-06-01 17:35 - In progress
- Branch: `codex/feature/fr-104-teaching-view-edit-delete`
- Scope: Bind TeachingView selected-point update/delete buttons to the Application contract and refresh list state after each mutation.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 15 tests; Debug/Release solution build and test passed with 101 tests; WPF hidden launch smoke passed.
- Risks: Delete confirmation, selected-point history display, and active recipe ownership remain follow-up work.

## 2026-06-01 17:43 - In progress
- Branch: `codex/feature/fr-104-teaching-delete-confirmation`
- Scope: Add a testable WPF confirmation service and require confirmation before TeachingView deletes a selected point.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 16 tests; Debug/Release solution build and test passed with 102 tests; WPF hidden launch smoke passed.
- Risks: Selected-point history display and active recipe ownership remain follow-up work.

## 2026-06-01 17:52 - In progress
- Branch: `codex/feature/fr-104-teaching-history-display`
- Scope: Add selected-point Teaching history display backed by `ITeachingHistoryRepository.ListByPointAsync`.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 18 tests; Debug/Release solution build and test passed with 104 tests; WPF hidden launch smoke passed.
- Risks: Active recipe ownership remains follow-up work, so history `recipe_id` can still be null.

## 2026-06-01 18:04 - In progress
- Branch: `codex/feature/fr-120-teaching-recipe-context`
- Scope: Add ADR-backed optional recipe context to Teaching save/update/delete requests and history rows.
- Validation target: Application/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 39 tests; App targeted tests passed with 18 tests after a transient parallel build copy race; Debug/Release solution build and test passed with 105 tests; WPF hidden launch smoke passed.
- Risks: Full Recipe CRUD and app-settings based active recipe selection remain follow-up work.

## 2026-06-01 18:18 - In progress
- Branch: `codex/feature/fr-120-recipe-domain-contract`
- Scope: Add ADR-backed Application Recipe definition and validation contract for metadata, Teaching, camera, ROI, vision parameters, and sequence rules.
- Validation target: Application targeted tests, full Debug/Release build/test, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 45 tests; Debug/Release solution build and test passed with 111 tests; WPF hidden launch smoke passed.
- Risks: JSON persistence, SQLite indexing, active recipe settings, and RecipeView editing remain follow-up work.

## 2026-06-01 18:30 - In progress
- Branch: `codex/feature/fr-120-recipe-json-store`
- Scope: Add ADR-backed Recipe JSON document store port and Persistence implementation with validation-before-save and safe file-name generation.
- Validation target: Persistence targeted tests, full Debug/Release build/test, GitHub Actions after PR creation.
- Local validation: Persistence targeted tests passed with 20 tests; Debug/Release solution build and test passed with 116 tests; WPF hidden launch smoke passed.
- Risks: SQLite Recipe indexing, active recipe app settings, and RecipeView editing remain follow-up work.

## 2026-06-01 18:42 - In progress
- Branch: `codex/feature/fr-120-recipe-sqlite-index`
- Scope: Add ADR-backed SQLite Recipe index migration/repository for metadata list and lookup workflows.
- Validation target: Persistence targeted tests, full Debug/Release build/test, GitHub Actions after PR creation.
- Local validation: Persistence targeted tests passed with 25 tests; Debug/Release solution build and test passed with 121 tests; WPF hidden launch smoke passed.
- Risks: JSON document save and SQLite index update are not yet an atomic workflow; active recipe settings and RecipeView editing remain follow-up work.

## 2026-06-01 18:45 - In progress
- Branch: `codex/feature/fr-120-recipe-index-view`
- Scope: Register the SQLite Recipe index repository in App composition and bind RecipeView to refresh/display indexed Recipe metadata with active and validation state.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 21 tests; Debug/Release solution build and test passed with 124 tests; WPF hidden launch smoke passed.
- Risks: Browser is read-only; create/import/save/activate Recipe workflows remain follow-up work.

## 2026-06-01 19:00 - In progress
- Branch: `codex/feature/fr-120-recipe-library-save`
- Scope: Add ADR-backed Application Recipe library save use case that validates Recipe definitions, saves JSON documents, computes checksums, and upserts the SQLite Recipe index.
- Validation target: Application targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 49 tests; Debug/Release solution build and test passed with 128 tests; WPF hidden launch smoke passed.
- Risks: File-system document save and SQLite index update are coordinated but not atomic; active Recipe selection remains follow-up work.

## 2026-06-01 19:12 - In progress
- Branch: `codex/feature/fr-120-recipe-app-composition`
- Scope: Extract WPF App service registration and register Recipe document store plus Recipe library save use case with local app-data paths.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 22 tests; Debug/Release solution build and test passed with 129 tests; WPF hidden launch smoke passed.
- Risks: RecipeView save/import buttons remain follow-up work; this slice only wires composition.

## 2026-06-01 19:25 - In progress
- Branch: `codex/feature/fr-120-recipe-view-save`
- Scope: Bind RecipeView editor fields and Save Recipe command to `IRecipeLibraryUseCase`, then refresh/select the Recipe index row after save.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 25 tests; Debug/Release solution build and test passed with 132 tests; WPF hidden launch smoke passed.
- Risks: Save surface handles one Camera Teaching point and one ROI; active Recipe activation and multi-row editors remain follow-up work.

## 2026-06-01 19:40 - In progress
- Branch: `codex/feature/fr-122-active-recipe-index`
- Scope: Add an ADR-backed active Recipe query/switch contract to the Recipe index and SQLite repository using the existing `recipes.is_active` column.
- Validation target: Persistence targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 49 tests; Persistence targeted tests passed with 29 tests; App targeted tests passed with 25 tests; Debug/Release solution build and test passed with 136 tests; WPF hidden launch smoke passed.
- Risks: This slice only adds repository-level active state; RecipeView activation and app startup restore remain follow-up work.

## 2026-06-01 19:55 - In progress
- Branch: `codex/feature/fr-122-recipe-view-activate`
- Scope: Bind RecipeView Set Active command to the Recipe index active-state contract and refresh selected Recipe state after activation.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 29 tests; Debug/Release solution build and test passed with 140 tests; WPF hidden launch smoke passed.
- Risks: Active Recipe app startup restore and Teaching/inspection context consumption remain follow-up work.

## 2026-06-01 20:10 - In progress
- Branch: `codex/feature/fr-122-active-recipe-context`
- Scope: Add an ADR-backed Application active Recipe context provider over the Recipe index active row for Teaching and inspection consumers.
- Validation target: Application/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 53 tests; App targeted tests passed with 29 tests; Debug/Release solution build and test passed with 144 tests; WPF hidden launch smoke passed.
- Risks: This slice exposes active context but does not yet inject it into Teaching mutation requests or inspection sequence startup.

## 2026-06-01 20:25 - In progress
- Branch: `codex/feature/fr-122-teaching-active-context`
- Scope: Inject active Recipe context into TeachingViewModel so Save/Update/Delete Teaching history uses the selected active Recipe automatically.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 30 tests; Debug/Release solution build and test passed with 145 tests; WPF hidden launch smoke passed.
- Risks: Manual Recipe ID fallback remains for no-active situations; inspection startup still does not consume active context.

## 2026-06-01 20:40 - In progress
- Branch: `codex/feature/fr-180-inspection-active-precheck`
- Scope: Add InspectionView active Recipe precheck states and Run Inspection rejection for missing, invalid, or unavailable active Recipe context.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 34 tests; Debug/Release solution build and test passed with 149 tests. Windows application control temporarily blocked freshly built Debug Motion and Release Equipment test DLLs until those projects were rebuilt with `-p:Deterministic=false`; apphost exe launch was also blocked, so WPF smoke passed by launching `VisionCell.App.dll` directly through `dotnet` for 6 seconds.
- Risks: This slice does not execute camera grab, vision algorithms, or inspection result persistence yet.

## 2026-06-01 21:15 - In progress
- Branch: `codex/feature/fr-181-inspection-run-use-case`
- Scope: Add ADR-backed `IInspectionRunUseCase`, InspectionView timeline binding, and Stop Inspection cancellation for the active run token.
- Validation target: Application/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 57 tests; App targeted tests passed with 34 tests; Debug/Release solution build and test passed with 153 tests; WPF hidden launch smoke passed through `dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj -c Debug --no-build`.
- Risks: This slice submits a correlated Run Inspection command and reports camera/vision/judge/persist steps as skipped; it does not yet execute camera grab, algorithms, result persistence, or overlay rendering.

## 2026-06-01 21:35 - In progress
- Branch: `codex/feature/fr-180-simulator-auto-mode`
- Scope: Add ADR-backed simulator Enter Manual/Enter Auto commands, backend interlocks, Dashboard bindings, and mode transition tests.
- Validation target: Application/Equipment/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 59 tests; Equipment targeted tests passed with 18 tests; App targeted tests passed with 35 tests after rerunning App alone because the first parallel targeted run hit a build DLL file lock. Debug/Release solution build and test passed with 158 tests; WPF hidden launch smoke passed through `dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj -c Debug --no-build`.
- Risks: Enter Auto does not require active Recipe by design; Run Inspection still enforces active Recipe separately. Camera/vision/result persistence remains follow-up.

## 2026-06-01 22:05 - In progress
- Branch: `codex/feature/fr-141-camera-grab-simulator`
- Scope: Add ADR-backed camera grab contracts, `VirtualCameraDevice`, InspectionRunUseCase Grab Image execution, and InspectionView Last Grab rendering.
- Validation target: Application/Equipment/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 60 tests; Equipment targeted tests passed with 20 tests; App targeted tests passed with 35 tests. Debug/Release solution build and test passed with 161 tests; WPF hidden launch smoke passed through `dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj -c Debug --no-build`. Initial parallel targeted test run hit a shared Debug DLL file lock and was rerun serially.
- Risks: Synthetic frame is Gray8 only; Move To Camera, 2D/3D algorithms, judge, overlay, and result persistence remain follow-up work.

## 2026-06-01 22:25 - In progress
- Branch: `codex/feature/fr-180-recipe-camera-move`
- Scope: Load active Recipe documents during inspection, add internal `SequenceMoveToCamera`, move to the Recipe Camera Teaching point, and use Recipe camera settings for grab.
- Validation target: Application/Equipment/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 63 tests; Equipment targeted tests passed with 21 tests; App targeted tests passed with 35 tests. Debug/Release solution build and test passed with 165 tests; WPF hidden launch smoke passed through `dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj -c Debug --no-build`.
- Risks: 2D/3D algorithms, judge, overlay, and result persistence remain follow-up work.

## 2026-06-01 22:50 - In progress
- Branch: `codex/feature/fr-160-deterministic-2d-inspection`
- Scope: Add ADR-backed `IVisionInspectionEngine`, deterministic Gray8 Missing/Scratch/Offset checks, InspectionRunUseCase Inspect 2D execution, and Judge Pass/Fail timeline state.
- Validation target: Vision/Application/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Vision targeted tests passed with 5 tests; Application targeted tests passed with 64 tests; App targeted tests passed with 35 tests. Debug/Release solution build and test passed with 170 tests; WPF hidden launch smoke passed through `dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj -c Debug --no-build`. Initial parallel targeted run hit a shared Debug DLL file lock and was rerun serially.
- Risks: Engine is a deterministic simulator baseline, not production metrology; 3D inspection, overlay rendering, and result persistence remain follow-up work.

## 2026-06-01 23:20 - In progress
- Branch: `codex/feature/fr-170-deterministic-3d-inspection`
- Scope: Add ADR-backed synthetic height-map generation, deterministic Lift/Dent/LeadBent checks, InspectionRunUseCase Inspect 3D execution, and 2D/3D Judge aggregation.
- Validation target: Vision/Application/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Vision targeted tests passed with 10 tests; Application targeted tests passed with 66 tests; App targeted tests passed with 35 tests. Debug/Release solution build and test passed with 177 tests; WPF hidden launch smoke passed through `dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj -c Debug --no-build`.
- Risks: Height-map inspection is deterministic simulator evidence, not production metrology; overlay rendering and result persistence remain follow-up work.

## 2026-06-01 23:45 - In progress
- Branch: `codex/feature/fr-200-inspection-result-persistence`
- Scope: Add ADR-backed SQLite inspection result persistence for Judge, defects, timing, Recipe metadata, lot ID, correlation ID, and generated source image URI.
- Validation target: Application/Persistence/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 67 tests; Persistence targeted tests passed with 31 tests; App targeted tests passed with 35 tests. Debug/Release solution build and test passed with 180 tests; WPF hidden launch smoke passed through `dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj -c Debug --no-build`.
- Risks: Overlay and height-map artifact file generation remain follow-up work; persisted overlay/height-map paths are nullable in this slice.

## 2026-06-02 00:15 - In progress
- Branch: `codex/feature/fr-200-inspection-artifacts`
- Scope: Add ADR-backed inspection artifact writer contract and file-system BMP writer for overlay and height-map evidence during Persist Result.
- Validation target: Application/Persistence/App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 68 tests; Persistence targeted tests passed with 33 tests; App targeted tests passed with 35 tests. Debug/Release solution build and test passed with 183 tests; WPF hidden launch smoke passed through `dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj -c Debug --no-build`.
- Risks: BMP artifacts are deterministic evidence files and not final rich viewport rendering; PNG/export packaging remains future polish.

## 2026-06-02 00:35 - In progress
- Branch: `codex/feature/fr-221-offline-debug-results`
- Scope: Add ADR-backed Offline Debug result browser over `IInspectionResultReader`, including result KPIs, selected result metadata, artifact paths, and defect rows.
- Validation target: App targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: App targeted tests passed with 38 tests. Debug/Release solution build and test passed with 186 tests; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This slice displays artifact paths only; safe artifact preview/open commands and re-inspection remain follow-up work.

## 2026-06-02 09:45 - In progress
- Branch: `feature/fr-060-motion-command-hardening`
- Scope: Preserve `MachineCommandRequest.CorrelationId` in simulator motion/controller backend results across success, rejected, timeout, cancelled, and Stop paths.
- Validation target: Equipment targeted tests, full Debug/Release build/test, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Equipment targeted tests passed with 26 tests; Debug/Release solution build and test passed with 193 tests; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This slice hardens simulator/backend traceability; richer operator-facing motion history filtering remains follow-up work.

## 2026-06-02 10:14 - In progress
- Branch: `feature/application-equipment-use-cases`
- Scope: Move Dashboard equipment connect/disconnect/refresh/Manual/Auto orchestration behind Application `IEquipmentDashboardUseCase`, keeping WPF focused on state binding.
- Validation target: Application/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 72 tests; App targeted tests passed with 38 tests; Debug/Release solution build and test passed with 195 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This slice cleans Dashboard equipment command orchestration only; Motion, Teaching, Recipe, and Inspection view-model cleanup remain follow-up work.

## 2026-06-02 10:31 - In progress
- Branch: `feature/fr-063-motion-application-boundary`
- Scope: Move MotionView snapshot refresh and command availability behind Application `IMotionPanelUseCase`, while keeping command execution in `IMotionCommandUseCase`.
- Validation target: Application/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 75 tests; App targeted tests passed with 40 tests; Debug/Release solution build and test passed with 198 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: Motion input parsing remains in WPF by design for this slice; Teaching, Recipe, and Inspection view-model cleanup remain follow-up work.

## 2026-06-02 10:46 - In progress
- Branch: `feature/fr-100-teaching-goto-application-boundary`
- Scope: Move Teaching Go To snapshot retrieval and interlock context creation into Application `ITeachingPointUseCase`, removing direct equipment snapshot access from TeachingViewModel.
- Validation target: Application/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 76 tests; App targeted tests passed with 40 tests; Debug/Release solution build and test passed with 199 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This changes the `TeachingPointGoToRequest` contract; Teaching history query cleanup remains follow-up work.

## 2026-06-02 11:00 - In progress
- Branch: `feature/fr-100-teaching-history-usecase-boundary`
- Scope: Move Teaching selected-point history reads from direct WPF repository access into Application `ITeachingPointUseCase.ListHistoryAsync`.
- Validation target: Application/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 77 tests; App targeted tests passed with 40 tests; Debug/Release solution build and test passed with 202 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This slice keeps WPF history row formatting and selection state; Recipe and Inspection view-model cleanup remain follow-up work.

## 2026-06-02 11:24 - In progress
- Branch: `feature/fr-120-recipe-library-usecase-boundary`
- Scope: Move RecipeView recent-list and activate flows behind Application `IRecipeLibraryUseCase`, removing direct Recipe index repository orchestration from WPF.
- Validation target: Application/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, artifact scan, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 81 tests; App targeted tests passed with 40 tests. Debug/Release solution build and test passed with 206 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This slice keeps WPF editor parsing and Recipe row formatting in the ViewModel; Inspection view-model cleanup remains follow-up work.

## 2026-06-02 12:53 - In progress
- Branch: `feature/fr-180-inspection-precheck-usecase-boundary`
- Scope: Move InspectionView active Recipe precheck behind Application `IInspectionRunUseCase`, removing direct active Recipe context injection from WPF.
- Validation target: Application/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, artifact scan, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Application targeted tests passed with 82 tests; App targeted tests passed with 41 tests. Debug/Release solution build and test passed with 208 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This slice keeps WPF image-source creation and sequence row formatting in the ViewModel; richer inspection image preview/export polish remains follow-up work.

## 2026-06-02 13:06 - In progress
- Branch: `feature/fr-221-offline-artifact-metadata`
- Scope: Add Application/Persistence artifact metadata reader and bind Offline Debug artifact availability status for overlay and height-map paths.
- Validation target: Persistence/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, artifact scan, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Persistence targeted tests passed with 35 tests; App targeted tests passed with 41 tests. Debug/Release solution build and test passed with 210 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This slice reports artifact metadata only; image preview rendering, safe file opening, parameter replay, and re-inspection remain follow-up work.

## 2026-06-02 14:41 - In progress
- Branch: `feature/fr-221-offline-artifact-preview`
- Scope: Add safe Application/Persistence artifact preview loading for deterministic BMP overlay/height-map files and add Offline Debug Re-inspect preparation state.
- Validation target: Persistence/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, artifact scan, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Debug/Release solution build and test passed with 211 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: Preview supports current deterministic BMP artifacts only; safe file opening, parameter replay, actual re-inspection execution, and real hardware validation remain follow-up work.

## 2026-06-02 15:05 - In progress
- Branch: `feature/fr-184-alarm-recovery-center`
- Scope: Add Alarm / Fault / Recovery Center domain, Application recorder/use case, SQLite alarm repository, WPF AlarmView, and simulated Motion/Camera/Inspection failure recording.
- Validation target: Core/Application/Persistence/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, artifact scan, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Core targeted tests passed with 4 tests; Application targeted tests passed with 83 tests; Persistence targeted tests passed with 38 tests; App targeted tests passed with 43 tests. Debug/Release solution build and test passed with 218 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: Alarm records are generated from simulator/Application failure paths only; no real PLC, motion controller, camera, fieldbus, or safety relay alarm source is validated.

## 2026-06-02 15:28 - Local validation passed
- Branch: `feature/fr-020-hardware-adapter-boundary`
- Scope: Add Hardware Adapter Boundary contracts for future Motion/Camera/PLC I/O adapters and document the non-implemented real hardware integration plan.
- Validation target: Equipment targeted tests, full Debug/Release build/test, static blocking/code-behind checks, artifact scan, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Equipment targeted tests passed with 28 tests. Debug/Release solution build and test passed with 220 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: This PR defines adapter contracts only; no real `RealEquipmentController`, vendor SDK, PLC protocol, fieldbus, camera trigger, or safety relay validation is performed.

## 2026-06-02 16:02 - Local validation passed
- Branch: `feature/fr-080-io-monitor-fault-injection`
- Scope: Add simulator I/O Monitor and Fault Injection baseline for EStop, Door, AirPressure, Vacuum, CameraReady, ServoAlarm, interlock blocking, and alarm recorder integration through Application use cases.
- Validation target: Core/Equipment/Application/App targeted tests, full Debug/Release build/test, static blocking/code-behind checks, artifact scan, WPF launch smoke, GitHub Actions after PR creation.
- Local validation: Core targeted tests passed with 4 tests; Equipment targeted tests passed with 31 tests; Application targeted tests passed with 84 tests; App targeted tests passed with 44 tests. Debug/Release solution build and test passed with 225 tests; static blocking/code-behind/artifact checks passed; WPF hidden launch smoke passed by starting `VisionCell.App.exe` for 5 seconds.
- Risks: Fault injection is simulator-only; no real PLC, safety relay, EStop circuit, door switch, air/vacuum sensor, camera-ready line, servo drive alarm, or fieldbus path is validated.

## 2026-06-02 16:12 - In progress
- Branch: `feature/fr-006-ui-qa-hmi-polish`
- Scope: Add a local WPF HMI polish pass for Dashboard, Motion, Teaching, Recipe, Inspection, and Alarm screens with wrapping command forms, shared HMI table/input styles, explicit table scrollbars, command tooltips, and InspectionView scrolling.
- Validation target: App targeted tests, full Debug/Release build/test, static blocking/code-behind/artifact scans, WPF launch smoke, GitHub Actions after PR creation.
- Risks: This slice improves local WPF layout reachability only; no actual shop-floor display, touch panel, live hardware, or production operator shift validation is performed.
