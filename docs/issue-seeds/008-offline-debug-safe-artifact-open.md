# FR-221 FR-222: Offline Debug Safe Artifact Open

## Requirement IDs

FR-221 FR-222 FR-260 NFR-006 NFR-008 NFR-009 NFR-010

## Goal

Implement safe operator-confirmed external opening for Offline Debug overlay and height-map artifacts, following ADR-0035.

## Acceptance Criteria

- [ ] Add an Application-facing artifact open request/result boundary.
- [ ] Reuse the existing relative artifact path safety policy.
- [ ] Reject rooted paths, traversal segments, not-recorded paths, missing files, and unavailable files with operator-visible messages.
- [ ] Add explicit Overlay and Height Map open commands in Offline Debug.
- [ ] Require operator confirmation before launching an external viewer.
- [ ] Keep WPF code-behind free of business logic, path resolution, and process-launch policy.
- [ ] Do not execute Re-inspect as part of artifact open.
- [ ] Add Application/Persistence/App tests for available, missing, unsafe, declined-confirmation, and success paths.
- [ ] Run Debug/Release build/test and document real field validation limits.

## Codex Prompt

```text
Read AGENTS.md and ADR-0035. Implement safe Offline Debug artifact open commands in one PR-sized change. Keep path resolution outside WPF, require operator confirmation, add tests for unsafe/missing/declined/success paths, run Debug/Release validation, and document limits.
```
