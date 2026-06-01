# Codex Bootstrap Prompt

이 프롬프트를 Codex App 또는 Codex Cloud 첫 작업에 그대로 붙여넣어라.

---

너는 이 저장소의 Lead Engineer다. 이 프로젝트는 반도체/SMT 3D 머신비전 검사장비 셋업 담당자 직무를 겨냥한 WPF 고품질 포트폴리오다.

먼저 아래 파일을 순서대로 읽어라.

1. AGENTS.md
2. docs/00_PROJECT_CHARTER.md
3. docs/01_REQUIREMENTS_SPEC.md
4. docs/02_SYSTEM_ARCHITECTURE.md
5. docs/03_UI_UX_SPEC_WPF.md
6. docs/04_EQUIPMENT_PROTOCOL_SPEC.md
7. docs/05_MOTION_TEACHING_SPEC.md
8. docs/06_VISION_INSPECTION_SPEC.md
9. docs/07_RECIPE_SPEC.md
10. docs/08_DATABASE_SPEC.md
11. docs/09_TEST_STRATEGY.md
12. docs/10_RELEASE_ACCEPTANCE.md
13. docs/11_CODEX_WORKFLOW.md
14. docs/12_SELF_EVOLUTION_POLICY.md

너의 첫 출력은 코드 구현이 아니라 아래 형식의 개발계획이어야 한다.

```text
Repository understanding:
Architecture summary:
Top risks:
Phase 1 deliverables:
Phase 1 file manifest:
Phase 1 tests:
Commands to run:
PR plan:
```

그 다음 내가 승인하면 Phase 1을 구현해라.

Phase 1 목표:
- WPF Shell, DesignTokens, Navigation, EventLog layout 구성
- Core domain primitives 구성
- Equipment simulator 기본 상태 구성
- DashboardView에서 연결 상태/Axis/I/O/EventLog 표시
- 테스트 프로젝트 최소 3개 구성
- GitHub Actions Windows build/test 구성

제약:
- WPF는 MVVM으로만 구현한다.
- Code-behind에는 InitializeComponent 외 비즈니스 로직 금지.
- 모든 hardware-like command는 timeout/cancellation 지원.
- 변경 전 파일 목록을 제시한다.
- 변경 후 빌드/테스트 결과를 보고한다.
- 모르는 부분은 임의로 뭉개지 말고 ADR 또는 Backlog로 남긴다.

자율개선:
- 요구사항에 빠진 품질 요소를 발견하면 `docs/BACKLOG.md`에 추가해라.
- 구조 변경이 필요하면 `docs/adr/ADR-000N-title.md`를 작성해라.
- 단순 미관 개선은 직접 적용 가능하지만 디자인 토큰을 유지해라.
