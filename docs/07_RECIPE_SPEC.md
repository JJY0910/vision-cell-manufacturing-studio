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
- SQLite indexing, active recipe settings, and RecipeView editing remain follow-up work.

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
