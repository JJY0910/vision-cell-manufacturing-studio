using FluentAssertions;
using VisionCell.Application.Inspection;
using VisionCell.Application.Recipes;
using Xunit;

namespace VisionCell.Application.Tests;

public sealed class InspectionReinspectRecipePolicyUseCaseTests
{
    [Fact]
    public async Task ResolveAsync_Should_Report_Match_When_Active_Recipe_Equals_Historical_Result()
    {
        var preparation = CreatePreparation("RCP-OFFLINE", "1.0.0");
        var useCase = new InspectionReinspectRecipePolicyUseCase(new FakeActiveRecipeContext(
            ActiveRecipeContextResult.Success(CreateRecipe("RCP-OFFLINE", "1.0.0", isValid: true))));

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectRecipePolicyStatus.ActiveMatchesHistorical);
        result.HistoricalRecipeText.Should().Be("RCP-OFFLINE v1.0.0");
        result.ActiveRecipeText.Should().Be("RCP-OFFLINE v1.0.0");
        result.Message.Should().Contain("does not execute live replay");
    }

    [Fact]
    public async Task ResolveAsync_Should_Report_Difference_When_Active_Recipe_Differs_From_Historical_Result()
    {
        var preparation = CreatePreparation("RCP-HIST", "1.0.0");
        var useCase = new InspectionReinspectRecipePolicyUseCase(new FakeActiveRecipeContext(
            ActiveRecipeContextResult.Success(CreateRecipe("RCP-ACTIVE", "2.0.0", isValid: true))));

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectRecipePolicyStatus.ActiveDiffersFromHistorical);
        result.PolicyLabel.Should().Be("Current active Recipe differs");
        result.ActiveRecipeText.Should().Be("RCP-ACTIVE v2.0.0");
        result.Message.Should().Contain("Source-image replay policy remains unimplemented");
    }

    [Fact]
    public async Task ResolveAsync_Should_Report_HistoricalOnly_When_No_Active_Recipe_Is_Selected()
    {
        var preparation = CreatePreparation("RCP-HIST", "1.0.0");
        var useCase = new InspectionReinspectRecipePolicyUseCase(new FakeActiveRecipeContext(
            ActiveRecipeContextResult.NotSelected()));

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectRecipePolicyStatus.HistoricalOnly);
        result.ActiveRecipeText.Should().Be("-");
        result.Message.Should().Contain("selected historical result context only");
    }

    [Fact]
    public async Task ResolveAsync_Should_Report_Invalid_Active_Recipe()
    {
        var preparation = CreatePreparation("RCP-HIST", "1.0.0");
        var useCase = new InspectionReinspectRecipePolicyUseCase(new FakeActiveRecipeContext(
            ActiveRecipeContextResult.InvalidRecipe(CreateRecipe("RCP-ACTIVE", "2.0.0", isValid: false))));

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectRecipePolicyStatus.ActiveRecipeInvalid);
        result.ActiveRecipeText.Should().Be("RCP-ACTIVE v2.0.0");
        result.Message.Should().Contain("invalid");
    }

    private static InspectionReinspectPreparation CreatePreparation(string recipeId, string version)
    {
        return new InspectionReinspectPreparation(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "LOT-20260603120000",
            recipeId,
            version,
            "Pass",
            TimeSpan.FromMilliseconds(123),
            0,
            "corr-001",
            "camera-frame://VirtualCamera/source",
            "inspection-artifacts/result.overlay.bmp",
            "inspection-artifacts/result.height.bmp",
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero),
            true,
            "Ready for metadata comparison.");
    }

    private static RecipeIndexEntry CreateRecipe(string recipeId, string version, bool isValid)
    {
        return new RecipeIndexEntry(
            Guid.NewGuid(),
            recipeId,
            version,
            "Memory Module",
            $@"assets\recipes\{recipeId}.v{version}.recipe.json",
            "0123456789abcdef",
            IsActive: true,
            IsValid: isValid,
            isValid ? null : "validation failed",
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 3, 12, 10, 0, TimeSpan.Zero));
    }

    private sealed class FakeActiveRecipeContext : IActiveRecipeContext
    {
        private readonly ActiveRecipeContextResult _result;

        public FakeActiveRecipeContext(ActiveRecipeContextResult result)
        {
            _result = result;
        }

        public Task<ActiveRecipeContextResult> GetActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }
}
