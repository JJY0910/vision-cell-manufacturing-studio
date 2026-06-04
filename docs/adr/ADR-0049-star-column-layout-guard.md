# ADR-0049: Star Column Layout Guard

## Status

Accepted

## Date

2026-06-04

## Context

Priority 5 UI QA requires dense WPF HMI screens to remain usable at constrained widths. Root screen grids already use `MinWidth="0"`, vertical scrolling, and disabled horizontal workspace scrolling. Several nested split layouts still used star-sized `ColumnDefinition` entries without `MinWidth="0"`. In WPF measure/layout, long table text or detail panels can cause star columns to keep a larger desired width and push the workspace toward horizontal growth.

## Decision

- Add `MinWidth="0"` to star-sized `ColumnDefinition` entries in priority module XAML screens.
- Add an App XAML QA test that scans priority HMI screens and requires every star `ColumnDefinition` to set `MinWidth="0"`.
- Keep this as a local/CI structure guard; it does not replace physical panel or touch validation.

## Alternatives Considered

- Leave only the root `Grid MinWidth="0"` guard: rejected because nested star columns can still preserve oversized child desired width.
- Enable horizontal workspace scrolling: rejected because the operator workflow should remain vertically reachable without drifting sideways.
- Replace split panels with new responsive controls: deferred because this guard is smaller and keeps existing screen behavior intact.

## Consequences

- Split HMI panels are less likely to be forced wider by long table/path/status content.
- Future XAML changes that add unguarded star columns in priority screens fail App tests.
- No ViewModel, Application, persistence, or equipment behavior changes.
- Physical 1920x1080, 1366x768, touch, and real equipment workflow validation remain unverified.

## Requirement Links

- FR-006: constrained-width HMI layout reachability.
- NFR-001: UI should not be pushed into awkward blocked/overflow states by long content.
- NFR-009/NFR-010: layout guard is reusable and test-covered.

## Rollback

Remove the `MinWidth="0"` attributes added to star `ColumnDefinition` entries, remove the XAML QA test, and revert this ADR and related documentation updates.
