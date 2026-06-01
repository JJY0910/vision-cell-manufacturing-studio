# Command Interlock Matrix

This is the Phase 1 design baseline for HMI command enablement and backend validation. UI disabled state is not enough; each command also needs backend validation before execution.

## State Inputs

| State | Meaning |
|---|---|
| `connected` | Controller connection is active. |
| `servoOn` | Servo output is enabled. |
| `homed` | Required axis or axes are homed. |
| `axisBusy` | One or more axes are homing or moving. |
| `emergencyStop` | Emergency stop input is active. |
| `doorClosed` | Door closed sensor is active. |
| `manualMode` | Machine mode is Manual. |
| `autoMode` | Machine mode is Auto. |
| `recipeLoaded` | Active recipe is selected and valid enough to run. |
| `safetyOk` | `!emergencyStop && doorClosed` plus required utility states. |
| `withinSoftLimit` | Target position is inside configured axis soft limit. |

## Matrix

| Command | Enable Conditions | Reject Conditions | Notes |
|---|---|---|---|
| Connect | `!connected` | None for simulator baseline | Must support timeout/cancellation. |
| Disconnect | `connected && !axisBusy` | `axisBusy` | Should move to safe state when later hardware drivers exist. |
| Servo On | `connected && manualMode && safetyOk && !axisBusy` | `!connected`, `emergencyStop`, `!doorClosed`, `axisBusy` | Backend must emit explicit interlock failure. |
| Servo Off | `connected && servoOn` | `axisBusy` unless Stop has completed | Emergency stop may force Servo Off. |
| Home | `connected && manualMode && servoOn && safetyOk && !axisBusy` | `!servoOn`, `emergencyStop`, `!doorClosed`, `axisBusy` | Per-axis and Home All both need cancellation. |
| Jog | `connected && manualMode && servoOn && homed && safetyOk && !axisBusy && withinSoftLimit` | `!homed`, `!withinSoftLimit`, `axisBusy`, `autoMode` | Continuous jog is later scope. |
| Move Absolute | `connected && manualMode && servoOn && homed && safetyOk && !axisBusy && withinSoftLimit` | `!homed`, `!withinSoftLimit`, `axisBusy`, `autoMode` | Must log target and elapsed time. |
| Stop | `connected && axisBusy` | `!connected` | Stop must be cancellable-safe and produce a command result. |
| Reset Alarm | `connected && !emergencyStop && !axisBusy` | `emergencyStop`, `axisBusy` | Must validate current root cause before clearing. |
| Run Inspection | `connected && autoMode && recipeLoaded && homed && safetyOk && !axisBusy` | `manualMode`, `!recipeLoaded`, `!homed`, `emergencyStop`, `!doorClosed`, `axisBusy` | Phase 1 only defines the interlock; implementation is later. |

## Follow-Up

- Implement command state objects in each screen ViewModel.
- Add backend validation tests for each reject condition.
- Add structured `SystemEvent` entries for every rejected command.
