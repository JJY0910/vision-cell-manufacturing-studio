using System.Globalization;
using VisionCell.Core.Primitives;

namespace VisionCell.Motion.Commands;

public static class MotionCommandParameterParser
{
    public static bool TryReadJogTarget(
        IReadOnlyDictionary<string, string> parameters,
        JogMotionTarget fallback,
        out JogMotionTarget target,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(fallback);

        target = fallback;
        error = string.Empty;

        if (!TryReadAxis(parameters, fallback.AxisId, out var axisId, out error))
        {
            return false;
        }

        if (!TryReadDirection(parameters, fallback.Direction, out var direction, out error))
        {
            return false;
        }

        if (!TryReadJogStep(parameters, fallback.Step, out var step, out error))
        {
            return false;
        }

        if (!double.IsFinite(step) || step <= 0.0)
        {
            error = "Jog step must be greater than zero.";
            return false;
        }

        target = new JogMotionTarget(axisId, direction, step);
        return true;
    }

    public static bool TryReadAbsoluteMoveTarget(
        IReadOnlyDictionary<string, string> parameters,
        AbsoluteMoveTarget fallback,
        out AbsoluteMoveTarget target,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(fallback);

        target = fallback;
        error = string.Empty;

        if (!TryReadOptionalDouble(parameters, MotionCommandParameterKeys.X, fallback.X, out var x, out error) ||
            !TryReadOptionalDouble(parameters, MotionCommandParameterKeys.Y, fallback.Y, out var y, out error) ||
            !TryReadOptionalDouble(parameters, MotionCommandParameterKeys.Z, fallback.Z, out var z, out error) ||
            !TryReadOptionalDouble(parameters, MotionCommandParameterKeys.Theta, fallback.Theta, out var theta, out error) ||
            !TryReadOptionalDouble(parameters, MotionCommandParameterKeys.Velocity, fallback.Velocity, out var velocity, out error) ||
            !TryReadOptionalDouble(parameters, MotionCommandParameterKeys.Acceleration, fallback.Acceleration, out var acceleration, out error) ||
            !TryReadOptionalDouble(parameters, MotionCommandParameterKeys.Deceleration, fallback.Deceleration, out var deceleration, out error) ||
            !TryReadOptionalDouble(parameters, MotionCommandParameterKeys.Jerk, fallback.Jerk, out var jerk, out error) ||
            !TryReadOptionalDouble(parameters, MotionCommandParameterKeys.ArrivalTolerance, fallback.ArrivalTolerance, out var arrivalTolerance, out error))
        {
            return false;
        }

        if (!IsGreaterThanZero(velocity, MotionCommandParameterKeys.Velocity, out error) ||
            !IsGreaterThanZero(acceleration, MotionCommandParameterKeys.Acceleration, out error) ||
            !IsGreaterThanZero(deceleration, MotionCommandParameterKeys.Deceleration, out error) ||
            !IsGreaterThanZero(jerk, MotionCommandParameterKeys.Jerk, out error) ||
            !IsGreaterThanZero(arrivalTolerance, MotionCommandParameterKeys.ArrivalTolerance, out error))
        {
            return false;
        }

        target = new AbsoluteMoveTarget(x, y, z, theta, velocity, acceleration, deceleration, jerk, arrivalTolerance);
        return true;
    }

    private static bool TryReadAxis(
        IReadOnlyDictionary<string, string> parameters,
        AxisId fallback,
        out AxisId axisId,
        out string error)
    {
        axisId = fallback;
        error = string.Empty;

        if (!TryGet(parameters, MotionCommandParameterKeys.Axis, out var raw))
        {
            return true;
        }

        if (string.Equals(raw, "T", StringComparison.OrdinalIgnoreCase))
        {
            axisId = AxisId.Theta;
            return true;
        }

        if (Enum.TryParse<AxisId>(raw, ignoreCase: true, out axisId))
        {
            return true;
        }

        error = $"Unsupported axis '{raw}'.";
        return false;
    }

    private static bool TryReadDirection(
        IReadOnlyDictionary<string, string> parameters,
        MotionDirection fallback,
        out MotionDirection direction,
        out string error)
    {
        direction = fallback;
        error = string.Empty;

        if (!TryGet(parameters, MotionCommandParameterKeys.Direction, out var raw))
        {
            return true;
        }

        if (string.Equals(raw, "+", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "Plus", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "Positive", StringComparison.OrdinalIgnoreCase))
        {
            direction = MotionDirection.Positive;
            return true;
        }

        if (string.Equals(raw, "-", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "Minus", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "Negative", StringComparison.OrdinalIgnoreCase))
        {
            direction = MotionDirection.Negative;
            return true;
        }

        error = $"Unsupported jog direction '{raw}'.";
        return false;
    }

    private static bool TryReadJogStep(
        IReadOnlyDictionary<string, string> parameters,
        double fallback,
        out double value,
        out string error)
    {
        value = fallback;
        error = string.Empty;

        foreach (var key in new[] { MotionCommandParameterKeys.Step, MotionCommandParameterKeys.StepMm, MotionCommandParameterKeys.StepDeg })
        {
            if (TryGet(parameters, key, out _))
            {
                return TryReadOptionalDouble(parameters, key, fallback, out value, out error);
            }
        }

        return true;
    }

    private static bool TryReadOptionalDouble(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        double fallback,
        out double value,
        out string error)
    {
        value = fallback;
        error = string.Empty;

        if (!TryGet(parameters, key, out var raw))
        {
            return true;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value))
        {
            error = $"{key} must be a finite number.";
            return false;
        }

        return true;
    }

    private static bool IsGreaterThanZero(double value, string key, out string error)
    {
        error = string.Empty;

        if (value > 0.0)
        {
            return true;
        }

        error = $"{key} must be greater than zero.";
        return false;
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> parameters, string key, out string value)
    {
        if (parameters.TryGetValue(key, out value!))
        {
            return true;
        }

        var item = parameters.FirstOrDefault(
            pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
        value = item.Value;
        return item.Key is not null;
    }
}
