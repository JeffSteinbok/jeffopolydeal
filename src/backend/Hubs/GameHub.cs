using JeffopolyDeal.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace JeffopolyDeal.Hubs
{
    public class GameHub : Hub
    {
        private readonly GameCache _gameCache;
        private readonly ILogger<GameHub> _logger;

        public GameHub(GameCache gameCache, ILogger<GameHub> logger)
        {
            _gameCache = gameCache;
            _logger = logger;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _gameCache.RemoveConnectionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public string CreateGame(string? fixedCode = null)
        {
            try
            {
                return _gameCache.CreateGame(fixedCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateGame");
                throw;
            }
        }

        public async Task JoinGame(string gameCode, string playerName, string playerId)
        {
            try
            {
                if (string.IsNullOrEmpty(gameCode)) throw new ArgumentNullException(nameof(gameCode));
                if (string.IsNullOrEmpty(playerName)) throw new ArgumentNullException(nameof(playerName));
                if (string.IsNullOrEmpty(playerId)) throw new ArgumentNullException(nameof(playerId));
                await _gameCache.JoinGameAsync(Context.ConnectionId, gameCode, playerName, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JoinGame for {GameCode}", gameCode);
            }
        }

        /// <summary>
        /// Rejoin an active game after reconnection/page reload.
        /// Returns true if successfully reconnected.
        /// </summary>
        public async Task<bool> RejoinGame(string gameCode, string playerName, string playerId)
        {
            try
            {
                if (string.IsNullOrEmpty(gameCode)) return false;
                if (string.IsNullOrEmpty(playerName)) return false;
                if (string.IsNullOrEmpty(playerId)) return false;
                return await _gameCache.RejoinGameAsync(Context.ConnectionId, gameCode, playerName, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RejoinGame for {GameCode}", gameCode);
                return false;
            }
        }

        public async Task StartGame(string gameCode, bool allowSinglePlayer = false, bool populateBoards = false)
        {
            try
            {
                if (string.IsNullOrEmpty(gameCode)) throw new ArgumentNullException(nameof(gameCode));
                await _gameCache.StartGameAsync(gameCode, allowSinglePlayer, populateBoards);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StartGame for {GameCode}", gameCode);
            }
        }

        public async Task DrawCards()
        {
            try
            {
                await _gameCache.DrawCardsAsync(Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DrawCards");
            }
        }

        public async Task PlayCard(int cardId, PlayCardRequest request)
        {
            try
            {
                await _gameCache.PlayCardAsync(Context.ConnectionId, cardId, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PlayCard for card {CardId}", cardId);
            }
        }

        public async Task EndTurn()
        {
            try
            {
                await _gameCache.EndTurnAsync(Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EndTurn");
            }
        }

        public async Task DiscardCard(int cardId)
        {
            try
            {
                await _gameCache.DiscardCardAsync(Context.ConnectionId, cardId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DiscardCard for card {CardId}", cardId);
            }
        }

        public async Task RespondToAction(ActionResponse response)
        {
            try
            {
                await _gameCache.RespondToActionAsync(Context.ConnectionId, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RespondToAction");
            }
        }

        public DebugDeckInfo? GetDebugDeckInfo()
        {
            try
            {
                return _gameCache.GetDebugDeckInfo(Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDebugDeckInfo");
                return null;
            }
        }

        public async Task FlipWildcard(int cardId)
        {
            try
            {
                await _gameCache.FlipWildcardAsync(Context.ConnectionId, cardId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FlipWildcard for card {CardId}", cardId);
            }
        }

        public async Task MoveProperty(int cardId, int targetSetId, string? targetColor)
        {
            try
            {
                PropertyColor? color = null;
                if (targetColor != null && Enum.TryParse<PropertyColor>(targetColor, out var parsed))
                    color = parsed;
                await _gameCache.MovePropertyAsync(Context.ConnectionId, cardId, targetSetId, color);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MoveProperty for card {CardId}", cardId);
            }
        }
    }
}
