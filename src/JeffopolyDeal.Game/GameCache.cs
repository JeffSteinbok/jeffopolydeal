using JeffopolyDeal.Hubs;
using JeffopolyDeal.Models;
using JeffopolyDeal.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JeffopolyDeal
{
    /// <summary>
    /// Singleton cache of active games; holds the SignalR hub context.
    /// </summary>
    public class GameCache
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ITurnNotificationService _turnNotificationService;
        private readonly IPushTokenStore _pushTokenStore;
        private readonly ConcurrentDictionary<string, string> _connectionToGame = new();
        private readonly ConcurrentDictionary<string, Game> _games = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cleanupTimers = new();
        private readonly Random _rng = new();
        private static readonly TimeSpan LobbyCleanupDelay = TimeSpan.FromMinutes(2);

        public GameCache(
            IHubContext<GameHub> hubContext,
            ILoggerFactory loggerFactory,
            ITurnNotificationService? turnNotificationService = null,
            IPushTokenStore? pushTokenStore = null)
        {
            _hubContext = hubContext;
            _loggerFactory = loggerFactory;
            _turnNotificationService = turnNotificationService ?? NullTurnNotificationService.Instance;
            _pushTokenStore = pushTokenStore ?? new PushTokenStore();
        }

        public string CreateGame(string? fixedCode = null, string? themeName = null)
        {
            if (!string.IsNullOrEmpty(fixedCode))
            {
                var code = fixedCode.ToUpperInvariant();
                var game = new Game(_hubContext, _loggerFactory.CreateLogger<Game>(), code, themeName, _turnNotificationService);
                _games[code] = game;
                return code;
            }

            string gameCode;
            do
            {
                gameCode = GenerateGameCode();
            } while (_games.ContainsKey(gameCode));

            var newGame = new Game(_hubContext, _loggerFactory.CreateLogger<Game>(), gameCode, themeName, _turnNotificationService);
            _games[gameCode] = newGame;
            return gameCode;
        }

        public async Task JoinGameAsync(string connectionId, string gameCode, string playerName, string playerId)
        {
            gameCode = gameCode.ToUpperInvariant();
            if (!_games.TryGetValue(gameCode, out var game))
                return;

            CancelCleanupTimer(gameCode);
            _connectionToGame[connectionId] = gameCode;
            await game.ConnectPlayerAsync(connectionId, playerName, playerId);
        }

        /// <summary>
        /// Reconnect a player to an active game using their stable PlayerId.
        /// Returns true if the player was found and reconnected.
        /// </summary>
        public async Task<bool> RejoinGameAsync(string connectionId, string gameCode, string playerName, string playerId)
        {
            gameCode = gameCode.ToUpperInvariant();
            if (!_games.TryGetValue(gameCode, out var game))
                return false;

            CancelCleanupTimer(gameCode);
            _connectionToGame[connectionId] = gameCode;
            return await game.ReconnectPlayerAsync(connectionId, playerName, playerId);
        }

        public bool RegisterPushToken(string connectionId, string playerId, string deviceToken)
        {
            if (string.IsNullOrWhiteSpace(deviceToken)
                || deviceToken.Length != 64
                || deviceToken.Any(character => !Uri.IsHexDigit(character)))
                return false;

            var game = GetGameForConnection(connectionId);
            if (game == null || !game.MatchesPlayer(connectionId, playerId))
                return false;

            _pushTokenStore.Register(playerId, deviceToken.ToLowerInvariant());
            return true;
        }

        public async Task AddBotPlayerAsync(string gameCode)
        {
            gameCode = gameCode.ToUpperInvariant();
            if (!_games.TryGetValue(gameCode, out var game))
                return;

            if (game.AddBotPlayer())
            {
                await game.BroadcastGameStateAsync();
            }
        }

        public async Task StartGameAsync(
            string gameCode,
            bool allowSinglePlayer = false,
            bool populateBoards = false,
            bool addBots = false)
        {
            gameCode = gameCode.ToUpperInvariant();
            if (!_games.TryGetValue(gameCode, out var game))
                return;

            if (populateBoards || addBots)
            {
                game.AddBotPlayers(3);
            }

            await game.StartGameAsync(allowSinglePlayer, populateBoards);
        }

        public async Task DrawCardsAsync(string connectionId)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return;
            await game.DrawCardsAsync(connectionId);
        }

        public async Task PlayCardAsync(string connectionId, int cardId, PlayCardRequest request)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return;
            await game.PlayCardAsync(connectionId, cardId, request);
        }

        public async Task EndTurnAsync(string connectionId)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return;
            await game.EndTurnAsync(connectionId);
        }

        public async Task DiscardCardAsync(string connectionId, int cardId)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return;
            await game.DiscardCardAsync(connectionId, cardId);
        }

        public async Task CancelDiscardAsync(string connectionId)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return;
            await game.CancelDiscardAsync(connectionId);
        }

        public async Task RespondToActionAsync(string connectionId, ActionResponse response)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return;
            await game.RespondToActionAsync(connectionId, response);
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            if (!_connectionToGame.TryRemove(connectionId, out var gameCode))
                return;

            if (_games.TryGetValue(gameCode, out var game))
            {
                await game.RemovePlayerAsync(connectionId);

                if (game.CanBeDeleted)
                {
                    // Schedule delayed cleanup — gives disconnected lobby players time to rejoin
                    ScheduleGameCleanup(gameCode);
                }
            }
        }

        /// <summary>
        /// Schedule a delayed cleanup for a lobby game. Cancels any existing timer for this game.
        /// </summary>
        private void ScheduleGameCleanup(string gameCode)
        {
            // Cancel any existing timer for this game
            CancelCleanupTimer(gameCode);

            var cts = new CancellationTokenSource();
            _cleanupTimers[gameCode] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(LobbyCleanupDelay, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    return; // Timer was cancelled (player reconnected)
                }

                // Timer fired — clean up expired players first
                if (_games.TryGetValue(gameCode, out var game))
                {
                    await game.CleanupExpiredLobbyPlayersAsync();

                    // Re-check: if still deletable, remove the game
                    if (game.CanBeDeleted)
                    {
                        _games.TryRemove(gameCode, out _);
                    }
                }

                _cleanupTimers.TryRemove(gameCode, out _);
            });
        }

        /// <summary>Cancel a pending cleanup timer for a game (e.g., when a player reconnects).</summary>
        private void CancelCleanupTimer(string gameCode)
        {
            if (_cleanupTimers.TryRemove(gameCode, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        public Task EndGameAsync(string connectionId)
        {
            if (!_connectionToGame.TryGetValue(connectionId, out var gameCode))
                return Task.CompletedTask;

            _games.TryRemove(gameCode, out _);

            var relatedConnections = _connectionToGame
                .Where(kvp => kvp.Value == gameCode)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var relatedConnection in relatedConnections)
            {
                _connectionToGame.TryRemove(relatedConnection, out _);
            }

            return Task.CompletedTask;
        }

        public DebugDeckInfo? GetDebugDeckInfo(string connectionId)
        {
            var game = GetGameForConnection(connectionId);
            return game?.GetDebugDeckInfo();
        }

        public async Task FlipWildcardAsync(string connectionId, int cardId)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return;
            await game.FlipWildcardAsync(connectionId, cardId);
        }

        public async Task MovePropertyAsync(string connectionId, int cardId, int targetSetId, PropertyColor? targetColor)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return;
            await game.MovePropertyAsync(connectionId, cardId, targetSetId, targetColor);
        }

        public async Task<string> DebugCommandAsync(string connectionId, string command)
        {
            var game = GetGameForConnection(connectionId);
            if (game == null) return "Error: no game found";
            var result = await game.DebugCommandAsync(connectionId, command);
            await game.BroadcastGameStateAsync();
            return result;
        }

        private Game? GetGameForConnection(string connectionId)
        {
            if (!_connectionToGame.TryGetValue(connectionId, out var gameCode))
                return null;
            _games.TryGetValue(gameCode, out var game);
            return game;
        }

        private string GenerateGameCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var code = new char[4];
            for (int i = 0; i < 4; i++)
                code[i] = chars[_rng.Next(chars.Length)];
            return new string(code);
        }
    }
}
