using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Recipes;

public sealed record RecipeDefinition(
    string RecipeId,
    string ProductName,
    string Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    RecipeMotionSection Motion,
    RecipeCameraSettings Camera,
    RecipeVisionSection Vision,
    RecipeSequence Sequence);

public sealed record RecipeMotionSection(IReadOnlyList<RecipeTeachingPoint> TeachingPoints);

public sealed record RecipeTeachingPoint(
    string Id,
    string Name,
    TeachingRole Role,
    Position4D Position,
    PositionTolerance Tolerance);

public sealed record RecipeCameraSettings(
    double ExposureMs,
    double Gain,
    int LightIntensity);

public sealed record RecipeVisionSection(
    IReadOnlyList<RecipeRoi> Rois,
    RecipeVisionParameters Parameters);

public sealed record RecipeRoi(
    string Id,
    string Name,
    int X,
    int Y,
    int Width,
    int Height);

public sealed record RecipeVisionParameters(
    double MissingAreaThreshold,
    int OffsetTolerancePx,
    double ScratchThreshold,
    double ExpectedHeight,
    double HeightToleranceLow,
    double HeightToleranceHigh);

public sealed record RecipeSequence(IReadOnlyList<string> Steps);
