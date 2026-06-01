# ADR-0008: Recipe Domain Validation Contract

Status: Accepted
Date: 2026-06-01

## Context

FR-120 through FR-124 require Recipe management, Recipe-owned Teaching/ROI/Vision parameters, and validation feedback in the WPF UI. RecipeView is still a shell, so the next safe step is an Application-layer contract that can validate Recipe data before JSON persistence, SQLite indexing, and UI editing are added.

## Decision

Add `VisionCell.Application.Recipes` with:

- `RecipeDefinition` and child records for metadata, Teaching points, camera settings, ROIs, vision parameters, and sequence steps.
- `RecipeValidationIssue` and `RecipeValidationResult`.
- `RecipeValidator` for required metadata, semantic version format, Teaching point domain validation, ROI image bounds, camera/vision parameter ranges, and required sequence steps.

The validator reuses the existing Teaching Point domain rules for position/tolerance validation. Persistence and UI workflows will consume this contract in later slices.

## Alternatives

- Build RecipeView and JSON persistence first: rejected because validation rules should be testable without WPF or file-system behavior.
- Put Recipe models in Core: deferred because the current contract includes Application workflow validation and references Motion Teaching domain types.
- Encode Recipe as untyped JSON only: rejected because it would make FR-124 validation and future UI binding brittle.

## Consequences

- Recipe validation can be tested independently of WPF and SQLite.
- JSON persistence and RecipeView can build on a typed contract.
- The contract is not yet a full repository, active recipe service, or UI editor.

## Requirement Impact

- FR-120: Establishes Recipe metadata contract.
- FR-121: Captures Teaching, ROI, camera, vision, and sequence sections.
- FR-124: Adds validation result shape for UI display.
- NFR-TEST-001: Adds focused Application tests before persistence/UI expansion.

## Rollback

Remove `VisionCell.Application.Recipes`, the recipe validator tests, related docs, and this ADR. Existing Teaching/Motion behavior remains unchanged.
