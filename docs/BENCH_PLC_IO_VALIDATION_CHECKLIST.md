# Bench PLC I/O Validation Checklist

## Scope

This checklist defines the bench-only evidence required before any future PLC or remote I/O adapter can be treated as validated. It does not implement or approve a real PLC connection, safety relay reset, fieldbus protocol, output write path, or production machine operation.

Related requirements:

- FR-020, FR-021, FR-022
- FR-040, FR-041, FR-042, FR-043, FR-044
- FR-080, FR-081, FR-082, FR-083, FR-084
- FR-184, FR-201, FR-240
- NFR-002, NFR-004, NFR-007, NFR-TEST-001

## Entry Criteria

Do not begin bench PLC validation unless all items below are true:

- `EquipmentRuntimeProfile.RealHardware` remains disabled by default.
- The adapter under test is behind `IPlcIoAdapter`; no WPF ViewModel or Application use case references vendor SDK objects, PLC frames, fieldbus payloads, or raw connection handles.
- The bench setup has no production motion enabled.
- A physical EStop path is available and verified by the equipment owner before any output write is attempted.
- PLC endpoint, station ID, rack/slot, network segment, and protocol version are documented outside source code.
- Test operator, date, bench controller identifier, adapter build SHA, and rollback path are recorded.

## Read-Only Snapshot Checklist

Record evidence for every item:

| Item | Expected evidence | Pass condition |
|---|---|---|
| Adapter identity | `IHardwareAdapter.GetStatusAsync` result | Adapter name, endpoint label, connected flag, ready flag, and message are visible without blocking. |
| EStop input | PLC input snapshot | `DI_ESTOP_ON` changes to true when EStop is pressed and false after release. |
| Door input | PLC input snapshot | `DI_DOOR_CLOSED` changes false/open and true/closed without inverted semantics. |
| Vacuum input | PLC input snapshot | `DI_VACUUM_OK` represents vacuum-ready state, not output command state. |
| Air pressure input | PLC input snapshot | `DI_AIR_PRESSURE_OK` falls false when bench air is removed and recovers true when restored. |
| Camera ready input | PLC input snapshot | `DI_CAMERA_READY` reflects camera-ready line or documented simulator bridge only. |
| Servo alarm input | PLC input snapshot | `DI_SERVO_ALARM` true blocks motion commands and records an alarm candidate. |
| Snapshot timing | Timestamped samples | Snapshot latency and timestamp drift stay within the configured HMI refresh target. |
| Cancellation | Cancelled read call | Cancellation returns an explicit cancelled result or exception mapping without freezing the caller. |
| Timeout | Endpoint unavailable or delayed read | Timeout is bounded and converted to an explicit result/error path. |

## Output Write Gate Checklist

Do not run these items on a production machine. Output writes are allowed only after read-only snapshot validation passes.

| Item | Expected evidence | Pass condition |
|---|---|---|
| Manual mode gate | Attempt output write outside Manual mode | Backend rejects unsafe mode with `MachineCommandResult` and structured event. |
| EStop gate | Attempt output write while EStop is active | Backend rejects write; no PLC output changes state. |
| Door gate | Attempt output write with door open when policy requires closed door | Backend rejects write with operator-visible reason. |
| Allowed output | Toggle low-risk bench output such as tower lamp | Command has timeout/cancellation, correlation ID, explicit success/failure, and transition record. |
| Unsafe output | Attempt motion/vacuum/actuator output without signed bench permission | Backend rejects write and records a warning/alarm as required. |
| Output audit | Review `io_transition_history` or future output-write audit rows | Timestamp, source, correlation ID, previous/current state, and operator memo are present. |

## Alarm And Interlock Checklist

| Item | Expected evidence | Pass condition |
|---|---|---|
| EStop alarm | Trigger EStop from bench input | Alarm record contains code, severity, area, message, occurred time, and correlation ID. |
| Servo alarm | Trigger servo alarm input | Motion commands are blocked and an alarm candidate is recorded. |
| Camera not ready | Force camera-ready false | Inspection start is rejected and camera/inspection alarm path is recorded. |
| Air pressure low | Force air-pressure false | Auto/inspection path is blocked and recovery message is visible. |
| Acknowledge vs reset | Acknowledge alarm in WPF | `acknowledged_at` and memo update only operator recovery state; hardware reset remains separately evidenced. |

## Evidence Package

Every bench run must attach or reference:

- Adapter build SHA and branch.
- Hardware endpoint labels with secrets redacted.
- Screenshots or exported logs for read-only snapshots, rejected writes, allowed writes, alarms, and recovery memo.
- `system_events`, `equipment_alarms`, and I/O history evidence.
- Known deviations and whether the runtime profile remains blocked.
- Approval from the equipment owner before any future real-hardware profile can be enabled.

## Exit Criteria

The bench PLC I/O path remains unvalidated until all required evidence is attached and reviewed. Passing this checklist does not prove production readiness; it only permits the next controlled adapter validation phase in `docs/HARDWARE_INTEGRATION_PLAN.md`.
