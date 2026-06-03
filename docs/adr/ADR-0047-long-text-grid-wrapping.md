# ADR-0047: Long Text Grid Wrapping

Status: Accepted
Date: 2026-06-03

## Context

Priority 5 UI QA requires dense HMI screens to remain readable at constrained widths. Several GridView columns carried long command messages, alarm messages, artifact paths, validation summaries, or correlation IDs through `DisplayMemberBinding`. That is compact, but it can clip long operator text and make diagnostic rows harder to read.

## Decision

- Convert selected long-text GridView columns to `CellTemplate` text blocks with `TextWrapping="Wrap"`.
- Cover Motion command messages, Equipment fault-event messages, Equipment I/O transition sources, Recipe validation summaries, Offline Debug overlay/artifact status, and Alarm message/correlation columns.
- Add an App XAML QA test that verifies these long-text columns keep wrapping templates.
- Do not change ViewModels, data contracts, persistence, or command behavior.

## Alternatives

- Leave long text clipped: rejected because diagnostic HMI rows should remain readable without guessing hidden text.
- Add horizontal workspace scrolling: rejected because priority screens should keep vertical reachability and avoid parent-width drift.
- Replace tables with separate detail panels in this slice: deferred because wrapping columns is a smaller, safer UI QA improvement.

## Consequences

- Long diagnostic text is more readable in the existing HMI tables.
- Some table rows may grow taller when text is long, which is acceptable because the screens already use vertical scrolling.
- Actual physical panel readability still needs field validation.

## Requirement impact

- FR-006: constrained-width HMI readability improves for long diagnostic rows.
- FR-063/FR-084/FR-120/FR-200/FR-222: motion history, I/O transition, recipe, alarm, and offline-debug diagnostic text remains visible.
- NFR-001/NFR-007/NFR-TEST-001 remain supported.

## Rollback

Restore the affected `DisplayMemberBinding` columns and remove the long-text wrapping XAML QA test and documentation updates.
