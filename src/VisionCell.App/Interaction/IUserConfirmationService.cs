namespace VisionCell.App.Interaction;

public interface IUserConfirmationService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken);
}
