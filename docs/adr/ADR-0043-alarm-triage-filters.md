# ADR-0043: Alarm Center Triage Filters

## Status

Accepted

## Context

Alarm Center can list persisted simulator/Application alarm rows, show recovery guidance, and acknowledge records with an action memo. As alarm history grows, operators need a quick way to focus on active alarms, severity, and equipment area without changing the SQLite schema or adding unvalidated PLC/vendor alarm sources.

## Decision

- Keep `AlarmCenterUseCase.ListRecentAsync` as the source of recent alarm rows.
- Store the latest loaded alarm rows inside `AlarmViewModel`.
- Apply UI-side ViewModel filters for:
  - active-only records
  - severity
  - equipment area
- Bind the filtered rows to `AlarmView` through the existing `Alarms` collection.
- Display a filter summary and total-vs-visible count.
- Keep filtering out of WPF code-behind and out of the repository contract.

## Alternatives Considered

- Add SQL-level filtering now: rejected because the current recent-row query is small, and repository filtering can be added later if retention grows.
- Add PLC/vendor alarm filters: rejected because real alarm source integration is not implemented or validated.
- Hide acknowledged alarms permanently: rejected because acknowledged records remain recovery history and must stay reviewable.

## Consequences

- Operators can triage active/critical/area-specific alarm rows without losing recovery history.
- The filter remains a WPF ViewModel concern and does not alter alarm persistence.
- Real PLC/vendor alarm source, hardware reset, and safety relay confirmation remain follow-up work.

## Requirement Links

- FR-043: Alarm/Fault/Recovery Center gains operator triage controls.
- FR-201/FR-241: Simulator/Application alarm rows stay traceable and reviewable.
- FR-240/NFR-009: Real hardware alarm source and reset validation remain out of scope.
- NFR-001/NFR-TEST-001: Filtering is MVVM-only and covered by ViewModel/XAML tests.

## Rollback

Remove the AlarmView filter bindings, ViewModel filter state, tests, this ADR, and related documentation updates.
