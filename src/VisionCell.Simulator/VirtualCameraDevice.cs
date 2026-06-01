using System.Diagnostics;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Cameras;

namespace VisionCell.Simulator;

public sealed class VirtualCameraDevice : ICameraDevice
{
    private static readonly TimeSpan GrabLatency = TimeSpan.FromMilliseconds(80);
    private readonly Func<DateTimeOffset> _clock;

    public VirtualCameraDevice(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public bool IsReady { get; set; } = true;
    public bool InjectGrabTimeout { get; set; }
    public bool InjectGrabFailure { get; set; }

    public async Task<CameraGrabResult> GrabAsync(CameraGrabRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sw = Stopwatch.StartNew();
        if (!IsReady)
        {
            return CameraGrabResult.NotReady("Virtual camera is not ready.", sw.Elapsed, request.CorrelationId);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(request.Timeout);

        try
        {
            var latency = InjectGrabTimeout
                ? request.Timeout + TimeSpan.FromMilliseconds(50)
                : GrabLatency;
            await Task.Delay(latency, cts.Token).ConfigureAwait(false);

            if (InjectGrabFailure)
            {
                return CameraGrabResult.Failed("Virtual camera grab failure was injected.", sw.Elapsed, request.CorrelationId);
            }

            var frame = CreateSyntheticFrame(request);
            return CameraGrabResult.Success(
                frame,
                $"Grabbed {frame.Width}x{frame.Height} {frame.PixelFormat} frame from {frame.CameraName}.",
                sw.Elapsed,
                request.CorrelationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CameraGrabResult.Cancelled("Virtual camera grab was cancelled.", sw.Elapsed, request.CorrelationId);
        }
        catch (OperationCanceledException)
        {
            return CameraGrabResult.Timeout(
                $"Virtual camera grab timed out after {request.Timeout.TotalMilliseconds:0} ms.",
                sw.Elapsed,
                request.CorrelationId);
        }
    }

    private CameraFrame CreateSyntheticFrame(CameraGrabRequest request)
    {
        const int width = 320;
        const int height = 240;
        const int stride = width;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = 28 + (x * 112 / width) + (y * 54 / height);
                if (x is >= 118 and <= 205 && y is >= 76 and <= 158)
                {
                    value += 58;
                }

                if (x - y / 2 is >= 134 and <= 138 && y is >= 48 and <= 198)
                {
                    value -= 72;
                }

                pixels[(y * stride) + x] = (byte)Math.Clamp(value, 0, 255);
            }
        }

        return new CameraFrame(
            "Virtual 3D camera",
            width,
            height,
            stride,
            CameraPixelFormat.Gray8,
            pixels,
            _clock(),
            new Dictionary<string, string>
            {
                ["RecipeId"] = request.RecipeId,
                ["RecipeVersion"] = request.RecipeVersion,
                ["ExposureMs"] = request.ExposureMilliseconds.ToString("0.###"),
                ["Gain"] = request.Gain.ToString("0.###"),
                ["LightIntensity"] = request.LightIntensity.ToString(),
                ["FrameKind"] = "SyntheticGray8Package"
            });
    }
}
