using System.Diagnostics;

namespace VisionCell.Vision.Inspection;

public sealed class DeterministicHeightMapInspectionEngine : IHeightMapInspectionEngine
{
    public Task<VisionInspectionResult> InspectAsync(
        HeightMapInspectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);

        try
        {
            var result = InspectCore(request, sw, timeout.Token);
            return Task.FromResult(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return Task.FromResult(CreateInvalidResult(
                request,
                sw.Elapsed,
                $"3D inspection timed out after {request.Timeout.TotalMilliseconds:0} ms.",
                new Defect("Timeout", 1.0, 0, 0, request.HeightMap.Width, request.HeightMap.Height, "3D height-map inspection timeout.")));
        }
    }

    private static VisionInspectionResult InspectCore(
        HeightMapInspectionRequest request,
        Stopwatch sw,
        CancellationToken cancellationToken)
    {
        var parameterIssue = ValidateParameters(request.Parameters);
        if (parameterIssue is not null)
        {
            return CreateInvalidResult(
                request,
                sw.Elapsed,
                parameterIssue,
                new Defect("InvalidParameters", 1.0, 0, 0, request.HeightMap.Width, request.HeightMap.Height, parameterIssue));
        }

        if (request.HeightMap.Values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
        {
            return CreateInvalidResult(
                request,
                sw.Elapsed,
                "Height map values must be finite.",
                new Defect("InvalidHeightMap", 1.0, 0, 0, request.HeightMap.Width, request.HeightMap.Height, "Height map contains non-finite values."));
        }

        var defects = new List<Defect>();
        foreach (var roi in request.Rois)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsRoiInsideMap(roi, request.HeightMap))
            {
                defects.Add(new Defect(
                    "InvalidRoi",
                    1.0,
                    Math.Max(roi.X, 0),
                    Math.Max(roi.Y, 0),
                    Math.Max(roi.Width, 0),
                    Math.Max(roi.Height, 0),
                    $"ROI '{roi.Id}' is outside the {request.HeightMap.Width}x{request.HeightMap.Height} height map."));
                continue;
            }

            defects.AddRange(InspectRoi(request.HeightMap, roi, request.Parameters, cancellationToken));
        }

        sw.Stop();
        if (defects.Any(defect => defect.Type == "InvalidRoi"))
        {
            return CreateInvalidResult(
                request,
                sw.Elapsed,
                $"3D inspection invalid: {defects.Count(defect => defect.Type == "InvalidRoi")} ROI boundary issue(s).",
                defects);
        }

        var judgment = defects.Count == 0 ? Judgment.Pass : Judgment.Fail;
        var message = judgment == Judgment.Pass
            ? $"3D inspection Pass: {request.Rois.Count} ROI(s) evaluated."
            : $"3D inspection Fail: {defects.Count} defect(s) detected.";

