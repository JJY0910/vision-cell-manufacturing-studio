# 04. Equipment Protocol Specification

## 목적

실제 장비가 없어도 Controller, Camera, I/O, Safety 상태를 일관된 프로토콜로 시뮬레이션한다.

## Equipment State

```csharp
public sealed record EquipmentSnapshot(
    bool IsConnected,
    MachineMode Mode,
    SafetySnapshot Safety,
    IReadOnlyList<AxisSnapshot> Axes,
    IoSnapshot Io,
    CameraSnapshot Camera,
    AlarmSnapshot? Alarm,
    DateTimeOffset Timestamp);
```

## Controller Commands

| Command | Input | Output | Timeout | Interlock |
|---|---|---|---:|---|
| Connect | profileId | Connected | 3000ms | none |
| Disconnect | none | Disconnected | 1000ms | none |
| EnterManualMode | none | ModeChanged(Manual) | 1000ms | connected, idle |
| EnterAutoMode | none | ModeChanged(Auto) | 1000ms | connected, servo on, homed, safety/camera/I/O ready |
| ResetAlarm | none | AlarmCleared | 2000ms | EStop off |
| GetSnapshot | none | EquipmentSnapshot | 500ms | none |

## I/O Bits

| Name | Direction | Default | Description |
|---|---|---:|---|
| DI_DOOR_CLOSED | Input | true | Door close sensor |
| DI_ESTOP_ON | Input | false | Emergency stop |
| DI_AIR_PRESSURE_OK | Input | true | Air pressure |
| DI_PRODUCT_PRESENT | Input | true | Product detect |
| DI_CAMERA_READY | Input | true | Camera ready |
| DI_VACUUM_OK | Input | true | Vacuum sensor ready |
| DI_SERVO_ALARM | Input | false | Servo alarm input |
| DO_SERVO_ENABLE | Output | false | Servo enable |
| DO_VACUUM_ON | Output | false | Vacuum output |
| DO_RING_LIGHT_ON | Output | false | Light output |
| DO_BUZZER_ON | Output | false | Buzzer |
| DO_TOWER_GREEN | Output | false | Tower lamp green |
| DO_TOWER_YELLOW | Output | false | Tower lamp yellow |
| DO_TOWER_RED | Output | false | Tower lamp red |

## Error Codes

| Code | Severity | Meaning | Recovery |
|---|---|---|---|
| EQP-001 | Error | Controller connection failed | Check profile and reconnect |
| EQP-002 | Error | Heartbeat lost | Reconnect controller |
| EQP-003 | Alarm | Emergency stop active | Release EStop and reset |
| EQP-004 | Warning | Door open | Close door |
| EQP-008 | Alarm | Air pressure low | Restore air pressure and reset |
| EQP-009 | Warning | Vacuum loss | Check vacuum line / product pickup |
| MOT-001 | Alarm | Servo off | Servo on before motion |
| MOT-002 | Alarm | Axis not homed | Home axis |
| MOT-003 | Error | Motion timeout | Check soft limit / reset |
| MOT-004 | Error | Soft limit exceeded | Change target |
| MOT-005 | Alarm | Servo alarm active | Clear servo drive alarm and reset |
| CAM-001 | Error | Camera grab timeout | Retry grab |
| CAM-002 | Error | Camera not ready | Check camera status |
| CAM-003 | Error | Camera grab failed | Check simulator injection / retry grab |
| VIS-001 | Warning | Recipe validation failed | Fix recipe |
| VIS-002 | Error | Inspection failed | Review image/params |
| DB-001 | Error | Persistence failed | Check database path |

## Simulator Failure Injection

Simulator must support:

- command latency
- heartbeat loss
- motion timeout
- camera grab failure
- random image defect rate
- EStop/Door toggles
- Air pressure, vacuum, camera-ready, and servo-alarm fault injection
- DB disabled mode for testing UI error path

Current simulator fault injection is exposed through `IEquipmentFaultInjector` and `EquipmentFaultInjectionUseCase`. It is a simulator diagnostics path, not a real PLC or safety relay control path.

Successful simulator fault injection compares I/O snapshots before and after the command and persists changed bit transitions through `IEquipmentIoTransitionRepository`. Stored transition rows include bit name, address, direction, previous/current value, previous/current forced state, source command, correlation ID, operator memo, and timestamp. This is not PLC scan polling and does not validate real safety wiring.

## Hardware Adapter Boundary

Future real hardware integration must stay behind `IEquipmentController` and the adapter contracts in `VisionCell.Equipment.Hardware`.

- `IMotionControllerAdapter`: reads axis snapshots and executes Servo/Home/Jog/Move/Stop commands.
- `ICameraAdapter`: reads camera readiness and executes frame acquisition.
- `IPlcIoAdapter`: reads digital I/O and writes allowed outputs.
- `HardwareAdapterStatus`: reports adapter connected/ready/endpoint state.

No vendor SDK object, PLC frame, raw fieldbus payload, or camera buffer handle may cross into WPF or Application use cases.

## Snapshot Update Policy

- UI refresh target: 250ms~1000ms
- Event-based update preferred, polling fallback acceptable
- Each snapshot has timestamp and correlationId where applicable

## Safety Validation

Before motion:

```text
Connected == true
EStop == false
DoorClosed == true OR Manual mode with override disabled policy
ServoEnabled == true
Axis not moving
Target within soft limit
```

Before inspection:

```text
Connected == true
ActiveRecipe != null
EStop == false
DoorClosed == true
CameraReady == true
All required axes homed
Teaching points valid
Recipe validation passed
```
