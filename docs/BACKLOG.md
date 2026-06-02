# Backlog

## P0 Implementation Backlog

- [ ] FR-001 WPF Shell layout
- [ ] FR-020 Equipment connect/disconnect (Dashboard Application use case baseline added; hardware adapter validation pending)
- [ ] FR-040 Safety interlock baseline
- [ ] FR-060 Axis state display
- [ ] FR-061 Axis Home
- [ ] FR-062 Axis Jog
- [ ] FR-100 Teaching point save/go-to (domain, Application use case, SQLite repository, list query, and WPF binding added; recipe ownership pending)
- [ ] FR-120 Recipe CRUD
- [x] FR-140 Camera simulator grab (synthetic Gray8 frame and Last Grab UI binding)
- [x] FR-160 2D inspection baseline
- [ ] FR-180 Inspection sequence
- [x] FR-200 SQLite result logging (Judge, defect, timing, Recipe metadata, and generated artifact paths persisted)

## P1 Quality Backlog

- [ ] Offline Debug Station
- [ ] Synthetic 3D height map inspection
- [ ] Recipe version history
- [ ] Failure injection panel
- [ ] Motion command history chart/export polish
- [ ] CSV report export
- [ ] Error code catalog
- [ ] FR-004 command interlock baseline implemented; requires future visual QA and hardware adapter validation
- [ ] Shell clock/status ticker injectable service
- [ ] Dashboard visual quality review at 1366x768 and 1920x1080
- [ ] Motion profile per-axis override and recipe reuse policy
- [ ] Offline Debug Station remains out of Phase 1 implementation scope

## Codex-discovered Improvements

Codex must append discovered improvements here with:

```text
Date:
Source:
Problem:
Proposed improvement:
Requirement impact:
Priority:
```

Date: 2026-06-01
Source: Phase 1 WPF Shell / Dashboard implementation
Problem: FR-004 command enabled conditions now have a Core/Application/Dashboard/backend baseline, but feature-specific Motion/Inspection/Recipe command handlers and hardware adapter validation are still pending.
Proposed improvement: Extend the baseline command state objects into Motion, Inspection, and Recipe views as those command handlers are implemented, and add adapter-level tests for EStop, Door Open, Servo Off, Auto mode, and active recipe prerequisites.
Requirement impact: FR-004, FR-040, FR-041, FR-042, FR-083, FR-122, FR-180
Priority: P1
Status: Dashboard equipment command availability and execution orchestration now flows through `IEquipmentDashboardUseCase`; feature-specific Motion/Inspection/Recipe handlers and adapter validation remain open.

Date: 2026-06-02
Source: Dashboard equipment Application use case
Problem: Dashboard connect/disconnect/refresh/mode commands now use an Application boundary, but Motion, Teaching, Recipe, and Inspection view models still need ongoing review for direct orchestration that should belong in Application use cases.
Proposed improvement: Continue view-model cleanup in small slices, starting with the highest-risk hardware-like command surfaces that need timeout, cancellation, structured events, and backend validation.
Requirement impact: FR-020, FR-021, FR-022, FR-061, FR-062, FR-100, FR-120, FR-180, NFR-001, NFR-002, NFR-004
Priority: P1
Status: MotionView snapshot refresh and command availability now flow through `IMotionPanelUseCase`; Teaching, Recipe, and Inspection cleanup remain open.

Date: 2026-06-01
Source: Phase 1 Shell status bar
Problem: Shell exposes a formatted clock value, but no UI timer has been introduced yet because the first phase focused on simulator state and navigation.
Proposed improvement: Add an injectable clock/status ticker service so Shell time and heartbeat age update without blocking the UI thread.
Requirement impact: FR-003, FR-021
Priority: P2

Date: 2026-06-01
Source: FR-060 motion command hardening
Problem: Simulator motion commands now return explicit success/rejected/timeout/cancelled results, but there is still no persisted motion command history table writer or MotionView log binding.
Proposed improvement: Add an application use case that records `motion_command_history` entries and exposes the latest command results in MotionView without placing business logic in WPF code-behind.
Requirement impact: FR-063, FR-064, FR-067, FR-069, FR-200, NFR-004
Priority: P1

