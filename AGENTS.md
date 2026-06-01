# AGENTS.md — VisionCell Manufacturing Studio

## Mission

Codex는 이 저장소를 단순 데모가 아니라, 반도체/SMT 3D 머신비전 검사장비 셋업 담당자 직무에 맞는 고품질 WPF 포트폴리오 제품으로 개발한다.

최종 제품은 다음 질문에 답해야 한다.

1. 이 사람이 장비 셋업 업무의 흐름을 이해하는가?
2. 이 사람이 WPF/C#/Visual Studio 기반 현장 HMI를 만들 수 있는가?
3. 이 사람이 Motor, I/O, Teaching, Recipe, 검사 시퀀스, Offline Debug를 구조적으로 구현할 수 있는가?
4. 이 사람이 GitHub Issue/PR/CI/Review 기반으로 회사식 개발을 할 수 있는가?

## Required reading order before any implementation

1. `docs/00_PROJECT_CHARTER.md`
2. `docs/01_REQUIREMENTS_SPEC.md`
3. `docs/02_SYSTEM_ARCHITECTURE.md`
4. `docs/03_UI_UX_SPEC_WPF.md`
5. `docs/04_EQUIPMENT_PROTOCOL_SPEC.md`
6. `docs/05_MOTION_TEACHING_SPEC.md`
7. `docs/06_VISION_INSPECTION_SPEC.md`
8. `docs/07_RECIPE_SPEC.md`
9. `docs/08_DATABASE_SPEC.md`
10. `docs/09_TEST_STRATEGY.md`
11. `docs/10_RELEASE_ACCEPTANCE.md`
12. `docs/11_CODEX_WORKFLOW.md`
13. `docs/12_SELF_EVOLUTION_POLICY.md`

## Non-negotiable architecture rules

- WPF UI must use MVVM. Do not put business logic in code-behind.
- `VisionCell.App` may reference Application/Core/Telemetry, but must not directly own hardware state logic.
- `VisionCell.Application` orchestrates use cases and sequences.
- `VisionCell.Core` must not reference WPF, SQLite, OpenCV, HTTP, or file-system-specific code.
- Hardware access must go through interfaces: `IEquipmentController`, `IAxisController`, `ICameraDevice`, `IDigitalIoDevice`.
- Every hardware-like command must support timeout and cancellation.
- UI thread must never block on motion, camera, DB, or inspection work.
- All commands must write structured operation logs.
- Every feature must be traceable to one or more requirement IDs.
- No hard-coded absolute user path in source code. Absolute paths may appear only in docs/scripts.
- No API keys, tokens, credentials, or private machine paths in commits.

## WPF quality rules

- Use `ShellWindow` layout with left navigation, top status bar, main workspace, bottom event log.
- Use reusable controls for status pill, axis card, I/O bit, KPI card, sequence step, image viewport, ROI overlay, recipe editor field.
- Maintain consistent spacing, typography, and visual hierarchy via `Themes/DesignTokens.xaml`.
- Use command binding, async commands, validation, and view-model state objects.
- All long work must surface progress and cancellability.
- Show clear states: Connected, Disconnected, Homing, Moving, Ready, Alarm, EmergencyStop, Inspecting, Paused.
- Never hide errors. Convert exceptions into user-facing `SystemEvent` and developer log.

## Development workflow required from Codex

For every task, respond first with:

```text
Task Summary:
Requirement IDs:
Files to create/modify:
Implementation plan:
Test plan:
Risk / rollback:
```

Then implement in small commits or PR-sized patches.

After implementation, provide:

```text
Changed files:
Build/test commands run:
Test results:
Requirement coverage:
Known gaps:
Next recommended issue:
```

## Autonomy and self-development mandate

Codex is allowed and expected to improve the product beyond the literal task when it discovers quality gaps. However, it must follow this protocol:

1. If improvement is local and non-breaking, implement it and mention it in the summary.
2. If improvement changes architecture, public contract, UI navigation, DB schema, or acceptance criteria, create/update an ADR in `docs/adr/` before implementation.
3. If improvement is useful but too large for the current PR, add it to `docs/BACKLOG.md` and optionally create an issue seed in `docs/issue-seeds/`.
4. Do not delete or weaken P0/P1 requirements without explicit human approval.
5. If a requirement conflicts with buildability or UX quality, propose the smallest requirement change and document the tradeoff.

## Build commands

Preferred local Windows commands:

```powershell
dotnet restore .\VisionCell.sln
dotnet build .\VisionCell.sln -c Debug
dotnet test .\VisionCell.sln -c Debug
```

For WPF visual verification:

```powershell
dotnet run --project .\src\VisionCell.App\VisionCell.App.csproj
```

## Review guidelines

Codex review must flag P0/P1 issues for:

- UI freeze risk caused by `.Result`, `.Wait()`, synchronous I/O, or blocking hardware calls.
- Missing timeout/cancellation for controller/motion/camera/inspection operations.
- Business logic inside WPF code-behind.
- Unhandled exception path in user-triggered command.
- Missing structured log for machine command.
- Missing test for domain/application/motion/vision logic.
- Unsafe file I/O path traversal.
- Secret/token in code, config, sample data, or logs.
- Schema change without migration.
- Requirement implemented without acceptance evidence.

## Branch and PR naming

- Branch: `feature/FR-003-axis-jog`, `feature/FR-016-offline-debug`, `fix/BUG-001-motion-timeout`
- PR title: `feat(FR-003): implement axis jog command and UI binding`
- Commit style: `feat`, `fix`, `test`, `docs`, `refactor`, `chore`

## Git and repository operations

- Always run `git status --short --branch` before changing files.
- Prefer feature branches for implementation work. Keep `main` release-ready and use `develop` for integration when GitHub is connected.
- After changes, run restore, build, and test before committing:

```powershell
dotnet restore .\VisionCell.sln
dotnet build .\VisionCell.sln -c Debug --no-restore
dotnet test .\VisionCell.sln -c Debug --no-build
dotnet build .\VisionCell.sln -c Release --no-restore
dotnet test .\VisionCell.sln -c Release --no-build
```

- Do not commit when restore, build, or tests fail.
- Never commit build/local artifacts: `bin/`, `obj/`, `.vs/`, `TestResults/`, `artifacts/`, `out/`, `*.db`, `*.sqlite`, `*.log`, `*.user`, `*.suo`.
- Any implementation change must update the related docs or an ADR when behavior, architecture, public contract, workflow, schema, or acceptance criteria changes.
- WPF code-behind must not contain business logic. Keep WPF MVVM.
- Every hardware-like command must expose timeout, cancellation, and an explicit error/result path.

## Required Definition of Done

A task is done only when:

- Related requirement IDs are listed.
- New/changed code builds.
- Tests are added or a justified reason is documented.
- UI state is validated manually or with a view-model test.
- Logs/errors are handled.
- Docs are updated if behavior changed.
- No unrelated churn is included.
