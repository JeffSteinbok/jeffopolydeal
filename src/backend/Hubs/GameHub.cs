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

        public string CreateGame()
        {
            try
            {
                return _gameCache.CreateGame();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateGame");
                throw;
            }
        }

        public async Task JoinGame(string gameCode, string playerName)
        {
            try
            {
                if (string.IsNullOrEmpty(gameCode)) throw new ArgumentNullException(nameof(gameCode));
                if (string.IsNullOrEmpty(playerName)) throw new ArgumentNullException(nameof(playerName));
                await _gameCache.JoinGameAsync(Context.ConnectionId, gameCode, playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JoinGame for {GameCode}", gameCode);
            }
        }

        public async Task StartGame(string gameCode)
        {
            try
            {
                if (string.IsNullOrEmpty(gameCode)) throw new ArgumentNullException(nameof(gameCode));
                await _gameCache.StartGameAsync(gameCode);
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
    }
}