Date: 2026-06-01
Source: FR-063 Application motion use case
Problem: The Application layer can now create traceable motion command requests and record them through a history port, but no SQLite repository or MotionView binding exists yet.
Proposed improvement: Implement the Persistence-layer SQLite `motion_command_history` repository with idempotent schema migration, then bind recent command history into MotionView through a view-model state object.
Requirement impact: FR-063, FR-069, FR-200, NFR-004
Priority: P1

Date: 2026-06-01
Source: FR-069 SQLite motion command history
Problem: SQLite motion command history persistence now stores request/result records, but no MotionView state reads and displays the latest rows yet.
Proposed improvement: Add an Application query use case or read port for recent motion command history and bind it to MotionView with refresh/error state.
Requirement impact: FR-063, FR-069, FR-200, NFR-004
Priority: P1

Date: 2026-06-01
Source: FR-069 MotionView history binding
Problem: MotionView can show recent persisted command history, but it still lacks operator controls for Servo/Home/Jog/Move/Stop execution through `IMotionCommandUseCase`.
Proposed improvement: Add MotionView command buttons and target inputs that execute through Application use cases, refresh history after each command, and keep backend interlock validation authoritative.
Requirement impact: FR-061, FR-062, FR-063, FR-064, FR-066, FR-067, FR-069, NFR-002, NFR-004
Priority: P1

Date: 2026-06-01
Source: FR-063 MotionView command controls
Problem: MotionView can now execute simulator-backed Servo/Home/Jog X +1/Move preset/Stop commands through `IMotionCommandUseCase`, but typed per-axis request DTOs are still needed before operator-entered jog steps and absolute targets can drive real adapters.
Proposed improvement: Introduce typed motion command request DTOs for axis, direction, distance, profile, and absolute targets; validate operator-entered values against soft limits before dispatch and persist the typed payload in `motion_command_history`.
Requirement impact: FR-062, FR-063, FR-066, FR-069, NFR-004
Priority: P1
Status: Addressed for axis, direction, step, and absolute target payloads by ADR-0003; profile/tolerance remains open.

Date: 2026-06-01
Source: FR-063 typed motion command payloads
Problem: MotionView can now dispatch typed jog and absolute move targets, but velocity profile, acceleration, and arrival tolerance inputs are still fixed by the simulator/controller defaults.
Proposed improvement: Add typed profile/tolerance payloads with UI validation, simulator acceptance checks, and command history persistence before real adapter integration.
Requirement impact: FR-063, FR-064, FR-066, FR-067, FR-069, NFR-004
Priority: P1
Status: Addressed by ADR-0004 for request-level velocity, acceleration, deceleration, jerk, and arrival tolerance payloads.

Date: 2026-06-01
Source: FR-065 motion profile/tolerance payloads
Problem: Move Absolute now captures a request-level profile and arrival tolerance, but there is no profile preset library, per-axis override policy, or recipe-level profile reuse model yet.
Proposed improvement: Add named motion profile presets, clarify per-axis override behavior, and wire profile selection into recipe/teaching workflows before real adapter integration.
Requirement impact: FR-063, FR-065, FR-100, FR-120, FR-200, NFR-004
Priority: P1
Status: Addressed by ADR-0005 for built-in Fine/Standard/Fast presets; per-axis override and recipe/teaching reuse remain open.

Date: 2026-06-01
Source: FR-065 motion profile presets
Problem: MotionView now offers built-in profile presets, but profile selection is not yet persisted as reusable recipe/teaching configuration and cannot vary per axis.
Proposed improvement: Define recipe-level profile preset persistence, per-axis override behavior, and teaching point default profile selection before real adapter integration.
Requirement impact: FR-065, FR-068, FR-100, FR-120, FR-200, NFR-004
Priority: P1

Date: 2026-06-01
Source: FR-100 teaching point domain model
Problem: Teaching points now have Motion-layer role, position, tolerance, and soft-limit validation, but there is still no Application use case or Persistence contract for saving them to the active recipe and dispatching Go To.
Proposed improvement: Add a teaching application use case with duplicate-name validation, command history correlation, recipe persistence, edit history, and MotionView/TeachingView bindings for Save Current Position and Go To Teaching Point.
Requirement impact: FR-100, FR-101, FR-103, FR-104, FR-120, FR-200, NFR-004
Priority: P0
Status: Application use case and repository port added; SQLite persistence, edit history, and WPF TeachingView binding remain open.

