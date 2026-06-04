# ADR-0050: Inspection KPI Card Summary Band

## Status

Accepted

## Date

2026-06-04

## Context

Priority 5 UI QA asks the WPF screens to look and behave like a consistent equipment HMI. Motion, Recipe, and Alarm already use the shared `KpiCard` control for compact status bands. InspectionView still used four one-off `Border` panels for Active Recipe, Precheck, Last Check, and Correlation status. That duplicated typography/layout decisions and made future HMI polish easier to drift.

## Decision

- Replace the InspectionView summary `Border` panels with four shared `KpiCard` controls.
- Preserve the existing ViewModel bindings for active Recipe, precheck, last check, and correlation ID.
- Add an App XAML QA test that keeps the Inspection summary band on shared `KpiCard` controls.
- Keep this slice as WPF presentation polish only.

## Alternatives Considered

- Leave the one-off panels: rejected because they duplicate HMI metric-card styling that already exists.
- Change `KpiCard` itself: rejected because the existing control already supports the needed title/value/detail structure.
- Add new Inspection behavior or sequence state: rejected because this slice is UI QA only.

## Consequences

- Inspection summary cards now match the rest of the HMI metric-card style.
- Future changes that replace the shared cards with one-off panels fail App XAML QA.
- No ViewModel, sequence, camera, persistence, or equipment behavior changes.
- Physical panel and operator acceptance validation remain unverified.

## Requirement Links

- FR-006: HMI screen consistency and constrained layout polish.
- FR-180/FR-181: Inspection status remains visible without changing sequence behavior.
- NFR-009/NFR-010: reusable controls and operator readability.

## Rollback

Restore the previous four InspectionView `Border` panels, remove the XAML QA assertion, and revert this ADR plus related documentation updates.
