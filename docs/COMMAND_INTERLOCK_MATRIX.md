# Command Interlock Matrix

This is the Phase 1 implementation baseline for HMI command enablement and backend validation. UI disabled state is not enough; each command also needs backend validation before execution.

## State Inputs

| State | Meaning |
|---|---|
| `connected` | Controller connection is active. |
| `servoOn` | Servo output is enabled. |
| `homed` | Required axis or axes are homed. |
| `axisBusy` | One or more axes are homing or moving. |
| `axisAlarm` | One or more axes have an active alarm. |
| `emergencyStop` | Emergency stop input is active. |
| `doorClosed` | Door closed sensor is active. |
| `manualMode` | Machine mode is Manual. |
| `autoMode` | Machine mode is Auto. |
| `recipeLoaded` | Active recipe is selected and valid enough to run. |
| `safetyOk` | `!emergencyStop && doorClosed` plus required utility states. |
| `withinSoftLimit` | Target position is inside configured axis soft limit. |
| `controllerBusy` | Controller command is in progress. |
| `sequenceRunning` | Inspection sequence is running. |
| `cameraConnected` | Camera simulator or real camera is connected. |
| `ioReady` | Required digital I/O and utilities are ready. |
| `alarmActive` | Alarm is active and can be reset. |

## Matrix

| Command | Enable Conditions | Reject Conditions | Notes |
|---|---|---|---|
| Connect | `!connected && !controllerBusy` | `connected`, `controllerBusy` | Must support timeout/cancellation. |
| Disconnect | `connected && !sequenceRunning && !controllerBusy` | `!connected`, `sequenceRunning`, `controllerBusy` | Should move to safe state when later hardware drivers exist. |
| Servo On | `connected && safetyOk && !emergencyStop && doorClosed && !axisAlarm && !axisBusy` | `!connected`, `!safetyOk`, `emergencyStop`, `!doorClosed`, `axisAlarm`, `axisBusy` | Backend emits explicit interlock failure. |
| Servo Off | `connected && servoOn && !axisBusy && !sequenceRunning` | `!connected`, `!servoOn`, `axisBusy`, `sequenceRunning` | Emergency stop may force Servo Off in future hardware adapter. |
| Home | `connected && manualMode && servoOn && safetyOk && !axisBusy && !sequenceRunning` | `!connected`, `autoMode`, `!servoOn`, `!safetyOk`, `axisBusy`, `sequenceRunning` | Simulator baseline returns success, timeout, cancellation, and stopped-before-completion results. |
| Jog | `connected && manualMode && servoOn && safetyOk && !axisBusy && !sequenceRunning && withinSoftLimit` | `!connected`, `autoMode`, `!servoOn`, `!safetyOk`, `axisBusy`, `sequenceRunning`, `!withinSoftLimit` | Jog does not require homing for setup/teaching movement. |
| Move Absolute | `connected && manualMode && servoOn && axisHomed && safetyOk && !axisBusy && !sequenceRunning && withinSoftLimit` | `!connected`, `autoMode`, `!servoOn`, `!axisHomed`, `!safetyOk`, `axisBusy`, `sequenceRunning`, `!withinSoftLimit` | Simulator baseline maps soft-limit rejects to `MOT-004` and timeout alarms to `MOT-003`; persisted motion history remains follow-up work. |
| Stop | `connected && (axisBusy || sequenceRunning)` | `!connected`, `!axisBusy && !sequenceRunning` | Stop must be cancellable-safe and produce a command result. |
| Reset Alarm | `connected && alarmActive && !emergencyStop && doorClosed` | `!connected`, `!alarmActive`, `emergencyStop`, `!doorClosed` | Must validate current root cause before clearing. |
| Run Inspection | `connected && autoMode && recipeLoaded && cameraConnected && ioReady && safetyOk && !sequenceRunning && allRequiredAxesHomed && !axisBusy && !axisAlarm` | `!connected`, `manualMode`, `!recipeLoaded`, `!cameraConnected`, `!ioReady`, `!safetyOk`, `sequenceRunning`, `!allRequiredAxesHomed`, `axisBusy`, `axisAlarm` | Phase 1 implements interlock baseline only; inspection execution remains later scope. |

## Follow-Up

- Extend command state objects from Dashboard to Motion and Inspection views when those commands get full user-facing handlers.
- Persist motion command request/result records into `motion_command_history`.
- Add hardware adapter validation tests when real controller, motion, camera, and I/O adapters exist.
- Add structured `SystemEvent` entries for every persisted command result.
