using FluentAssertions;
using VisionCell.Application.Recipes;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class ActiveRecipeContextTests
{
    [Fact]
    public async Task GetActiveAsync_Should_Return_NotSelected_When_No_Recipe_Is_Active()
    {
        var context = new ActiveRecipeContext(new FakeRecipeIndexRepository());

        var result = await context.GetActiveAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ActiveRecipeContextStatus.NotSelected);
        result.Entry.Should().BeNull();
        result.Message.Should().Contain("No active recipe");
    }

    [Fact]
    public async Task GetActiveAsync_Should_Return_Success_For_Valid_Active_Recipe()
    {
        var entry = CreateEntry(isActive: true, isValid: true, validationSummary: "Valid");
        var context = new ActiveRecipeContext(new FakeRecipeIndexRepository(entry));

        var result = await context.GetActiveAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ActiveRecipeContextStatus.Success);
        result.Entry.Should().Be(entry);
        result.RecipeId.Should().Be("PKG-MEMORY-MODULE");
        result.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task GetActiveAsync_Should_Return_InvalidRecipe_For_Invalid_Active_Recipe()
    {
        var entry = CreateEntry(isActive: true, isValid: false, validationSummary: "Missing ROI");
        var context = new ActiveRecipeContext(new FakeRecipeIndexRepository(entry));

        var result = await context.GetActiveAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ActiveRecipeContextStatus.InvalidRecipe);
        result.Entry.Should().Be(entry);
        result.Message.Should().Contain("Missing ROI");
    }

    [Fact]
    public async Task GetActiveAsync_Should_Return_RepositoryUnavailable_For_Index_Failure()
    {
        var repository = new FakeRecipeIndexRepository
        {
            FindActiveHandler = _ => throw new InvalidOperationException("recipe index unavailable")
        };
        var context = new ActiveRecipeContext(repository);

        var result = await context.GetActiveAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ActiveRecipeContextStatus.RepositoryUnavailable);
        result.Message.Should().Contain("recipe index unavailable");
    }

    private static RecipeIndexEntry CreateEntry(
        bool isActive,
        bool isValid,
        string? validationSummary)
    {
        return new RecipeIndexEntry(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "PKG-MEMORY-MODULE",
            "1.0.0",
            "Memory Module",
            @"local-data\recipes\PKG-MEMORY-MODULE.v1.0.0.recipe.json",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            isActive,
            isValid,
            validationSummary,
            new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));
    }

    private sealed class FakeRecipeIndexRepository : IRecipeIndexRepository
    {
        private readonly List<RecipeIndexEntry> _entries = new();

        public FakeRecipeIndexRepository(params RecipeIndexEntry[] entries)
        {
            _entries.AddRange(entries);
        }

        public Func<CancellationToken, Task<RecipeIndexEntry?>>? FindActiveHandler { get; init; }

        public Task SaveAsync(RecipeIndexEntry entry, CancellationToken cancellationToken)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<RecipeIndexEntry?> FindAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_entries.FirstOrDefault(entry =>
                string.Equals(entry.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<RecipeIndexEntry?> FindActiveAsync(CancellationToken cancellationToken)
        {
            if (FindActiveHandler is not null)
            {
                return FindActiveHandler(cancellationToken);
            }

            return Task.FromResult(_entries.FirstOrDefault(entry => entry.IsActive));
        }

        public Task<bool> SetActiveAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<RecipeIndexEntry>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RecipeIndexEntry>>(_entries.Take(limit).ToArray());
        }
    }
}
