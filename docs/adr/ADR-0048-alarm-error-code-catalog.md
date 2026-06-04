# ADR-0048: Alarm Error Code Catalog

## Status

Accepted

## Date

2026-06-04

## Context

Alarm Center already displays persisted simulator/Application alarm rows, selected-alarm recovery guidance, triage filters, acknowledgement, and recovery memo state. FR-241 requires an error code catalog with code, cause, and recovery method. The previous selected-alarm hint switch made recovery guidance visible only after an alarm was selected and duplicated the same mapping that `EquipmentAlarmFactory` uses for area/severity behavior.

## Decision

- Add `ErrorCodeCatalog` and `ErrorCodeCatalogEntry` in `VisionCell.Core.Errors`.
- Keep the catalog read-only and limited to the documented EQP/MOT/CAM/VIS/DB codes from the equipment protocol specification.
- Route `EquipmentAlarmFactory` area/severity resolution through the catalog so alarm records and operator guidance share one source.
- Bind the catalog into `AlarmViewModel` as read-only HMI rows.
- Display the catalog in `AlarmView` with code, severity, area, cause, and recovery action.
- Keep hardware reset, PLC/vendor alarm ingestion, and safety relay acknowledgement out of scope.

## Alternatives Considered

- Keep the WPF-only recovery switch: rejected because FR-241 needs a browsable catalog and shared mappings reduce drift.
- Store the catalog in SQLite: rejected because these documented protocol codes are static application metadata, not operator history.
- Add vendor/PLC alarm source rows now: rejected because real hardware alarm-source validation belongs to future Hardware Adapter work.

## Consequences

- Operators can inspect alarm/error guidance before selecting or receiving an alarm record.
- Alarm severity/area and recovery guidance now derive from a shared Core catalog.
- The catalog improves Recovery Center traceability without changing the alarm schema or enabling real hardware.
- Real equipment alarm source validation remains explicitly unvalidated.

## Requirement Links

- FR-043: Alarm/Fault/Recovery Center keeps acknowledgement separate from hardware reset.
- FR-184: Inspection failure alarms continue to map to visible recovery guidance.
- FR-201: Alarm records remain durable recovery history.
- FR-241: Error code catalog now displays code, cause, severity/area, and recovery action.
- NFR-004/NFR-006/NFR-009: mappings are centralized, testable, and UI-safe.

## Rollback

Remove `ErrorCodeCatalog`, `ErrorCodeCatalogEntry`, AlarmView catalog bindings, related tests, this ADR, and documentation updates, then restore the previous WPF-only recovery guidance switch.
