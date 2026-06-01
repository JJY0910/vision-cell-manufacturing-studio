using System.Windows;

namespace VisionCell.App.Interaction;

public sealed class MessageBoxConfirmationService : IUserConfirmationService
{
    public Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
