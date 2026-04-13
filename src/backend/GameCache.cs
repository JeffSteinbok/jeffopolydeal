using JeffopolyDeal.Hubs;
using JeffopolyDeal.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace JeffopolyDeal
{
    /// <summary>
    /// Singleton cache of active games; holds the SignalR hub context.
    /// </summary>
    public class GameCache
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ConcurrentDictionary<string, string> _connectionToGame = new();
        private readonly ConcurrentDictionary<string, Game> _games = new();
        private readonly Random _rng = new();

        public GameCache(IHubContext<GameHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public string CreateGame(string? fixedCode = null)
        {
            if (!string.IsNullOrEmpty(fixedCode))
            {
                var code = fixedCode.ToUpperInvariant();
                var game = new Game(_hubContext, code);
                _games[code] = game;
                return code;
            }

            string gameCode;
            do
            {
                gameCode = GenerateGameCode();
            } while (_games.ContainsKey(gameCode));

            var newGame = new Game(_hubContext, gameCode);
            _games[gameCode] = newGame;
            return gameCode;
        }

        public async Task JoinGameAsync(string connectionId, string gameCode, string playerName)
        {
            gameCode = gameCode.ToUpperInvariant();
            if (!_games.TryGetValue(gameCode, out var game))
                return;

            _connectionToGame[connectionId] = gameCode;
            await game.ConnectPlayerAsync(connectionId, playerName);
        }

        public async Task StartGameAsync(string gameCode, bool allowSinglePlayer = false)
        {
            gameCode = gameCode.ToUpperInvariant();
            if (!_games.TryGetValue(gameCode, out var game))
                return;

            await game.StartGameAsync(allowSinglePlayer);
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
                if (game.IsEmpty)
                {
                    _games.TryRemove(gameCode, out _);
                }
            }
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
