using VisionCell.Core.Primitives;
using VisionCell.Motion.Axes;

namespace VisionCell.Motion.Teaching;

public static class TeachingPointFactory
{
    private static readonly IReadOnlyDictionary<AxisId, SoftLimit> DefaultSoftLimits =
        AxisDefaults.CreatePowerOffAxes().ToDictionary(axis => axis.AxisId, axis => axis.SoftLimit);

    public static TeachingPointCreateResult Create(
        string name,
        TeachingRole role,
        Position4D position,
        PositionTolerance tolerance,
        string? memo = null,
        Guid? id = null,
        DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(tolerance);

        var issues = new List<TeachingPointValidationIssue>();
        var normalizedName = NormalizeName(name, issues);
        ValidateRole(role, issues);
        ValidatePosition(position, issues);
        ValidateTolerance(tolerance, issues);

        var pointId = id ?? Guid.NewGuid();
        if (pointId == Guid.Empty)
        {
            issues.Add(new TeachingPointValidationIssue(
                "TeachingPoint.IdRequired",
                "Teaching point id must not be empty."));
        }

        if (issues.Count > 0)
        {
            return TeachingPointCreateResult.Failure(issues);
        }

        var now = timestamp ?? DateTimeOffset.UtcNow;
        var point = new TeachingPoint(
            pointId,
            normalizedName,
            role,
            position,
            tolerance,
            NormalizeOptionalText(memo),
            now,
            now);

        return TeachingPointCreateResult.Success(point);
    }

    private static string NormalizeName(string name, ICollection<TeachingPointValidationIssue> issues)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
        {
            issues.Add(new TeachingPointValidationIssue(
                "TeachingPoint.NameRequired",
                "Teaching point name must not be empty."));
        }

        return normalizedName;
    }

    private static void ValidateRole(TeachingRole role, ICollection<TeachingPointValidationIssue> issues)
    {
        if (!Enum.IsDefined(role))
        {
            issues.Add(new TeachingPointValidationIssue(
                "TeachingPoint.RoleUnsupported",
                $"Teaching point role '{role}' is not supported."));
        }
    }

    private static void ValidatePosition(Position4D position, ICollection<TeachingPointValidationIssue> issues)
    {
        ValidateAxisPosition(AxisId.X, position.X, issues);
        ValidateAxisPosition(AxisId.Y, position.Y, issues);
        ValidateAxisPosition(AxisId.Z, position.Z, issues);
        ValidateAxisPosition(AxisId.Theta, position.Theta, issues);
    }

    private static void ValidateAxisPosition(AxisId axisId, double value, ICollection<TeachingPointValidationIssue> issues)
    {
        if (!double.IsFinite(value))
        {
            issues.Add(new TeachingPointValidationIssue(
                "TeachingPoint.PositionNotFinite",
                $"{axisId} position must be a finite number."));
            return;
        }

        var softLimit = DefaultSoftLimits[axisId];
        if (!softLimit.Contains(value))
        {
            issues.Add(new TeachingPointValidationIssue(
                "TeachingPoint.PositionOutOfSoftLimit",
                $"{axisId} position {value:0.###} is outside {softLimit.Minimum:0.###}..{softLimit.Maximum:0.###} {softLimit.Unit}."));
        }
    }

    private static void ValidateTolerance(PositionTolerance tolerance, ICollection<TeachingPointValidationIssue> issues)
    {
        ValidateToleranceValue(AxisId.X, tolerance.X, issues);
        ValidateToleranceValue(AxisId.Y, tolerance.Y, issues);
        ValidateToleranceValue(AxisId.Z, tolerance.Z, issues);
        ValidateToleranceValue(AxisId.Theta, tolerance.Theta, issues);
    }

    private static void ValidateToleranceValue(AxisId axisId, double value, ICollection<TeachingPointValidationIssue> issues)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            issues.Add(new TeachingPointValidationIssue(
                "TeachingPoint.ToleranceInvalid",
                $"{axisId} tolerance must be a finite number greater than zero."));
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