        return new VisionInspectionResult(
            judgment,
            defects,
            message,
            request.RecipeId,
            request.RecipeVersion,
            sw.Elapsed,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<Defect> InspectRoi(
        VisionHeightMap heightMap,
        VisionRoi roi,
        HeightMapInspectionParameters parameters,
        CancellationToken cancellationToken)
    {
        var maxHeight = double.MinValue;
        var minHeight = double.MaxValue;
        var maxHeightX = roi.X;
        var maxHeightY = roi.Y;
        var minHeightX = roi.X;
        var minHeightY = roi.Y;
        var maxGradient = 0.0;
        var gradientX = roi.X;
        var gradientY = roi.Y;

        for (var localY = 0; localY < roi.Height; localY++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapY = roi.Y + localY;
            for (var localX = 0; localX < roi.Width; localX++)
            {
                var mapX = roi.X + localX;
                var value = heightMap.Values[(mapY * heightMap.Width) + mapX];
                if (value > maxHeight)
                {
                    maxHeight = value;
                    maxHeightX = mapX;
                    maxHeightY = mapY;
                }

                if (value < minHeight)
                {
                    minHeight = value;
                    minHeightX = mapX;
                    minHeightY = mapY;
                }

                if (localX + 1 < roi.Width)
                {
                    var next = heightMap.Values[(mapY * heightMap.Width) + mapX + 1];
                    var gradient = Math.Abs(next - value);
                    if (gradient > maxGradient)
                    {
                        maxGradient = gradient;
                        gradientX = mapX;
                        gradientY = mapY;
                    }
                }

                if (localY + 1 < roi.Height)
                {
                    var next = heightMap.Values[((mapY + 1) * heightMap.Width) + mapX];
                    var gradient = Math.Abs(next - value);
                    if (gradient > maxGradient)
                    {
                        maxGradient = gradient;
                        gradientX = mapX;
                        gradientY = mapY;
                    }
                }
            }
        }

        var defects = new List<Defect>();
        var lift = maxHeight - parameters.ExpectedHeight;
        if (lift > parameters.HeightToleranceHigh)
        {
            defects.Add(new Defect(
                "Lift",
                lift,
                maxHeightX,
                maxHeightY,
                1,
                1,
                $"ROI '{roi.Id}' max height {maxHeight:0.000} {heightMap.Unit} exceeded upper tolerance by {lift - parameters.HeightToleranceHigh:0.000} {heightMap.Unit}."));
        }

        var dent = parameters.ExpectedHeight - minHeight;
        if (dent > parameters.HeightToleranceLow)
        {
            defects.Add(new Defect(
                "Dent",
                dent,
                minHeightX,
                minHeightY,
                1,
                1,
                $"ROI '{roi.Id}' min height {minHeight:0.000} {heightMap.Unit} exceeded lower tolerance by {dent - parameters.HeightToleranceLow:0.000} {heightMap.Unit}."));
        }

        if (maxGradient > parameters.LeadBentGradientTolerance)
        {
            defects.Add(new Defect(
                "LeadBent",
                maxGradient,
                gradientX,
                gradientY,
                2,
                2,
                $"ROI '{roi.Id}' local height gradient {maxGradient:0.000} {heightMap.Unit} exceeded lead-bent tolerance {parameters.LeadBentGradientTolerance:0.000} {heightMap.Unit}."));
        }

        return defects;
    }

    private static string? ValidateParameters(HeightMapInspectionParameters parameters)
    {
        if (!double.IsFinite(parameters.ExpectedHeight) || parameters.ExpectedHeight <= 0.0)
        {
            return "Expected height must be a finite positive value.";
        }

        if (!double.IsFinite(parameters.HeightToleranceLow) || parameters.HeightToleranceLow < 0.0)
        {
            return "Lower height tolerance must be a finite value greater than or equal to zero.";
        }

        if (!double.IsFinite(parameters.HeightToleranceHigh) || parameters.HeightToleranceHigh < 0.0)
        {
            return "Upper height tolerance must be a finite value greater than or equal to zero.";
        }

        if (!double.IsFinite(parameters.LeadBentGradientTolerance) || parameters.LeadBentGradientTolerance <= 0.0)
        {
            return "Lead-bent gradient tolerance must be a finite positive value.";
        }

        return null;
    }

    private static bool IsRoiInsideMap(VisionRoi roi, VisionHeightMap heightMap)
    {
        return !string.IsNullOrWhiteSpace(roi.Id) &&
               roi.X >= 0 &&
               roi.Y >= 0 &&
               roi.Width > 0 &&
               roi.Height > 0 &&
               roi.X + roi.Width <= heightMap.Width &&
               roi.Y + roi.Height <= heightMap.Height;
    }

    private static VisionInspectionResult CreateInvalidResult(
        HeightMapInspectionRequest request,
        TimeSpan elapsed,
        string message,
        Defect defect)
    {
        return CreateInvalidResult(request, elapsed, message, new[] { defect });
    }

    private static VisionInspectionResult CreateInvalidResult(
        HeightMapInspectionRequest request,
        TimeSpan elapsed,
        string message,
        IReadOnlyList<Defect> defects)
    {
        return new VisionInspectionResult(
            Judgment.Invalid,
            defects,
            message,
            request.RecipeId,
            request.RecipeVersion,
            elapsed,
            DateTimeOffset.UtcNow);
    }
}
