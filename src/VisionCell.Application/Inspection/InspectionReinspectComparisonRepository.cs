namespace VisionCell.Application.Inspection;

public interface IInspectionReinspectComparisonRepository
{
    Task SaveAsync(
        InspectionReinspectComparisonResult result,
        CancellationToken cancellationToken);
}

public interface IInspectionReinspectComparisonReader
{
    Task<IReadOnlyList<InspectionReinspectComparisonResult>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}
