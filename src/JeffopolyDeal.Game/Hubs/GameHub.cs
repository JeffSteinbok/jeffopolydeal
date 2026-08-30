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

        /// <summary>
        /// Which client this connection came from, for telemetry. The iOS app and
        /// the browser now reach this hub through the same JavaScript client, so
        /// nothing else distinguishes them. It travels in the query string
        /// because that is the only thing every SignalR transport carries — a
        /// browser cannot set headers on a WebSocket handshake.
        /// </summary>
        private string ClientKind
        {
            get
            {
                var kind = Context.GetHttpContext()?.Request.Query["client"].ToString();
                return kind is "ios-app" or "pwa" or "browser" ? kind : "unknown";
            }
        }

        public override Task OnConnectedAsync()
        {
            var clientKind = ClientKind;
            System.Diagnostics.Activity.Current?.SetTag("jeffopoly.client_kind", clientKind);
            _logger.LogInformation(
                "SignalR connected {ConnectionId} from {ClientKind}", Context.ConnectionId, clientKind);
            return base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            if (exception != null)
            {
                _logger.LogWarning(exception, "SignalR disconnected {ConnectionId} (exception)", connectionId);
            }
            else
            {
                _logger.LogInformation("SignalR disconnected {ConnectionId}", connectionId);
            }

            await _gameCache.RemoveConnectionAsync(connectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public string CreateGame(string? fixedCode = null, string? themeName = null)
        {
            try
            {
                return _gameCache.CreateGame(fixedCode, themeName);
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
                _logger.LogInformation("JoinGame {GameCode} {PlayerName} {PlayerId} {ConnectionId}",
                    gameCode, playerName, playerId, Context.ConnectionId);
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
                _logger.LogInformation("RejoinGame requested {GameCode} {PlayerName} {PlayerId} {ConnectionId}",
                    gameCode, playerName, playerId, Context.ConnectionId);
                var result = await _gameCache.RejoinGameAsync(Context.ConnectionId, gameCode, playerName, playerId);
                _logger.LogInformation("RejoinGame result {GameCode} {PlayerId} {ConnectionId} {Success}",
                    gameCode, playerId, Context.ConnectionId, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RejoinGame for {GameCode}", gameCode);
                return false;
            }
        }

        public bool RegisterPushToken(string playerId, string deviceToken)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(deviceToken))
                return false;

            return _gameCache.RegisterPushToken(Context.ConnectionId, playerId, deviceToken);
        }

        public async Task AddBotPlayer(string gameCode)
        {
            try
            {
                if (string.IsNullOrEmpty(gameCode)) throw new ArgumentNullException(nameof(gameCode));
                await _gameCache.AddBotPlayerAsync(gameCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddBotPlayer for {GameCode}", gameCode);
            }
        }

        public async Task StartGame(
            string gameCode,
            bool allowSinglePlayer = false,
            bool populateBoards = false,
            bool addBots = false)
        {
            try
            {
                if (string.IsNullOrEmpty(gameCode)) throw new ArgumentNullException(nameof(gameCode));
                await _gameCache.StartGameAsync(gameCode, allowSinglePlayer, populateBoards, addBots);
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

        public async Task CancelDiscard()
        {
            try
            {
                await _gameCache.CancelDiscardAsync(Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CancelDiscard");
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

        /// <summary>
        /// Cheap liveness probe. A client returning from the background cannot
        /// trust its own connection state — iOS freezes sockets while suspended,
        /// so a dead connection keeps reporting Connected until a keepalive
        /// eventually fails. Asking the server is the only honest answer.
        /// </summary>
        public bool Ping() => true;

        /// <summary>
        /// Lets a client say whether it is able to receive push at all. Without
        /// this, a device that never obtained an APNs token is indistinguishable
        /// from one that obtained a token we then failed to register.
        /// </summary>
        public void ReportPushStatus(string clientKind, bool nativeHost, bool hasToken)
        {
            _logger.LogInformation(
                "Push status from {ConnectionId}: kind={ClientKind} nativeHost={NativeHost} hasToken={HasToken}",
                Context.ConnectionId, clientKind, nativeHost, hasToken);
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

        public async Task EndGame()
        {
            try
            {
                await _gameCache.EndGameAsync(Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EndGame");
            }
        }

        public async Task<string> DebugCommand(string command)
        {
            try
            {
                return await _gameCache.DebugCommandAsync(Context.ConnectionId, command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DebugCommand: {Command}", command);
                return $"Error: {ex.Message}";
            }
        }
    }
}
