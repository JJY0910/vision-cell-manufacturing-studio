namespace VisionCell.Core.Interlocks;

public sealed record InterlockViolation(
    string Code,
    InterlockSeverity Severity,
    string Message);
