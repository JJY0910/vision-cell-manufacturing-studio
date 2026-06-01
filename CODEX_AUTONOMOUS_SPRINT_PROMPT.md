# Codex Autonomous Sprint Prompt

현재 저장소 상태를 점검하고 다음으로 개발해야 할 가장 가치 높은 작업을 스스로 선택해라.

절차:
1. `AGENTS.md`, `docs/BACKLOG.md`, `docs/01_REQUIREMENTS_SPEC.md`, 최근 커밋/브랜치 상태를 확인한다.
2. 아직 구현되지 않은 P0/P1 요구사항 중 의존성이 가장 낮은 것을 고른다.
3. 구현 전 아래 형식으로 계획을 출력한다.

```text
Selected requirement:
Why now:
Dependencies:
Files to change:
Implementation plan:
Test plan:
Expected PR title:
```

4. 구현한다.
5. 빌드/테스트를 실행한다.
6. 문서/Backlog/ADR을 갱신한다.
7. PR 요약문을 작성한다.

금지:
- P0/P1 요구사항 삭제
- UI 코드비하인드에 비즈니스 로직 추가
- 빌드 실패를 숨기기
- 임의로 큰 범위의 리라이트 수행
