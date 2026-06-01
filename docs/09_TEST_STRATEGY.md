# 09. Test Strategy

## Test Pyramid

```text
Many: Unit tests for Core/Application/Motion/Vision
Some: Integration tests for Persistence/Simulator
Few: WPF ViewModel tests and manual UI verification
```

## Test Projects

```text
tests/VisionCell.Core.Tests
tests/VisionCell.Application.Tests
tests/VisionCell.Equipment.Tests
tests/VisionCell.Motion.Tests
tests/VisionCell.Vision.Tests
tests/VisionCell.Persistence.Tests
tests/VisionCell.App.Tests
```

## Required Tests by Area

### Core

- Value object validation
- Error code mapping
- Machine mode transition rules

### Motion

- Home success
- Jog within limit
- Jog beyond soft limit rejected
- Move while servo off rejected
- Timeout result handling

### Equipment

- Connect/disconnect
- EStop blocks command
- Door open blocks Auto
- I/O toggle updates snapshot

### Recipe

- Valid recipe passes
- Missing teaching fails
- ROI outside image fails
- Version validation

### Vision

- Normal image PASS
- Missing defect FAIL
- Offset defect FAIL
- Height lift FAIL
- Overlay output generated

### Application Sequence

- Full inspection success path
- Camera timeout failure path
- Stop/cancel path
- Persist result after judge

### Persistence

- Migrations apply
- Insert/query event
- Insert/query inspection result
- Repository handles relative path

### WPF ViewModels

- Command enabled conditions
- State update after snapshot
- Error event displayed
- Navigation updates selected screen

## Manual UI Acceptance

- Launch app
- Connect simulator
- Servo On
- Home axes
- Jog X/Y/Z/T
- Save teaching point
- Load recipe
- Start inspection
- See result overlay
- Open Offline Debug
- Re-inspect result
- Export CSV

## CI

GitHub Actions should run on `windows-latest`:

```powershell
dotnet restore .\VisionCell.sln
dotnet build .\VisionCell.sln -c Release --no-restore
dotnet test .\VisionCell.sln -c Release --no-build
```

## Quality Gates

- No build errors.
- P0/P1 tests pass.
- No skipped test without reason.
- No WPF business logic in code-behind.
- No blocking calls on UI thread.
