# ADR-0051: Dashboard KPI Card Overview

## Status

Accepted

## Date

2026-06-04

## Context

Dashboard is the first HMI screen operators see for connection, safety, camera, and Recipe state. The top overview band still used one-off `Border` panels even though the shared `KpiCard` control is already used by Motion, Recipe, Inspection, and Alarm summary bands. That made the Dashboard visually close but structurally inconsistent with the rest of the HMI.

## Decision

- Replace Dashboard overview panels with three shared `KpiCard` controls:
  - Equipment Overview: connection status and controller latency.
  - Safety Summary: safety status.
  - Camera / Recipe: camera status and active Recipe status.
- Preserve the existing DashboardViewModel bindings.
- Add an App XAML QA test that keeps the Dashboard overview band on shared `KpiCard` controls.
- Keep this slice as WPF presentation polish only.

## Alternatives Considered

- Leave the one-off Dashboard panels: rejected because Dashboard is the primary equipment overview and should use the same HMI metric-card pattern.
- Change DashboardViewModel state: rejected because the existing properties already express the required values.
- Add a new specialized Dashboard card control: rejected because `KpiCard` already covers title/value/detail status cards.

## Consequences

- Dashboard overview metrics now align with the shared HMI metric-card style.
- Future XAML changes that replace the shared cards with one-off panels fail App XAML QA.
- No equipment controller, interlock, simulator, persistence, or navigation behavior changes.
- Physical panel and operator acceptance validation remain unverified.

## Requirement Links

- FR-003: global equipment state remains visible on the Dashboard.
- FR-006: HMI visual consistency and constrained layout polish.
- FR-020/FR-044: connection and safety summary remain visible.
- NFR-009/NFR-010: reusable controls and operator readability.

## Rollback

Restore the previous Dashboard overview `Border` panels, remove the XAML QA assertion, and revert this ADR plus related documentation updates.
