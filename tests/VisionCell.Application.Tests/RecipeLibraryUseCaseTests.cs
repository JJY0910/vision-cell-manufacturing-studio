using FluentAssertions;
using VisionCell.Application.Recipes;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class RecipeLibraryUseCaseTests
{
    [Fact]
    public async Task SaveAsync_Should_Save_Document_And_Index_Valid_Recipe()
    {
        var recipe = CreateValidRecipe();
        var indexId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var documentStore = new FakeRecipeDocumentStore(
            RecipeDocumentSaveResult.Success(@"local-data\recipes\PKG-MEMORY-MODULE.v1.0.0.recipe.json"));
        var indexRepository = new FakeRecipeIndexRepository();
        var useCase = new RecipeLibraryUseCase(
            documentStore,
            indexRepository,
            idFactory: () => indexId);

        var result = await useCase.SaveAsync(new RecipeLibrarySaveRequest(recipe), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(RecipeLibraryOperationStatus.Success);
        result.Entry.Should().NotBeNull();
        documentStore.SavedRecipes.Should().ContainSingle().Which.Should().Be(recipe);
        indexRepository.SavedEntries.Should().ContainSingle();

        var entry = indexRepository.SavedEntries[0];
        entry.Id.Should().Be(indexId);
        entry.RecipeId.Should().Be("PKG-MEMORY-MODULE");
        entry.Version.Should().Be("1.0.0");
        entry.ProductName.Should().Be("Memory Module Sample");
        entry.DocumentPath.Should().Contain("PKG-MEMORY-MODULE.v1.0.0.recipe.json");
        entry.Checksum.Should().MatchRegex("^[0-9a-f]{64}$");
        entry.IsActive.Should().BeFalse();
        entry.IsValid.Should().BeTrue();
        entry.ValidationSummary.Should().Be("Valid");
        entry.CreatedAt.Should().Be(recipe.CreatedAt);
        entry.UpdatedAt.Should().Be(recipe.UpdatedAt);
    }

    [Fact]
    public async Task SaveAsync_Should_Not_Save_When_Recipe_Is_Invalid()
    {
        var recipe = CreateValidRecipe() with
        {
            Version = "1.0",
            Motion = new RecipeMotionSection(Array.Empty<RecipeTeachingPoint>())
        };
        var documentStore = new FakeRecipeDocumentStore(RecipeDocumentSaveResult.Success("unused.recipe.json"));
        var indexRepository = new FakeRecipeIndexRepository();
        var useCase = new RecipeLibraryUseCase(documentStore, indexRepository);

        var result = await useCase.SaveAsync(new RecipeLibrarySaveRequest(recipe), CancellationToken.None);

        result.Status.Should().Be(RecipeLibraryOperationStatus.ValidationFailed);
        result.IsSuccess.Should().BeFalse();
        result.ValidationIssues.Select(issue => issue.Code).Should().Contain(new[]
        {
            "Recipe.VersionInvalid",
            "Recipe.TeachingRequired"
        });
        documentStore.SavedRecipes.Should().BeEmpty();
        indexRepository.SavedEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_Should_Surface_Document_Store_Failure()
    {
        var documentStore = new FakeRecipeDocumentStore(
            RecipeDocumentSaveResult.InvalidFileName("Recipe id contains unsupported file-name characters."));
        var indexRepository = new FakeRecipeIndexRepository();
        var useCase = new RecipeLibraryUseCase(documentStore, indexRepository);

        var result = await useCase.SaveAsync(new RecipeLibrarySaveRequest(CreateValidRecipe()), CancellationToken.None);

        result.Status.Should().Be(RecipeLibraryOperationStatus.InvalidFileName);
        result.Message.Should().Contain("unsupported");
        result.Entry.Should().BeNull();
        documentStore.SavedRecipes.Should().ContainSingle();
        indexRepository.SavedEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_Should_Surface_Index_Failure()
    {
        var documentStore = new FakeRecipeDocumentStore(
            RecipeDocumentSaveResult.Success(@"local-data\recipes\PKG-MEMORY-MODULE.v1.0.0.recipe.json"));
        var indexRepository = new FakeRecipeIndexRepository
        {
            SaveHandler = (_, _) => throw new InvalidOperationException("index database unavailable")
        };
        var useCase = new RecipeLibraryUseCase(documentStore, indexRepository);

        var result = await useCase.SaveAsync(new RecipeLibrarySaveRequest(CreateValidRecipe()), CancellationToken.None);

        result.Status.Should().Be(RecipeLibraryOperationStatus.IndexUnavailable);
        result.Message.Should().Contain("index database unavailable");
        result.Entry.Should().BeNull();
        documentStore.SavedRecipes.Should().ContainSingle();
        indexRepository.SavedEntries.Should().BeEmpty();
    }

    private static RecipeDefinition CreateValidRecipe()
    {
        return new RecipeDefinition(
            "PKG-MEMORY-MODULE",
            "Memory Module Sample",
            "1.0.0",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
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

    private sealed class FakeRecipeDocumentStore : IRecipeDocumentStore
    {
        private readonly RecipeDocumentSaveResult _saveResult;

        public FakeRecipeDocumentStore(RecipeDocumentSaveResult saveResult)
        {
            _saveResult = saveResult;
        }

        public List<RecipeDefinition> SavedRecipes { get; } = new();

        public Task<RecipeDocumentSaveResult> SaveAsync(
            RecipeDefinition recipe,
            CancellationToken cancellationToken)
        {
            SavedRecipes.Add(recipe);
            return Task.FromResult(_saveResult);
        }

        public Task<RecipeDocumentLoadResult> LoadAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(RecipeDocumentLoadResult.NotFound("unused"));
        }
    }

    private sealed class FakeRecipeIndexRepository : IRecipeIndexRepository
    {
        public List<RecipeIndexEntry> SavedEntries { get; } = new();

        public Func<RecipeIndexEntry, CancellationToken, Task>? SaveHandler { get; init; }

        public async Task SaveAsync(RecipeIndexEntry entry, CancellationToken cancellationToken)
        {
            if (SaveHandler is not null)
            {
                await SaveHandler(entry, cancellationToken).ConfigureAwait(false);
                return;
            }

            SavedEntries.Add(entry);
        }

        public Task<RecipeIndexEntry?> FindAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(SavedEntries.FirstOrDefault(entry =>
                string.Equals(entry.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<RecipeIndexEntry?> FindActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(SavedEntries
                .OrderByDescending(entry => entry.UpdatedAt)
                .FirstOrDefault(entry => entry.IsActive));
        }

        public Task<bool> SetActiveAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            var hasTarget = SavedEntries.Any(entry =>
                string.Equals(entry.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase));

            if (!hasTarget)
            {
                return Task.FromResult(false);
            }

            for (var index = 0; index < SavedEntries.Count; index++)
            {
                var entry = SavedEntries[index];
                var isTarget =
                    string.Equals(entry.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase);
                SavedEntries[index] = entry with { IsActive = isTarget };
            }

            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<RecipeIndexEntry>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RecipeIndexEntry>>(SavedEntries.Take(limit).ToArray());
        }
    }
}
