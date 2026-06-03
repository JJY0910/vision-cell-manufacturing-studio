# ADR-0040: Offline Debug Re-inspect Recipe Policy

Status: Accepted
Date: 2026-06-03

## Context

Offline Debug can prepare a historical result context, run a metadata comparison, and persist comparison history. The remaining FR-222 gap is that operators need to understand whether the selected historical result uses the same Recipe as the current active Recipe before any future replay path is considered.

The project still has no source-image replay runner or real equipment. Recipe policy visibility must not call the live inspection sequence or imply that current-vs-historical replay execution is validated.

## Decision

- Add `IInspectionReinspectRecipePolicyUseCase` in the Application inspection boundary.
- Resolve active Recipe metadata through `IActiveRecipeContext` inside the Application layer.
- Compare current active Recipe ID/version against the selected historical result Recipe ID/version.
- Display policy summary/detail in Offline Debug after Prepare Re-inspect.
- Keep source-image replay, current-vs-historical replay execution, and new inspection-result persistence out of scope.

## Consequences

- Operators can see whether active Recipe metadata matches, differs, is invalid, unavailable, or absent before running the offline metadata comparison.
- WPF does not inject `IActiveRecipeContext` directly and does not run the live sequence path.
- The policy is read-only metadata guidance and not a replay executor.

## Requirement Coverage

- FR-220/FR-221: Offline Debug continues to inspect historical result context.
- FR-222: Current-vs-historical Recipe policy is visible before metadata comparison.
- FR-240: Uses active Recipe metadata managed by the application configuration/repository boundary.
- FR-260: The decision is traceable through this ADR, tests, and PR workflow.
- NFR-001/NFR-006/NFR-009/NFR-TEST-001: Application orchestration remains outside WPF code-behind and is covered by Application/ViewModel/XAML tests.

## Rollback

Remove `IInspectionReinspectRecipePolicyUseCase`, `InspectionReinspectRecipePolicyUseCase`, Offline Debug policy bindings, tests, this ADR, and related documentation updates. The ADR-0039 metadata comparison history behavior remains intact.
