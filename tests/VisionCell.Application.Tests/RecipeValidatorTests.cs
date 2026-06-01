using FluentAssertions;
using VisionCell.Application.Recipes;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class RecipeValidatorTests
{
    private readonly RecipeValidator _validator = new();

    [Fact]
    public void Validate_Should_Accept_Valid_Recipe()
    {
        var recipe = CreateValidRecipe();

        var result = _validator.Validate(recipe);

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_Should_Reject_Missing_Metadata_And_Invalid_Version()
    {
        var recipe = CreateValidRecipe() with
        {
            RecipeId = " ",
            ProductName = "",
            Version = "1.0",
            UpdatedAt = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)
        };

        var result = _validator.Validate(recipe);

        result.IsValid.Should().BeFalse();
        result.Issues.Select(issue => issue.Code).Should().Contain(new[]
        {
            "Recipe.IdRequired",
            "Recipe.ProductNameRequired",
            "Recipe.VersionInvalid",
            "Recipe.UpdatedBeforeCreated"
        });
    }

    [Fact]
    public void Validate_Should_Reject_Invalid_Teaching_Points()
    {
        var recipe = CreateValidRecipe() with
        {
            Motion = new RecipeMotionSection(new[]
            {
                CreateTeachingPoint("CAMERA_POS_01"),
                CreateTeachingPoint("camera_pos_01") with
                {
                    Name = "",
                    Position = new Position4D(9999.0, 0.0, 0.0, 0.0),
                    Tolerance = new PositionTolerance(0.0, 0.01, 0.01, 0.01)
                }
            })
        };

        var result = _validator.Validate(recipe);

        result.IsValid.Should().BeFalse();
        result.Issues.Select(issue => issue.Code).Should().Contain("Recipe.TeachingIdDuplicate");
        result.Issues.Select(issue => issue.Code).Should().Contain("TeachingPoint.NameRequired");
        result.Issues.Select(issue => issue.Code).Should().Contain("TeachingPoint.PositionOutOfSoftLimit");
        result.Issues.Select(issue => issue.Code).Should().Contain("TeachingPoint.ToleranceInvalid");
    }

    [Fact]
    public void Validate_Should_Reject_Roi_Outside_Image_Bounds()
    {
        var recipe = CreateValidRecipe() with
        {
            Vision = CreateValidRecipe().Vision with
            {
                Rois = new[]
                {
                    new RecipeRoi("IC_TOP", "IC Top", 1800, 900, 300, 200)
                }
            }
        };

        var result = _validator.Validate(recipe);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "Recipe.RoiOutsideImage");
    }

    [Fact]
    public void Validate_Should_Reject_Invalid_Camera_And_Vision_Parameters()
    {
        var recipe = CreateValidRecipe() with
        {
            Camera = new RecipeCameraSettings(0.0, -1.0, 101),
            Vision = CreateValidRecipe().Vision with
            {
                Parameters = new RecipeVisionParameters(
                    1.2,
                    -1,
                    -0.1,
                    0.0,
                    -0.01,
                    0.1)
            }
        };

        var result = _validator.Validate(recipe);

        result.IsValid.Should().BeFalse();
        result.Issues.Select(issue => issue.Code).Should().Contain(new[]
        {
            "Recipe.CameraExposureInvalid",
            "Recipe.CameraGainInvalid",
            "Recipe.LightIntensityInvalid",
            "Recipe.MissingAreaThresholdInvalid",
            "Recipe.OffsetToleranceInvalid",
            "Recipe.ScratchThresholdInvalid",
            "Recipe.ExpectedHeightInvalid",
            "Recipe.HeightToleranceInvalid"
        });
    }

    [Fact]
    public void Validate_Should_Reject_Missing_Required_Sequence_Steps()
    {
        var recipe = CreateValidRecipe() with
        {
            Sequence = new RecipeSequence(new[] { "SafetyCheck", "Grab", "Judge" })
        };

        var result = _validator.Validate(recipe);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "Recipe.SequenceStepMissing" && issue.Message.Contains("MoveToCamera"));
        result.Issues.Should().Contain(issue => issue.Code == "Recipe.SequenceStepMissing" && issue.Message.Contains("Persist"));
    }

    private static RecipeDefinition CreateValidRecipe()
    {
        return new RecipeDefinition(
            "PKG-MEMORY-MODULE",
            "Memory Module Sample",
            "1.0.0",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new RecipeMotionSection(new[] { CreateTeachingPoint("CAMERA_POS_01") }),
            new RecipeCameraSettings(5.0, 1.0, 80),
            new RecipeVisionSection(
                new[] { new RecipeRoi("IC_TOP", "IC Top", 120, 80, 300, 200) },
                new RecipeVisionParameters(0.75, 8, 0.65, 1.0, 0.15, 0.15)),
            new RecipeSequence(new[] { "SafetyCheck", "MoveToCamera", "Grab", "Inspect2D", "Inspect3D", "Judge", "Persist" }));
    }

    private static RecipeTeachingPoint CreateTeachingPoint(string id)
    {
        return new RecipeTeachingPoint(
            id,
            "Camera Position 01",
            TeachingRole.Camera,
            new Position4D(10.0, 25.0, 8.0, 0.0),
            new PositionTolerance(0.05, 0.05, 0.02, 0.1));
    }
}
