# 12. Self-Evolution Policy

## 목적

요구사항정의서는 출발점이지 감옥이 아니다. Codex는 고품질 제품을 만들기 위해 스스로 개선점을 발견하고 개발할 수 있다. 단, 통제된 방식으로만 한다.

## Allowed Autonomous Improvements

Codex may autonomously implement:

- small UI polish within existing design tokens
- view-model refactor with no behavior change
- missing null/validation handling
- additional tests for existing behavior
- logging improvements
- better error messages
- documentation clarification
- sample data expansion
- minor reusable control extraction

## Requires ADR Before Implementation

Codex must create ADR before:

- changing solution/project structure
- changing public interface contracts
- changing database schema
- changing recipe schema
- adding new external dependency
- replacing architecture pattern
- adding native C++/ONNX integration
- changing navigation/screen information architecture

ADR format:

```text
# ADR-000N: Title

Status: Proposed | Accepted | Rejected
Date:
Context:
Decision:
Alternatives:
Consequences:
Requirement impact:
Rollback:
```

## Requires Human Approval

- Removing P0/P1 requirement
- Reducing safety interlock strictness
- Disabling tests/CI
- Committing secrets or environment-specific path
- Adding paid/cloud service dependency
- Changing project identity/name

## Backlog Policy

Every discovered improvement should be classified:

| Class | Action |
|---|---|
| Bug | Create issue seed + fix if small |
| P0/P1 gap | Add to current or next sprint |
| Architecture debt | ADR + backlog |
| UI polish | Implement if local, otherwise backlog |
| Nice-to-have | P2/P3 backlog |

## Definition of Autonomous Done

An autonomous change is done only when:

- It does not conflict with existing P0/P1 requirements.
- It has an explanation in PR summary.
- It includes tests or clear reason no test is needed.
- It updates docs if behavior changed.
- It keeps architecture direction.

## Evolution Log

Codex should append major discoveries to `docs/BACKLOG.md` and optionally add `docs/EVOLUTION_LOG.md` once implementation starts.
