You are reviewing a WPF equipment-control portfolio project.

Read AGENTS.md first. Review the pull request for only serious issues:

- WPF MVVM violations
- business logic in code-behind
- UI thread blocking
- missing timeout/cancellation for hardware-like commands
- missing safety interlock validation
- missing tests for changed domain/application logic
- unsafe file IO
- schema change without migration
- secret/token leakage
- requirement coverage mismatch

Return concise P0/P1 findings with file path, reason, and suggested fix. If no serious issues, say so.
