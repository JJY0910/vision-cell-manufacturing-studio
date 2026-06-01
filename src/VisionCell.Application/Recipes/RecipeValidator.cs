using System.Text.RegularExpressions;
using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Recipes;

public sealed class RecipeValidator
{
    public const int DefaultImageWidth = 1920;
    public const int DefaultImageHeight = 1080;

    private static readonly Regex SemanticVersionPattern = new(
        @"^\d+\.\d+\.\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] RequiredSequenceSteps =
    {
        "SafetyCheck",
        "MoveToCamera",
        "Grab",
        "Inspect2D",
        "Inspect3D",
        "Judge",
        "Persist"
    };

    public RecipeValidationResult Validate(RecipeDefinition? recipe)
    {
        var issues = new List<RecipeValidationIssue>();
        if (recipe is null)
        {
            issues.Add(new RecipeValidationIssue("Recipe.Required", "Recipe definition is required."));
            return new RecipeValidationResult(issues);
        }

        ValidateMetadata(recipe, issues);
        ValidateTeaching(recipe.Motion, issues);
        ValidateCamera(recipe.Camera, issues);
        ValidateVision(recipe.Vision, issues);
        ValidateSequence(recipe.Sequence, issues);

        return issues.Count == 0 ? RecipeValidationResult.Success : new RecipeValidationResult(issues);
    }

    private static void ValidateMetadata(RecipeDefinition recipe, ICollection<RecipeValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(recipe.RecipeId))
        {
            issues.Add(new RecipeValidationIssue("Recipe.IdRequired", "Recipe id is required."));
        }

        if (string.IsNullOrWhiteSpace(recipe.ProductName))
        {
            issues.Add(new RecipeValidationIssue("Recipe.ProductNameRequired", "Product name is required."));
        }

        if (string.IsNullOrWhiteSpace(recipe.Version) || !SemanticVersionPattern.IsMatch(recipe.Version.Trim()))
        {
            issues.Add(new RecipeValidationIssue("Recipe.VersionInvalid", "Recipe version must use major.minor.patch format."));
        }

        if (recipe.UpdatedAt < recipe.CreatedAt)
        {
            issues.Add(new RecipeValidationIssue("Recipe.UpdatedBeforeCreated", "Recipe updated time must not be earlier than created time."));
        }
    }

