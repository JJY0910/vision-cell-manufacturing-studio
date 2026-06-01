# 11. Codex Workflow

## 결론

이 프로젝트는 Codex만으로 충분히 진행 가능하다. 단, WPF는 Windows 전용이므로 실제 UI 빌드/실행 검증은 Windows 로컬 Codex App 또는 Windows GitHub Actions에서 수행한다.

## Local Codex 역할

- `C:\Dev\VisionCell-Pemtron-WPF-Codex` 폴더를 열고 작업
- WPF 앱 빌드/실행
- Visual Studio/.NET SDK 기반 테스트
- UI 수정/리팩터링/버그 수정

## Codex Cloud 역할

- GitHub repo 연결 후 Issue/PR 단위 개발
- 문서/요구사항/코드 리뷰
- 구현 PR 생성
- 리뷰 코멘트 기반 수정

## GitHub 역할

- Issue 관리
- PR 관리
- Windows build/test CI
- Codex review trigger
- Release notes

## 권장 루프

```text
Requirement ID 선택
→ GitHub Issue 생성
→ Codex에 Issue 기반 구현 요청
→ Codex가 계획/파일목록/테스트계획 제시
→ 구현
→ Windows CI build/test
→ @codex review
→ 수정
→ merge
→ 다음 requirement
```

## Codex에 던지는 작업 단위 예시

### Phase 1

```text
Implement FR-001, FR-002, FR-003, FR-005.
Create WPF ShellWindow with top status bar, side nav, workspace, bottom event log.
Use MVVM and reusable controls.
Before coding, list files to create/modify and test plan.
```

### Phase 2

```text
Implement FR-020, FR-021, FR-022 and equipment simulator.
Create IEquipmentController and VirtualEquipmentController.
Add timeout/cancellation and unit tests.
```

### Phase 3

```text
Implement FR-060, FR-061, FR-062, FR-066.
Add Axis state, Home/Jog, soft limit validation, MotionView binding, tests.
```

### Phase 4

```text
Implement FR-100, FR-120, FR-124.
Add TeachingView, Recipe model, JSON validation, and persistence interfaces.
```

### Phase 5

```text
Implement FR-160, FR-161, FR-180, FR-200.
Create inspection sequence, vision result, overlay generation placeholder, SQLite result storage.
```

## PR Review Prompt

```text
@codex review for WPF MVVM violations, UI thread blocking, missing timeout/cancellation, unsafe file IO, missing tests, schema migration issues, and requirement coverage gaps.
```

## Codex Self-Improvement Prompt

```text
Inspect the repo and docs. Find the highest-value missing P0/P1 requirement that can be implemented in one PR. Propose the plan first. If you discover architecture debt, write an ADR before coding.
```

## When Codex gets stuck

Codex must write:

```text
Blocked reason:
What was attempted:
Evidence/logs:
Smallest next action:
Human decision needed:
```

It must not silently fake success.
