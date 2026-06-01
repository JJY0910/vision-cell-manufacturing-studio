# 07. Recipe Specification

## Recipe File Naming

```text
assets/recipes/{ProductCode}.{RecipeVersion}.recipe.json
```

Example:

```text
assets/recipes/PKG-MEMORY-MODULE.v1.0.0.recipe.json
```

## Recipe Schema

```json
{
  "recipeId": "PKG-MEMORY-MODULE",
  "productName": "Memory Module Sample",
  "version": "1.0.0",
  "createdAt": "2026-01-01T00:00:00Z",
  "updatedAt": "2026-01-01T00:00:00Z",
  "motion": {
    "teachingPoints": [
      {
        "id": "CAMERA_POS_01",
        "name": "Camera Position 01",
        "role": "Camera",
        "position": { "x": 10.0, "y": 25.0, "z": 8.0, "theta": 0.0 },
        "tolerance": { "x": 0.05, "y": 0.05, "z": 0.02, "theta": 0.1 }
      }
    ]
  },
  "camera": {
    "exposureMs": 5.0,
    "gain": 1.0,
    "lightIntensity": 80
  },
  "vision": {
    "rois": [
      { "id": "IC_TOP", "name": "IC Top", "x": 120, "y": 80, "width": 300, "height": 200 }
    ],
    "parameters": {
      "missingAreaThreshold": 0.75,
      "offsetTolerancePx": 8,
      "scratchThreshold": 0.65,
      "expectedHeight": 1.0,
      "heightToleranceLow": 0.15,
      "heightToleranceHigh": 0.15
    }
  },
  "sequence": {
    "steps": ["SafetyCheck", "MoveToCamera", "Grab", "Inspect2D", "Inspect3D", "Judge", "Persist"]
  }
}
```

## Validation Rules

- recipeId required
- version semantic version format
- at least one teaching point for inspection
- all teaching positions within soft limit
- all ROI coordinates positive and inside image bounds
- thresholds within defined ranges
- sequence contains required steps

Implementation status:

- `VisionCell.Application.Recipes` defines `RecipeDefinition`, metadata/camera/vision/sequence child records, validation issue/result records, and `RecipeValidator`.
- `RecipeValidator` checks required metadata, semantic version format, Teaching Point domain validation, ROI bounds against the default 1920x1080 image size, camera/vision parameter ranges, and required sequence steps.
- `JsonRecipeDocumentStore` saves and loads validated Recipe JSON under a configured recipe root directory using `{RecipeId}.v{Version}.recipe.json` file names.
- `SqliteRecipeIndexRepository` stores Recipe metadata, document path, checksum, active state, validation state, and updated timestamp for list/query workflows.
- `RecipeViewModel` and `RecipeView` can refresh and display the SQLite Recipe index, including active state and validation summary.
- `RecipeLibraryUseCase` validates a Recipe, saves its JSON document, computes a checksum, and upserts the SQLite Recipe index through Application-layer ports.
- WPF App composition registers the JSON document store under `local-data/recipes` and the Recipe library save use case for RecipeView save and future import commands.
- RecipeView can save a valid single-camera-position Recipe from operator-entered metadata, camera, Teaching, and ROI fields, then refresh/select the indexed row.
- `SqliteRecipeIndexRepository` can query the active Recipe row and atomically switch active state to one existing indexed Recipe without clearing the previous active row on a missing target.
- RecipeView can set the selected indexed Recipe active through the Application-layer index port and refresh the active-state summary.
- `ActiveRecipeContext` exposes the active Recipe metadata through an Application-layer result contract for Teaching, inspection startup, and future interlock consumers.
- TeachingView resolves active Recipe context before Save/Update/Delete mutations and passes the active Recipe ID into Teaching history requests, with manual Recipe ID entry retained as a fallback.
- InspectionView uses active Recipe context as a precheck before Run Inspection can proceed to future sequence execution.
- Active Recipe app startup restore, full inspection sequence execution, and multi-row Recipe editing remain follow-up work.

## Versioning Policy

- Major: structure/inspection behavior incompatible
- Minor: new optional parameter/ROI/teaching
- Patch: small threshold or metadata update

## Recipe UI Requirements

- Metadata editor
- Teaching table
- ROI editor with image preview
- Vision params form
- Validation panel
- Save/Save As/Clone/Export

## Persistence

- Recipe is stored as JSON file and indexed in SQLite.
- Active recipe id/version stored in app settings.
- Recipe edits must generate event log.
- Current JSON document store rejects invalid Recipe definitions and unsafe recipe id/version file-name inputs before writing files.
- Current SQLite Recipe index stores metadata only; JSON document save and index update are not yet a single transaction.
- Current RecipeView browser is read-only and depends on rows already present in the SQLite Recipe index.
- Current Application Recipe save workflow reports document/index failures explicitly but does not make file-system and SQLite updates atomic.
- Current RecipeView save surface creates one Camera Teaching point and one ROI row; multi-row editors remain follow-up work.
- Current active Recipe state is stored in the SQLite Recipe index; a separate app settings pointer has not been added.
- Current active Recipe context checks metadata validity but does not yet load the JSON document.