Date: 2026-06-01
Source: FR-100 teaching application use case
Problem: Save Current Position and Go To now have an Application boundary, but there is no concrete Persistence repository or WPF TeachingView binding to make the workflow operator-visible.
Proposed improvement: Implement the SQLite teaching point repository with duplicate-name constraints and bind TeachingView commands/list state to the new Application use case.
Requirement impact: FR-100, FR-101, FR-103, FR-104, FR-120, FR-200, NFR-004
Priority: P0
Status: SQLite teaching point repository and schema added; WPF TeachingView binding and edit history remain open.

Date: 2026-06-01
Source: FR-100 SQLite teaching repository
Problem: Teaching Points now persist in SQLite, but operators still cannot save, list, or execute Go To from the WPF TeachingView.
Proposed improvement: Register the teaching repository/use case in App composition and bind TeachingView to save current position, list saved points, and execute Go To through the Application boundary.
Requirement impact: FR-100, FR-101, FR-103, FR-104, FR-120, FR-200, NFR-004
Priority: P0
Status: TeachingView refresh/save/go-to binding added; edit history and recipe ownership remain open.

Date: 2026-06-01
Source: FR-101 TeachingView binding
Problem: Operators can save, list, and Go To Teaching Points from the WPF surface, but Teaching Points are not yet associated with active recipes and edits do not create teaching history rows.
Proposed improvement: Add active recipe ownership for Teaching Points and write `teaching_history` rows for create/update/delete operations.
Requirement impact: FR-104, FR-120, FR-121, FR-200, NFR-004
Priority: P0
Status: Application history entry and repository port added; SQLite history persistence and recipe ownership remain open.

Date: 2026-06-01
Source: FR-104 SQLite teaching history repository
Problem: Teaching history rows can now be stored and queried in SQLite, but Save/Update/Delete teaching workflows do not yet append entries automatically and active recipe ownership is still not wired.
Proposed improvement: Inject `ITeachingHistoryRepository` into the teaching use case, serialize before/after snapshots for create/update/delete, and associate entries with the active recipe when recipe selection is available.
Requirement impact: FR-104, FR-120, FR-121, FR-200, NFR-004
Priority: P0
Status: Addressed for Save Current Position Created history rows; update/delete history and active recipe ownership remain open.

Date: 2026-06-01
Source: FR-104 Teaching save history append
Problem: Save Current Position now appends Created history rows, but the save and history writes are not wrapped in a single database transaction because the Application layer depends on repository ports rather than SQLite transaction details.
Proposed improvement: Add a persistence-level unit-of-work or combined teaching repository operation when update/delete support is introduced, so point mutation and history append can commit atomically.
Requirement impact: FR-104, FR-120, FR-200, NFR-004
Priority: P1

Date: 2026-06-01
Source: FR-104 teaching update/delete contract
Problem: Teaching Point update/delete behavior now has Application and Persistence contracts with history rows, but WPF does not yet expose operator edit/delete controls and active recipe ownership remains null.
Proposed improvement: Bind TeachingView edit/delete controls to the new Application contract, add confirmation for delete, and surface recent history entries for the selected point.
Requirement impact: FR-101, FR-104, FR-120, FR-200, NFR-004
Priority: P0
Status: WPF edit/delete command binding added; delete confirmation, selected-point history display, and active recipe ownership remain open.

Date: 2026-06-01
Source: FR-104 TeachingView edit/delete binding
Problem: TeachingView can update/delete selected points through the Application contract, but delete does not yet require operator confirmation and the selected point history rows are not visible in the UI.
Proposed improvement: Add a confirmation boundary for delete and a selected-point history panel backed by `ITeachingHistoryRepository.ListByPointAsync`.
Requirement impact: FR-104, FR-200, NFR-004
Priority: P0
Status: Delete confirmation boundary added; selected-point history display remains open.

Date: 2026-06-01
Source: FR-104 Teaching delete confirmation
Problem: TeachingView now confirms before delete, but selected-point history rows are still not visible in the operator workflow.
Proposed improvement: Add a selected-point history panel backed by `ITeachingHistoryRepository.ListByPointAsync`, showing Created/Updated/Deleted rows and timestamps for setup traceability.
Requirement impact: FR-104, FR-200, NFR-004
Priority: P0
Status: Addressed with a selected-point Teaching history panel; active recipe ownership and custom dialog styling remain open.

