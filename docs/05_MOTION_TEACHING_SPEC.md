# 05. Motion and Teaching Specification

## Axis

Axis IDs:

- X: horizontal stage
- Y: vertical stage
- Z: camera/head height
- T: theta rotation

## Axis State

```csharp
public sealed record AxisSnapshot(
    AxisId AxisId,
    double Position,
    double Target,
    bool IsHomed,
    bool ServoOn,
    bool IsMoving,
    AxisAlarm? Alarm,
    SoftLimit SoftLimit,
    MotionProfile Profile);
```

## Soft Limits

| Axis | Min | Max | Unit |
|---|---:|---:|---|
| X | -200.0 | 200.0 | mm |
| Y | -200.0 | 200.0 | mm |
| Z | 0.0 | 100.0 | mm |
| T | -180.0 | 180.0 | deg |

## Motion Commands

All motion-like commands return `MachineCommandResult` with one of:

- `Success`: command finished and state changed.
- `Rejected`: backend interlock or soft-limit validation blocked execution.
- `Timeout`: command exceeded the caller-supplied timeout.
- `Cancelled`: caller cancellation or Stop interrupted the command.
- `Failed`: unexpected adapter/runtime failure.

Application orchestration:

- UI and feature view-models call `IMotionCommandUseCase`, not the simulator/controller directly.
- The use case creates `MachineCommandRequest` with command name, timeout, timestamp, typed parameters, and correlation ID.
- The same `MachineCommandRequest` is passed to the controller boundary so the recorded payload matches the executed payload.
- The controller result is recorded with the same request correlation ID.
- Command history is written through `IMotionCommandHistoryRepository`; `SqliteMotionCommandHistoryRepository` persists and reads recent command rows for MotionView.
- MotionView operator controls dispatch Servo On/Off, Home All, Jog +/- with operator-selected axis/step, Move Absolute with operator-entered X/Y/Z/Theta targets and profile preset/profile/tolerance values, and Stop through `IMotionCommandUseCase`.

### Servo On/Off

- Servo Off 중 motion command 금지
- EmergencyStop 발생 시 Servo Off 처리

### Home

- Home 완료 후 position = configured origin
- Homing 중 jog/move 금지
- Timeout 발생 시 axis alarm

### Jog

Modes:

- Step Jog: 0.01, 0.1, 1.0, 5.0 mm/deg
- Continuous Jog: optional P2

Rules:

- target must stay within soft limit
- command cancellation on stop
- UI must reflect moving status

### Move Absolute

Inputs:

- X/Y/Z/T target
- profile preset: Fine, Standard, Fast
- velocity profile: velocity, acceleration, deceleration, jerk
- arrival tolerance
- timeout

Acceptance:

- all axes arrive within tolerance
- final position stored in command log
- selected profile preset and requested profile/tolerance stored in command log

### Stop

- Stop is allowed when an axis is moving or sequence execution is active.
- Stop must request cancellation for the active motion command and return its own command result.
- The interrupted motion command returns `Cancelled` rather than `Success`.

## Teaching Point

```csharp
public sealed record TeachingPoint(
    Guid Id,
    string Name,
    TeachingRole Role,
    Position4D Position,
    PositionTolerance Tolerance,
    string? Memo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

Roles:

- Load
- Camera
- Inspection
- Review
- Safe
- Park
- Custom

Implementation status:

- `VisionCell.Motion.Teaching` defines the Teaching Point domain model, role enum, per-axis tolerance model, and creation validation.
- `VisionCell.Application.Teaching` defines the Save Current Position and Go To Teaching Point use case boundary, repository port, and explicit result statuses.
- Teaching point creation rejects empty names, unsupported roles, non-finite coordinates, coordinates outside the default axis soft limits, and non-positive tolerances.
- Save Current Position reads the current equipment snapshot with timeout/cancellation, validates duplicate names through the repository port, and saves only validated points.
- Go To Teaching Point loads a saved point and dispatches a traceable Move Absolute request through `IMotionCommandUseCase`.
- SQLite persistence supports save, ID lookup, duplicate-name lookup, and updated-time ordered list queries.
- WPF TeachingView binding and edit history remain follow-up work.

## Teaching Workflow

1. Connect equipment
2. Servo On
3. Home required axes
4. Jog to target position
5. Click `Save Current Position`
6. Enter point name/role/tolerance
7. Validate duplicate name and limits
8. Save to active recipe
9. Test `Go To Point`
10. Event log records action

## Teaching Acceptance

- Cannot save point outside soft limit.
- Cannot go to point while EStop or ServoOff.
- Editing point creates history row.
- Deleting point asks confirmation and logs event.

## Motion Test Cases

| Case | Expected |
|---|---|
| Home X success | IsHomed true, position origin |
| Jog selected axis within limit | position increments/decrements by requested step |
| Jog beyond soft limit | command rejected |
| Move while servo off | rejected with MOT-001 |
| Move while EStop | rejected with EQP-003 |
| Timeout injection | MOT-003 alarm |
