# Validation Scope

## Confirmed In Current Environment

- Local .NET 8 restore, Debug build, Release build, and automated tests run on the developer workstation.
- GitHub Actions `Windows WPF Build and Test` runs restore/build/test on `windows-latest`.
- WPF launch smoke verifies that `VisionCell.App.exe` starts and remains alive for five seconds.
- Equipment behavior is validated against `VirtualEquipmentController`, virtual camera, deterministic 2D/3D vision engines, SQLite repositories, and local file-system artifact storage.
- Offline Debug validation covers SQLite result reads, relative artifact metadata checks, deterministic BMP preview decoding, and Re-inspect preparation state.
- Alarm Center validation covers Core alarm mapping, Application failure recorder calls, SQLite alarm save/list/acknowledge, and WPF AlarmView ViewModel state against simulator/Application failure paths.
- Hardware Adapter Boundary validation covers interface contracts and fake adapter tests only.
- I/O Monitor and Fault Injection validation covers `VirtualEquipmentController`, Application fault injection use case, WPF `EquipmentViewModel`, simulator I/O forced-state rows, interlock blocking, and Application alarm recorder calls.

## Not Yet Validated

- No real Pemtron, PLC, motion controller, camera, light controller, or 3D sensor hardware has been connected.
- No real fieldbus, serial, Ethernet, vendor SDK, trigger timing, encoder, servo alarm, or safety relay path has been validated.
- Alarm rows are produced from simulator/Application paths only; no real PLC/vendor alarm source or safety relay acknowledgement has been validated.
- Hardware adapter contracts are defined, but no `RealEquipmentController`, vendor SDK, PLC protocol, fieldbus, or camera trigger implementation has been validated.
- Fault injection is simulator-only; no real EStop circuit, door switch, vacuum sensor, air pressure switch, camera ready line, servo drive alarm, PLC output write, or safety relay reset path has been validated.
- No production calibration, metrology accuracy, takt-time, thermal, vibration, EMI, or long-duration burn-in validation has been performed.
- Offline Debug Re-inspect currently prepares context only; it does not replay a real inspection sequence.
- Artifact preview currently supports the deterministic uncompressed 24-bit BMP files produced by this project, not arbitrary customer image formats.
- WPF visual QA has not yet been completed on actual shop-floor displays or touch panels.

## Reporting Rule

Do not claim final production readiness until real hardware adapter validation, field I/O safety checks, camera acquisition validation, inspection accuracy evidence, and operator HMI visual QA are completed and documented.
