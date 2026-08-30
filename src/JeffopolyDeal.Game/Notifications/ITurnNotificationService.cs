using System.Threading;
using System.Threading.Tasks;

namespace JeffopolyDeal.Notifications;

public interface ITurnNotificationService
{
    Task NotifyTurnAsync(
        string playerId,
        string playerName,
        string gameCode,
        string hostName,
        CancellationToken cancellationToken = default);
}

public sealed class NullTurnNotificationService : ITurnNotificationService
{
    public static NullTurnNotificationService Instance { get; } = new();

    private NullTurnNotificationService()
    {
    }

    public Task NotifyTurnAsync(
        string playerId,
        string playerName,
        string gameCode,
        string hostName,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