    private static void ValidateTeaching(RecipeMotionSection? motion, ICollection<RecipeValidationIssue> issues)
    {
        if (motion?.TeachingPoints is null || motion.TeachingPoints.Count == 0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.TeachingRequired", "At least one teaching point is required."));
            return;
        }

        var duplicateIds = motion.TeachingPoints
            .Where(point => !string.IsNullOrWhiteSpace(point.Id))
            .GroupBy(point => point.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (var duplicateId in duplicateIds)
        {
            issues.Add(new RecipeValidationIssue("Recipe.TeachingIdDuplicate", $"Teaching point id '{duplicateId}' is duplicated."));
        }

        foreach (var point in motion.TeachingPoints)
        {
            if (string.IsNullOrWhiteSpace(point.Id))
            {
                issues.Add(new RecipeValidationIssue("Recipe.TeachingIdRequired", "Teaching point id is required."));
            }

            var creation = TeachingPointFactory.Create(point.Name, point.Role, point.Position, point.Tolerance);
            foreach (var issue in creation.Issues)
            {
                issues.Add(new RecipeValidationIssue(issue.Code, issue.Message));
            }
        }
    }

    private static void ValidateCamera(RecipeCameraSettings? camera, ICollection<RecipeValidationIssue> issues)
    {
        if (camera is null)
        {
            issues.Add(new RecipeValidationIssue("Recipe.CameraRequired", "Camera settings are required."));
            return;
        }

        if (!double.IsFinite(camera.ExposureMs) || camera.ExposureMs <= 0.0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.CameraExposureInvalid", "Camera exposure must be greater than zero."));
        }

        if (!double.IsFinite(camera.Gain) || camera.Gain < 0.0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.CameraGainInvalid", "Camera gain must be zero or greater."));
        }

        if (camera.LightIntensity is < 0 or > 100)
        {
            issues.Add(new RecipeValidationIssue("Recipe.LightIntensityInvalid", "Light intensity must be between 0 and 100."));
        }
    }

    private static void ValidateVision(RecipeVisionSection? vision, ICollection<RecipeValidationIssue> issues)
    {
        if (vision is null)
        {
            issues.Add(new RecipeValidationIssue("Recipe.VisionRequired", "Vision settings are required."));
            return;
        }

        if (vision.Rois is null || vision.Rois.Count == 0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.RoiRequired", "At least one ROI is required."));
        }
        else
        {
            foreach (var roi in vision.Rois)
            {
                ValidateRoi(roi, issues);
            }
        }

        ValidateVisionParameters(vision.Parameters, issues);
    }

    private static void ValidateRoi(RecipeRoi roi, ICollection<RecipeValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(roi.Id))
        {
            issues.Add(new RecipeValidationIssue("Recipe.RoiIdRequired", "ROI id is required."));
        }

        if (string.IsNullOrWhiteSpace(roi.Name))
        {
            issues.Add(new RecipeValidationIssue("Recipe.RoiNameRequired", "ROI name is required."));
        }

        if (roi.X < 0 || roi.Y < 0 || roi.Width <= 0 || roi.Height <= 0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.RoiBoundsInvalid", "ROI coordinates and size must be positive."));
            return;
        }

        if (roi.X + roi.Width > DefaultImageWidth || roi.Y + roi.Height > DefaultImageHeight)
        {
            issues.Add(new RecipeValidationIssue("Recipe.RoiOutsideImage", "ROI must fit inside the default image bounds."));
        }
    }

    private static void ValidateVisionParameters(RecipeVisionParameters? parameters, ICollection<RecipeValidationIssue> issues)
    {
        if (parameters is null)
        {
            issues.Add(new RecipeValidationIssue("Recipe.VisionParametersRequired", "Vision parameters are required."));
            return;
        }

        ValidateUnitRange(parameters.MissingAreaThreshold, "Recipe.MissingAreaThresholdInvalid", "Missing area threshold", issues);
        ValidateUnitRange(parameters.ScratchThreshold, "Recipe.ScratchThresholdInvalid", "Scratch threshold", issues);

        if (parameters.OffsetTolerancePx < 0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.OffsetToleranceInvalid", "Offset tolerance must be zero or greater."));
        }

        if (!double.IsFinite(parameters.ExpectedHeight) || parameters.ExpectedHeight <= 0.0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.ExpectedHeightInvalid", "Expected height must be greater than zero."));
        }

        if (!double.IsFinite(parameters.HeightToleranceLow) || parameters.HeightToleranceLow < 0.0 ||
            !double.IsFinite(parameters.HeightToleranceHigh) || parameters.HeightToleranceHigh < 0.0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.HeightToleranceInvalid", "Height tolerances must be zero or greater."));
        }
    }

    private static void ValidateSequence(RecipeSequence? sequence, ICollection<RecipeValidationIssue> issues)
    {
        if (sequence?.Steps is null || sequence.Steps.Count == 0)
        {
            issues.Add(new RecipeValidationIssue("Recipe.SequenceRequired", "Sequence steps are required."));
            return;
        }

        var stepSet = sequence.Steps
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select(step => step.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var requiredStep in RequiredSequenceSteps)
        {
            if (!stepSet.Contains(requiredStep))
            {
                issues.Add(new RecipeValidationIssue("Recipe.SequenceStepMissing", $"Sequence is missing required step '{requiredStep}'."));
            }
        }
    }

    private static void ValidateUnitRange(
        double value,
        string code,
        string label,
        ICollection<RecipeValidationIssue> issues)
    {
        if (!double.IsFinite(value) || value is < 0.0 or > 1.0)
        {
            issues.Add(new RecipeValidationIssue(code, $"{label} must be between 0 and 1."));
        }
    }
}
