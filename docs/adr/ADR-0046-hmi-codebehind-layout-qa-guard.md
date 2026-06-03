# ADR-0046: HMI Code-Behind and Layout QA Guard

Status: Accepted
Date: 2026-06-03

## Context

Priority 5 UI QA requires WPF screens to keep an industrial HMI layout while avoiding business logic in code-behind. Existing tests already cover shared command bars, HMI button styles, dark grid headers, disabled tooltips, and empty states. The suite still needed an explicit automated guard for module code-behind and the baseline scrollable workspace layout used by the priority screens.

## Decision

- Add an App test that verifies module `*.xaml.cs` files stay initialization-only and do not reference async work, Application use cases, repositories, process/file access, message boxes, blocking calls, or click handlers.
- Add an App test that verifies priority HMI screens keep a single vertical `ScrollViewer` with horizontal scrolling disabled and a root `Grid MinWidth="0"` layout guard.
- Keep this as an automated local/CI QA guard. It does not replace physical panel or operator acceptance testing.

## Alternatives

- Rely only on manual code review: rejected because code-behind regressions are cheap to scan automatically.
- Move layout verification into runtime screenshot tooling: deferred because current CI already provides deterministic XAML tests and WPF smoke; physical panel validation remains unavailable.
- Allow screen-specific code-behind exceptions: rejected for module screens because current modules only need `InitializeComponent`.

## Consequences

- New module screens are less likely to regress into code-behind business logic.
- Priority screens keep the scrollable, constrained-width HMI workspace pattern under automated test coverage.
- Physical 1920x1080, 1366x768, touch, and real equipment workflow validation remain unverified.

## Requirement impact

- FR-006: HMI layout reachability gains automated XAML structure coverage.
- NFR-001/NFR-002/NFR-004/NFR-007: MVVM and non-blocking UI constraints remain protected by test guards.
- NFR-TEST-001: UI QA coverage becomes part of the App test suite.

## Rollback

Remove the two HMI QA tests and revert the related documentation/checklist updates.