Date: 2026-06-01
Source: FR-120 Teaching recipe context
Problem: Teaching history can now be viewed in the UI, but history rows still need recipe context before full Recipe CRUD owns active recipe selection.
Proposed improvement: Add optional recipe id to Teaching mutation requests and pass it into Created/Updated/Deleted history rows, while leaving full Recipe CRUD/app-settings activation as follow-up.
Requirement impact: FR-104, FR-120, FR-121, FR-200, NFR-004
Priority: P0
Status: In progress on `codex/feature/fr-120-teaching-recipe-context`.

Date: 2026-06-01
Source: FR-120 Recipe domain validation contract
Problem: RecipeView and Recipe persistence need a typed validation contract before JSON/SQLite/UI work can be implemented safely.
Proposed improvement: Add Application-layer Recipe definition records and validation result shape for metadata, Teaching, ROI, camera, vision parameter, and sequence rules.
Requirement impact: FR-120, FR-121, FR-124, NFR-TEST-001
Priority: P0
Status: In progress on `codex/feature/fr-120-recipe-domain-contract`.

Date: 2026-06-01
Source: FR-120 Recipe JSON document store
Problem: Recipe definitions can be validated, but there is no safe JSON file round-trip or path traversal guard for Recipe persistence.
Proposed improvement: Add an Application document-store port and a Persistence JSON implementation that validates before save and generates safe `{RecipeId}.v{Version}.recipe.json` file names under a configured root.
Requirement impact: FR-120, FR-121, FR-124, NFR-008, NFR-TEST-001
Priority: P0
Status: In progress on `codex/feature/fr-120-recipe-json-store`.

Date: 2026-06-01
Source: FR-120 SQLite Recipe index
Problem: Recipe JSON files can be saved and loaded, but RecipeView still needs a fast metadata list/query path with validation and active state.
Proposed improvement: Add a SQLite `recipes` index table and repository for recipe id/version metadata, document path, checksum, active state, validation state, and updated timestamp.
Requirement impact: FR-120, FR-123, FR-124, NFR-004, NFR-TEST-001
Priority: P0
Status: Addressed by the SQLite Recipe index repository and current RecipeView browser binding; active recipe settings remain open.

Date: 2026-06-01
Source: FR-120 Recipe index view binding
Problem: RecipeView can now list SQLite Recipe metadata, but operators still cannot create/import/save Recipe documents or activate a selected Recipe from the UI.
Proposed improvement: Add an Application use case that coordinates `JsonRecipeDocumentStore`, `RecipeValidator`, and `SqliteRecipeIndexRepository`, then bind RecipeView create/import/save/activate commands to it.
Requirement impact: FR-120, FR-121, FR-122, FR-123, FR-124, NFR-004, NFR-008
Priority: P0
Status: Addressed for read-only Recipe index browser; Application save workflow and activation remain open.

Date: 2026-06-01
Source: FR-120 Recipe library save use case
Problem: Recipe JSON save and SQLite index update need one Application boundary before RecipeView can expose save/import commands safely.
Proposed improvement: Add `IRecipeLibraryUseCase` to validate Recipe definitions, save JSON through `IRecipeDocumentStore`, compute a checksum, and upsert `IRecipeIndexRepository`.
Requirement impact: FR-120, FR-121, FR-123, FR-124, NFR-004, NFR-008, NFR-TEST-001
Priority: P0
Status: Addressed by ADR-0011 and `RecipeLibraryUseCase`; App composition and UI binding remain open.

Date: 2026-06-01
Source: FR-120 Recipe App composition
Problem: The Recipe library save use case exists, but WPF App startup must register it with the JSON document store and SQLite index repository before UI commands can use it.
Proposed improvement: Extract App service registration into a testable composition helper and register Recipe document/library services with local app-data paths.
Requirement impact: FR-120, FR-121, FR-123, FR-124, NFR-004, NFR-008, NFR-TEST-001
Priority: P0
Status: Addressed by WPF App composition registration; RecipeView save command binding remains open.

Date: 2026-06-01
Source: FR-120 RecipeView save command
Problem: RecipeView can list indexed metadata and the App can resolve Recipe library services, but operators still need a WPF save command to create a valid Recipe document and index row.
Proposed improvement: Add a RecipeView metadata/camera/Teaching/ROI editor and bind Save Recipe to `IRecipeLibraryUseCase`, refreshing the index after a successful save.
Requirement impact: FR-120, FR-121, FR-123, FR-124, NFR-004, NFR-006, NFR-008, NFR-TEST-001
Priority: P0
Status: Addressed for single-camera-position Recipe save; active Recipe activation and multi-row editors remain open.

