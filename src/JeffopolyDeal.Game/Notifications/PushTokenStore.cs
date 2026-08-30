using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace JeffopolyDeal.Notifications;

public interface IPushTokenStore
{
    void Register(string playerId, string deviceToken);
    IReadOnlyCollection<string> GetTokens(string playerId);
    void Remove(string playerId, string deviceToken);
}

public sealed class PushTokenStore : IPushTokenStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tokens = new();

    public void Register(string playerId, string deviceToken)
    {
        var playerTokens = _tokens.GetOrAdd(playerId, _ => new ConcurrentDictionary<string, byte>());
        playerTokens[deviceToken] = 0;
    }

    public IReadOnlyCollection<string> GetTokens(string playerId) =>
        _tokens.TryGetValue(playerId, out var playerTokens)
            ? playerTokens.Keys.ToArray()
            : [];

    public void Remove(string playerId, string deviceToken)
    {
        if (!_tokens.TryGetValue(playerId, out var playerTokens))
            return;

        playerTokens.TryRemove(deviceToken, out _);
        if (playerTokens.IsEmpty)
            _tokens.TryRemove(playerId, out _);
    }
}

