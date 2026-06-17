# VisionCell Manufacturing Studio

> 반도체/SMT 3D 머신비전 검사장비 셋업 업무를 WPF 기반으로 재현하는 포트폴리오 프로젝트입니다.  
> 목표는 단순 데모가 아니라, 장비 셋업 담당자가 실제로 다루는 `Controller / Motion / I/O / Teaching / Recipe / Inspection / Offline Debug` 흐름을 회사 프로젝트 수준으로 구현하는 것입니다.

## 핵심 콘셉트

- WPF HMI에서 장비 상태, 모터, I/O, 카메라, 레시피, 검사 시퀀스를 제어합니다.
- 실제 장비 없이 `Hardware Simulator`가 Controller, Axis, Sensor, Camera, Error를 재현합니다.
- Inspection Sequence는 `Load Recipe → Safety Check → Home/Move → Grab → Inspect → Judge → Save → Report` 흐름으로 동작합니다.
- 검사 결과는 SQLite에 저장하고, Offline Debug Station에서 과거 검사 데이터를 재현/재검사합니다.
- Codex가 이 저장소를 읽고 스스로 계획, 구현, 테스트, PR까지 진행할 수 있도록 `AGENTS.md`, 요구사항, UI 명세, 테스트전략, 자율개선 정책을 포함합니다.

## 권장 로컬 위치

```powershell
C:\Dev\VisionCell-WPF-Codex
```

## 빠른 시작

```powershell
cd C:\Dev\VisionCell-WPF-Codex
powershell -ExecutionPolicy Bypass -File .\tools\bootstrap\init-solution.ps1
powershell -ExecutionPolicy Bypass -File .\tools\git\init-git-and-github.ps1 -RepoName vision-cell-manufacturing-studio -Visibility private
```

그 다음 Codex App 또는 Codex CLI에서 이 폴더를 프로젝트로 선택하고 `CODEX_BOOTSTRAP_PROMPT.md` 내용을 그대로 던지면 됩니다.

## 문서 읽는 순서

1. `AGENTS.md`
2. `docs/00_PROJECT_CHARTER.md`
3. `docs/01_REQUIREMENTS_SPEC.md`
4. `docs/02_SYSTEM_ARCHITECTURE.md`
5. `docs/03_UI_UX_SPEC_WPF.md`
6. `docs/04_EQUIPMENT_PROTOCOL_SPEC.md`
7. `docs/05_MOTION_TEACHING_SPEC.md`
8. `docs/06_VISION_INSPECTION_SPEC.md`
9. `docs/07_RECIPE_SPEC.md`
10. `docs/08_DATABASE_SPEC.md`
11. `docs/09_TEST_STRATEGY.md`
12. `docs/10_RELEASE_ACCEPTANCE.md`
13. `docs/11_CODEX_WORKFLOW.md`
14. `docs/12_SELF_EVOLUTION_POLICY.md`

## 프로젝트 레이어

```text
src/VisionCell.App             WPF HMI Shell, Views, ViewModels, Styles
src/VisionCell.Core            도메인 모델, 공통 타입, 값 객체
src/VisionCell.Application     UseCase, 시퀀스 오케스트레이션, DTO
src/VisionCell.Equipment       Controller, Camera, I/O, Safety 추상화
src/VisionCell.Motion          Axis, Homing, Jog, Teaching, Motion Profile
src/VisionCell.Vision          2D/3D 검사 알고리즘, Overlay, Judge
src/VisionCell.Persistence     SQLite Repository, Schema Migration
src/VisionCell.Simulator       가상 장비 시뮬레이터
src/VisionCell.Telemetry       로그, 이벤트, 성능 측정
native/VisionCell.NativeVision C++ OpenCV 엔진 후보; 선택 구현
```

## Codex 운영 원칙

- 한 PR은 한 기능 단위입니다.
- 구현 전 `수정 파일 목록`, `테스트 계획`, `요구사항 ID`를 먼저 제시해야 합니다.
- 빌드/테스트가 안 되면 원인과 다음 액션을 문서화합니다.
- 요구사항을 고정된 벽으로 보지 않고, 품질 향상이 필요한 부분은 ADR과 Backlog를 생성한 뒤 개선합니다.
