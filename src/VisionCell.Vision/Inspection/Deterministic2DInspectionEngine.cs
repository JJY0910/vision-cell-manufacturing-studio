using System.Diagnostics;

namespace VisionCell.Vision.Inspection;

public sealed class Deterministic2DInspectionEngine : IVisionInspectionEngine
{
    private const byte MissingDarkThreshold = 32;
    private const byte MinimumForegroundThreshold = 96;

    public Task<VisionInspectionResult> InspectAsync(
        VisionInspectionRequest request,
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
                $"2D inspection timed out after {request.Timeout.TotalMilliseconds:0} ms.",
                new Defect("Timeout", 1.0, 0, 0, request.Image.Width, request.Image.Height, "2D inspection timeout.")));
        }
    }

    private static VisionInspectionResult InspectCore(
        VisionInspectionRequest request,
        Stopwatch sw,
        CancellationToken cancellationToken)
    {
        if (request.Image.PixelFormat != VisionPixelFormat.Gray8)
        {
            return CreateInvalidResult(
                request,
                sw.Elapsed,
                $"Unsupported image pixel format: {request.Image.PixelFormat}.",
                new Defect("UnsupportedPixelFormat", 1.0, 0, 0, request.Image.Width, request.Image.Height, "Only Gray8 inspection frames are supported."));
        }

        var parameterIssue = ValidateParameters(request.Parameters);
        if (parameterIssue is not null)
        {
            return CreateInvalidResult(
                request,
                sw.Elapsed,
                parameterIssue,
                new Defect("InvalidParameters", 1.0, 0, 0, request.Image.Width, request.Image.Height, parameterIssue));
        }

        var defects = new List<Defect>();
        foreach (var roi in request.Rois)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsRoiInsideFrame(roi, request.Image))
            {
                defects.Add(new Defect(
                    "InvalidRoi",
                    1.0,
                    Math.Max(roi.X, 0),
                    Math.Max(roi.Y, 0),
                    Math.Max(roi.Width, 0),
                    Math.Max(roi.Height, 0),
                    $"ROI '{roi.Id}' is outside the {request.Image.Width}x{request.Image.Height} frame."));
                continue;
            }

            defects.AddRange(InspectRoi(request.Image, roi, request.Parameters, cancellationToken));
        }

        sw.Stop();
        if (defects.Any(defect => defect.Type == "InvalidRoi"))
        {
            return CreateInvalidResult(
                request,
                sw.Elapsed,
                $"2D inspection invalid: {defects.Count(defect => defect.Type == "InvalidRoi")} ROI boundary issue(s).",
                defects);
        }

        var judgment = defects.Count == 0 ? Judgment.Pass : Judgment.Fail;
        var message = judgment == Judgment.Pass
            ? $"2D inspection Pass: {request.Rois.Count} ROI(s) evaluated."
            : $"2D inspection Fail: {defects.Count} defect(s) detected.";

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
        VisionImageFrame image,
        VisionRoi roi,
        VisionInspectionParameters parameters,
        CancellationToken cancellationToken)
    {
        var width = roi.Width;
        var height = roi.Height;
        var total = width * height;
        var rowDarkCounts = new int[height];
        var columnDarkCounts = new int[width];
        var darkPixels = 0;
        var sum = 0L;
        var min = byte.MaxValue;
        var max = byte.MinValue;

        for (var localY = 0; localY < height; localY++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowOffset = ((roi.Y + localY) * image.Stride) + roi.X;
            for (var localX = 0; localX < width; localX++)
            {
                var value = image.Pixels[rowOffset + localX];
                sum += value;
                min = value < min ? value : min;
                max = value > max ? value : max;

                if (value <= MissingDarkThreshold)
                {
                    darkPixels++;
                    rowDarkCounts[localY]++;
                    columnDarkCounts[localX]++;
                }
            }
        }

        var defects = new List<Defect>();
        var missingLimit = ClampRatio(parameters.MissingAreaThreshold);
        var darkRatio = darkPixels / (double)total;
        if (darkRatio >= missingLimit)
        {
            defects.Add(new Defect(
                "Missing",
                darkRatio,
                roi.X,
                roi.Y,
                roi.Width,
                roi.Height,
                $"ROI '{roi.Id}' dark area ratio {darkRatio:0.000} exceeded missing limit {missingLimit:0.000}."));
            return defects;
        }

        var scratchLimit = ClampRatio(parameters.ScratchThreshold);
        var maxRowDarkRatio = rowDarkCounts.Max() / (double)width;
        var maxColumnDarkRatio = columnDarkCounts.Max() / (double)height;
        if (Math.Max(maxRowDarkRatio, maxColumnDarkRatio) >= scratchLimit)
        {
            defects.Add(CreateScratchDefect(roi, rowDarkCounts, columnDarkCounts, maxRowDarkRatio, maxColumnDarkRatio));
        }

        var foreground = CalculateForeground(image, roi, sum / (double)total, min, max, cancellationToken);
        if (foreground is not null && foreground.OffsetPixels > parameters.OffsetTolerancePx)
        {
            defects.Add(new Defect(
                "Offset",
                foreground.OffsetPixels,
                foreground.X,
                foreground.Y,
                foreground.Width,
                foreground.Height,
                $"ROI '{roi.Id}' foreground centroid offset {foreground.OffsetPixels:0.000} px exceeded tolerance {parameters.OffsetTolerancePx} px."));
        }

        return defects;
    }

    private static Defect CreateScratchDefect(
        VisionRoi roi,
        int[] rowDarkCounts,
        int[] columnDarkCounts,
        double maxRowDarkRatio,
        double maxColumnDarkRatio)
    {
        if (maxRowDarkRatio >= maxColumnDarkRatio)
        {
            var row = Array.IndexOf(rowDarkCounts, rowDarkCounts.Max());
            return new Defect(
                "Scratch",
                maxRowDarkRatio,
                roi.X,
                roi.Y + row,
                roi.Width,
                1,
                $"ROI '{roi.Id}' horizontal dark-line score {maxRowDarkRatio:0.000} exceeded scratch limit.");
        }

        var column = Array.IndexOf(columnDarkCounts, columnDarkCounts.Max());
        return new Defect(
            "Scratch",
            maxColumnDarkRatio,
            roi.X + column,
            roi.Y,
            1,
            roi.Height,
            $"ROI '{roi.Id}' vertical dark-line score {maxColumnDarkRatio:0.000} exceeded scratch limit.");
    }

    private static ForegroundMetrics? CalculateForeground(
        VisionImageFrame image,
        VisionRoi roi,
        double mean,
        byte min,
        byte max,
        CancellationToken cancellationToken)
    {
        if (max <= min)
        {
            return null;
        }

        var threshold = Math.Max(MinimumForegroundThreshold, mean + ((max - mean) * 0.25));
        var count = 0;
        var sumX = 0.0;
        var sumY = 0.0;
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        for (var localY = 0; localY < roi.Height; localY++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowOffset = ((roi.Y + localY) * image.Stride) + roi.X;
            for (var localX = 0; localX < roi.Width; localX++)
            {
                var value = image.Pixels[rowOffset + localX];
                if (value < threshold)
                {
                    continue;
                }

                count++;
                sumX += localX;
                sumY += localY;
                minX = Math.Min(minX, localX);
                minY = Math.Min(minY, localY);
                maxX = Math.Max(maxX, localX);
                maxY = Math.Max(maxY, localY);
            }
        }

        if (count == 0)
        {
            return null;
        }

        var foregroundRatio = count / (double)(roi.Width * roi.Height);
        var touchesRoiEdge = minX == 0 || minY == 0 || maxX == roi.Width - 1 || maxY == roi.Height - 1;
        if (foregroundRatio is < 0.02 or > 0.65 || touchesRoiEdge)
        {
            return null;
        }

        var centroidX = sumX / count;
        var centroidY = sumY / count;
        var centerX = (roi.Width - 1) / 2.0;
        var centerY = (roi.Height - 1) / 2.0;
        var offset = Math.Sqrt(Math.Pow(centroidX - centerX, 2.0) + Math.Pow(centroidY - centerY, 2.0));

        return new ForegroundMetrics(
            roi.X + minX,
            roi.Y + minY,
            maxX - minX + 1,
            maxY - minY + 1,
            offset);
    }

    private static string? ValidateParameters(VisionInspectionParameters parameters)
    {
        if (!IsFiniteRatio(parameters.MissingAreaThreshold))
        {
            return "Missing area threshold must be a finite value between 0 and 1.";
        }

        if (parameters.OffsetTolerancePx < 0)
        {
            return "Offset tolerance must be greater than or equal to zero.";
        }

        if (!IsFiniteRatio(parameters.ScratchThreshold))
        {
            return "Scratch threshold must be a finite value between 0 and 1.";
        }

        return null;
    }

    private static bool IsRoiInsideFrame(VisionRoi roi, VisionImageFrame image)
    {
        return !string.IsNullOrWhiteSpace(roi.Id) &&
               roi.X >= 0 &&
               roi.Y >= 0 &&
               roi.Width > 0 &&
               roi.Height > 0 &&
               roi.X + roi.Width <= image.Width &&
               roi.Y + roi.Height <= image.Height;
    }

    private static bool IsFiniteRatio(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value is >= 0.0 and <= 1.0;
    }

    private static double ClampRatio(double value)
    {
        return Math.Clamp(value, 0.01, 0.99);
    }

    private static VisionInspectionResult CreateInvalidResult(
        VisionInspectionRequest request,
        TimeSpan elapsed,
        string message,
        Defect defect)
    {
        return CreateInvalidResult(request, elapsed, message, new[] { defect });
    }

    private static VisionInspectionResult CreateInvalidResult(
        VisionInspectionRequest request,
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

    private sealed record ForegroundMetrics(
        int X,
        int Y,
        int Width,
        int Height,
        double OffsetPixels);
}