Date: 2026-06-01
Source: FR-122 Active Recipe index selection
Problem: RecipeView can save and list Recipe rows, but there is not yet a repository contract to mark one existing Recipe as active without risking stale UI clearing all active state.
Proposed improvement: Add active Recipe query/switch methods to the Recipe index port and SQLite repository, then bind RecipeView activation in a follow-up UI slice.
Requirement impact: FR-120, FR-122, FR-123, FR-124, NFR-004, NFR-TEST-001
Priority: P0
Status: Addressed by the Recipe index repository active query/switch contract; RecipeView activation is in progress on `codex/feature/fr-122-recipe-view-activate`.

Date: 2026-06-01
Source: FR-122 RecipeView active command
Problem: The Recipe index can mark one row active, but operators still need an HMI command to activate the selected Recipe and see the active summary update.
Proposed improvement: Bind RecipeView Set Active to `IRecipeIndexRepository.SetActiveAsync`, refresh the list after success, and surface rejected/missing-row paths in `StatusText`.
Requirement impact: FR-120, FR-122, FR-123, FR-124, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by RecipeView Set Active command; active context consumption by Teaching/inspection remains open.

Date: 2026-06-01
Source: FR-122 Active Recipe context provider
Problem: RecipeView can select an active Recipe, but Teaching and inspection workflows need a shared Application-layer way to read the active Recipe without depending on SQLite repository details.
Proposed improvement: Add an active Recipe context provider that maps active metadata to Success/NotSelected/InvalidRecipe/RepositoryUnavailable statuses.
Requirement impact: FR-120, FR-122, FR-180, FR-200, NFR-004, NFR-TEST-001
Priority: P0
Status: Addressed by `IActiveRecipeContext`; Teaching consumption is in progress on `codex/feature/fr-122-teaching-active-context`.

Date: 2026-06-01
Source: FR-122 Teaching active Recipe context
Problem: TeachingView still relies on manual Recipe ID entry when writing Recipe context into save/update/delete history rows.
Proposed improvement: Inject `IActiveRecipeContext` into TeachingViewModel, resolve active Recipe before Teaching mutations, and keep manual Recipe ID entry only as fallback when no active Recipe is selected.
Requirement impact: FR-100, FR-104, FR-120, FR-122, FR-200, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by TeachingViewModel active Recipe resolution; inspection precheck remains open.

Date: 2026-06-01
Source: FR-180 Inspection active Recipe precheck
Problem: InspectionView is still a placeholder and does not reject missing or invalid active Recipe context before an operator starts inspection.
Proposed improvement: Add an InspectionViewModel precheck command backed by `IActiveRecipeContext`, with operator-visible blocked states for no active Recipe, invalid active Recipe, and repository unavailable.
Requirement impact: FR-122, FR-180, FR-181, FR-200, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by InspectionView active Recipe precheck; Application run use case is in progress on `codex/feature/fr-181-inspection-run-use-case`.

Date: 2026-06-01
Source: FR-181/FR-182 Inspection run use case
Problem: InspectionView can precheck active Recipe context, but Run Inspection still needs an Application sequence boundary with timeline state and operator cancellation.
Proposed improvement: Add `IInspectionRunUseCase`, ordered sequence step records, ViewModel timeline binding, and Stop Inspection cancellation of the active run token.
Requirement impact: FR-122, FR-180, FR-181, FR-182, FR-200, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by `IInspectionRunUseCase`; camera grab was split into a follow-up slice.

Date: 2026-06-01
Source: FR-180 Simulator Auto mode transition
Problem: InspectionRunUseCase enforces Auto mode, but the simulator previously reported connected equipment as Manual only.
Proposed improvement: Add explicit Enter Manual/Enter Auto commands with interlock coverage and Dashboard bindings.
Requirement impact: FR-004, FR-040, FR-041, FR-180, FR-181, FR-182, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by simulator Manual/Auto commands and Dashboard bindings.

