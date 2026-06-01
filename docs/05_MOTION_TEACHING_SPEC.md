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
- velocity profile
- timeout

Acceptance:

- all axes arrive within tolerance
- final position stored in command log

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
| Jog X +1 within limit | position increments |
| Jog beyond soft limit | command rejected |
| Move while servo off | rejected with MOT-001 |
| Move while EStop | rejected with EQP-003 |
| Timeout injection | MOT-003 alarm |
