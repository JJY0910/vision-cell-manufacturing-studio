# ADR-0005: Motion Profile Presets

Status: Accepted
Date: 2026-06-01

## Context

ADR-0004 added request-level velocity, acceleration, deceleration, jerk, and arrival tolerance fields for Move Absolute. Operators still had to enter each numeric profile field manually, which is error-prone for repeated setup work and does not communicate whether the values came from an intentional profile choice.

FR-065 benefits from a small preset catalog before recipe-level profile management exists.

## Decision

Add `MotionProfilePreset` in `VisionCell.Motion.Commands` with a built-in catalog:

- `Fine`
- `Standard`
- `Fast`

MotionView exposes a profile preset selector. Selecting a preset populates the velocity, acceleration, deceleration, jerk, and arrival tolerance fields, while still allowing the operator to edit the numeric values before dispatch. `AbsoluteMoveTarget` now includes a `ProfilePreset` parameter so the persisted `MachineCommandRequest` records the selected preset name alongside the executed numeric values.

The simulator validates that `ProfilePreset` is not empty and includes the preset name in the command result message.

## Alternatives

- Store presets only in WPF view-model code: rejected because future adapters, recipe workflows, and tests need a shared payload vocabulary.
- Make presets immutable with no manual override: rejected because setup engineers often need to tune values during teaching/debug.
- Add recipe-level profile persistence now: deferred because recipe CRUD and teaching persistence are broader FR-100/FR-120 work.

## Consequences

- Operators can quickly switch between Fine, Standard, and Fast move behavior.
- Command history shows both the selected preset name and the final numeric profile values.
- The preset catalog is currently static and code-defined.
- Per-axis overrides and recipe profile reuse remain separate work.

## Requirement Impact

- FR-063: Move Absolute still dispatches through the same Application/controller path.
- FR-065: Motion profile setup now has named presets plus editable numeric fields.
- FR-069: Motion history stores the selected profile preset.
- NFR-004: Traceability includes the preset origin for profile values.

## Rollback

Remove `MotionProfilePreset`, the `ProfilePreset` parameter, MotionView preset selector/binding, simulator validation/message changes, related tests, and this ADR. Direct numeric profile entry from ADR-0004 remains valid.
