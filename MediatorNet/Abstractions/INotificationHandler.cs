namespace MediatorNet.Abstractions;

/// <summary>
/// Defines a handler for a notification
/// </summary>
/// <typeparam name="TNotification">The type of notification being handled</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles a notification
    /// </summary>
    /// <param name="notification">The notification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification handling operation</returns>
    ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}