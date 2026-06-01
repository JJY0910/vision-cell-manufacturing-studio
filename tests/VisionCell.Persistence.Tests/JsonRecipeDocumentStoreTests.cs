using FluentAssertions;
using VisionCell.Application.Recipes;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;
using VisionCell.Persistence.Recipes;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class JsonRecipeDocumentStoreTests
{
    [Fact]
    public async Task SaveAsync_And_LoadAsync_Should_RoundTrip_Valid_Recipe()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new JsonRecipeDocumentStore(directory.Path);
        var recipe = CreateValidRecipe();

        var save = await store.SaveAsync(recipe, CancellationToken.None);
        var load = await store.LoadAsync(recipe.RecipeId, recipe.Version, CancellationToken.None);

        save.IsSuccess.Should().BeTrue();
        save.DocumentPath.Should().NotBeNull();
        save.DocumentPath.Should().EndWith("PKG-MEMORY-MODULE.v1.0.0.recipe.json");
        File.Exists(save.DocumentPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(save.DocumentPath!, CancellationToken.None);
        json.Should().Contain("\"role\": \"Camera\"");

        load.IsSuccess.Should().BeTrue();
        load.Recipe.Should().BeEquivalentTo(recipe);
        load.DocumentPath.Should().Be(save.DocumentPath);
    }

    [Fact]
    public async Task SaveAsync_Should_Reject_Invalid_Recipe_Without_Writing_File()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new JsonRecipeDocumentStore(directory.Path);
        var recipe = CreateValidRecipe() with
        {
            RecipeId = "",
            Version = "1.0"
        };

        var save = await store.SaveAsync(recipe, CancellationToken.None);

        save.IsSuccess.Should().BeFalse();
        save.Status.Should().Be(RecipeDocumentOperationStatus.ValidationFailed);
        save.ValidationIssues.Select(issue => issue.Code).Should().Contain("Recipe.IdRequired");
        Directory.EnumerateFiles(directory.Path).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_Should_Reject_Path_Traversal_File_Name()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new JsonRecipeDocumentStore(directory.Path);
        var recipe = CreateValidRecipe() with
        {
            RecipeId = ".._escape"
        };

        var save = await store.SaveAsync(recipe, CancellationToken.None);
        var load = await store.LoadAsync("../escape", "1.0.0", CancellationToken.None);

        save.IsSuccess.Should().BeFalse();
        save.Status.Should().Be(RecipeDocumentOperationStatus.InvalidFileName);
        load.IsSuccess.Should().BeFalse();
        load.Status.Should().Be(RecipeDocumentOperationStatus.InvalidFileName);
        Directory.EnumerateFiles(directory.Path).Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_Should_Return_NotFound_When_Document_Is_Missing()
    {
        using var directory = TemporaryDirectory.Create();
        var store = new JsonRecipeDocumentStore(directory.Path);

        var load = await store.LoadAsync("PKG-MEMORY-MODULE", "1.0.0", CancellationToken.None);

        load.IsSuccess.Should().BeFalse();
        load.Status.Should().Be(RecipeDocumentOperationStatus.NotFound);
        load.DocumentPath.Should().EndWith("PKG-MEMORY-MODULE.v1.0.0.recipe.json");
    }

    [Fact]
    public async Task LoadAsync_Should_Return_InvalidDocument_For_Malformed_Json()
    {
        using var directory = TemporaryDirectory.Create();
        var path = System.IO.Path.Combine(directory.Path, "PKG-MEMORY-MODULE.v1.0.0.recipe.json");
        await File.WriteAllTextAsync(path, "{not valid json", CancellationToken.None);
        var store = new JsonRecipeDocumentStore(directory.Path);

        var load = await store.LoadAsync("PKG-MEMORY-MODULE", "1.0.0", CancellationToken.None);

        load.IsSuccess.Should().BeFalse();
        load.Status.Should().Be(RecipeDocumentOperationStatus.InvalidDocument);
    }

    private static RecipeDefinition CreateValidRecipe()
    {
        return new RecipeDefinition(
            "PKG-MEMORY-MODULE",
            "Memory Module Sample",
            "1.0.0",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new RecipeMotionSection(new[]
            {
                new RecipeTeachingPoint(
                    "CAMERA_POS_01",
                    "Camera Position 01",
                    TeachingRole.Camera,
                    new Position4D(10.0, 25.0, 8.0, 0.0),
                    new PositionTolerance(0.05, 0.05, 0.02, 0.1))
            }),
            new RecipeCameraSettings(5.0, 1.0, 80),
            new RecipeVisionSection(
                new[] { new RecipeRoi("IC_TOP", "IC Top", 120, 80, 300, 200) },
                new RecipeVisionParameters(0.75, 8, 0.65, 1.0, 0.15, 0.15)),
            new RecipeSequence(new[] { "SafetyCheck", "MoveToCamera", "Grab", "Inspect2D", "Inspect3D", "Judge", "Persist" }));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            return new TemporaryDirectory(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "VisionCellRecipeStoreTests",
                Guid.NewGuid().ToString("N")));
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