Date: 2026-06-01
Source: FR-140/FR-141 Camera grab simulator
Problem: InspectionRunUseCase can start the sequence but needs a tested camera acquisition boundary before vision/judge/persist slices.
Proposed improvement: Add `ICameraDevice`, camera grab request/result/frame contracts, a `VirtualCameraDevice`, InspectionRunUseCase Grab Image execution, and Last Grab UI binding.
Requirement impact: FR-140, FR-141, FR-180, FR-181, FR-182, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by ADR-0016 and PR #39; Move To Camera Recipe execution remains in progress.

Date: 2026-06-01
Source: FR-180 Recipe Camera move execution
Problem: InspectionRunUseCase can grab an image, but Move To Camera is still skipped and camera settings still use default simulator values.
Proposed improvement: Load the active Recipe document, add an internal `SequenceMoveToCamera` command, execute the Recipe Camera Teaching point through `IMotionCommandUseCase`, and use Recipe camera settings for grab.
Requirement impact: FR-100, FR-102, FR-121, FR-122, FR-140, FR-180, FR-181, FR-182, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by ADR-0017 and PR #40.

Date: 2026-06-01
Source: FR-160/FR-180 Deterministic 2D inspection
Problem: InspectionRunUseCase can move and grab an image, but the Inspect 2D and Judge steps still need deterministic simulator evidence before 3D, overlay, and persistence slices.
Proposed improvement: Add `IVisionInspectionEngine`, deterministic Gray8 Missing/Scratch/Offset checks, Application conversion from CameraFrame/Recipe ROI to Vision request, and Judge Pass/Fail timeline state.
Requirement impact: FR-140, FR-160, FR-161, FR-163, FR-180, FR-181, FR-182, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by ADR-0018 and PR #41.

Date: 2026-06-01
Source: FR-162 Synthetic height-map inspection
Problem: 2D inspection and Judge can run, but Inspect 3D still needs deterministic height-map evidence for Lift, Dent, and LeadBent-like defects.
Proposed improvement: Add synthetic height-map generation, deterministic 3D inspection, Application Inspect 3D execution, and final Judge aggregation across 2D/3D results.
Requirement impact: FR-162, FR-180, FR-181, FR-182, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by ADR-0019 and PR #42.

Date: 2026-06-01
Source: FR-200 Inspection result persistence
Problem: 2D/3D inspection and Judge can now run, but overlay rendering and SQLite result persistence are still pending.
Proposed improvement: Add image overlay artifact generation and `InspectionResult` persistence with Recipe/Lot/Judge/Defect/timing records.
Requirement impact: FR-180, FR-200, NFR-004, NFR-006, NFR-TEST-001
Priority: P0
Status: Addressed by ADR-0020 and PR #43 for SQLite metadata/defect/timing persistence; overlay artifact generation is addressed by ADR-0021.

Date: 2026-06-01
Source: FR-200 Inspection result persistence
Problem: SQLite result rows can now store nullable overlay and height-map artifact paths, but no overlay renderer or artifact writer creates files for Offline Debug or reports yet.
Proposed improvement: Add deterministic overlay rendering and artifact path policy that writes generated overlays/height-map snapshots under a safe local-data subdirectory and records those paths in `inspection_results`.
Requirement impact: FR-160, FR-162, FR-180, FR-200, NFR-004, NFR-008, NFR-TEST-001
Priority: P0
Status: Addressed by ADR-0021 and PR #44.

Date: 2026-06-01
Source: FR-220 Offline Debug result browser
Problem: Inspection results and artifact paths are persisted, but Offline Debug still needs a WPF surface to load and inspect historical rows.
Proposed improvement: Bind OfflineDebugView to `IInspectionResultReader` so operators can refresh recent results, select one row, and inspect source/overlay/height-map paths plus defects.
Requirement impact: FR-202, FR-220, FR-221, FR-200, NFR-004, NFR-006, NFR-TEST-001
Priority: P1
Status: Addressed by ADR-0022 and PR #45 for read-only result browsing; artifact rendering and re-inspection remain separate follow-up work.

Date: 2026-06-01
Source: FR-221 Offline Debug artifact rendering
Problem: Offline Debug can display artifact paths, but it does not yet render BMP overlays/height maps or launch a safe file viewer.
Proposed improvement: Add an artifact resolver/viewer boundary that maps relative artifact paths under the local data root, validates existence, and renders overlay/height-map previews in the Offline Debug workspace.
Requirement impact: FR-202, FR-221, FR-222, FR-200, NFR-006, NFR-008, NFR-TEST-001
Priority: P1
Status: Open.
