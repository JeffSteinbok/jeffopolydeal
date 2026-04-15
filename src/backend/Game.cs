using JeffopolyDeal.Cards;
using JeffopolyDeal.Hubs;
using JeffopolyDeal.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JeffopolyDeal
{
    /// <summary>
    /// Represents a game in progress.
    /// Operations on this class may cause side-effects such as outgoing SignalR calls.
    /// </summary>
    public class Game
    {
        private readonly object _lock = new();
        private readonly IHubContext<GameHub> _hubContext;
        private readonly Deck _deck;

        public string GameCode { get; }

        private readonly List<Player> _players = new();
        private readonly Dictionary<string, bool> _connections = new();

        private int _currentPlayerIndex;
        private int _playsUsed;
        private GamePhase _phase = GamePhase.Lobby;
        private PendingAction? _pendingAction;
        private string? _winnerId;
        private string? _lastPaymentError;
        private string? _lastPaymentErrorConnectionId;
        private readonly List<GameAction> _recentActions = new();
        private const int MaxRecentActions = 20;
        private int _nextActionId = 1;

        public Game(IHubContext<GameHub> hubContext, string gameCode)
        {
            _hubContext = hubContext;
            GameCode = gameCode;
            _deck = new Deck();
        }

        public bool IsEmpty
        {
            get { lock (_lock) { return _connections.Count == 0; } }
        }

        #region Connection Management

        public async Task ConnectPlayerAsync(string connectionId, string playerName, string playerId)
        {
            lock (_lock)
            {
                if (_phase != GamePhase.Lobby)
                    return;

                _connections[connectionId] = true;

                if (_players.Any(p => p.ConnectionId == connectionId))
                    return;

                _players.Add(new Player
                {
                    PlayerId = playerId,
                    ConnectionId = connectionId,
                    Name = playerName
                });
            }

            await _hubContext.Groups.AddToGroupAsync(connectionId, GameCode);
            await BroadcastGameStateAsync();
        }

        public async Task RemovePlayerAsync(string connectionId)
        {
            lock (_lock)
            {
                _connections.Remove(connectionId);
                // Only remove from player list during lobby — active game players may reconnect
                if (_phase == GamePhase.Lobby)
                {
                    _players.RemoveAll(p => p.ConnectionId == connectionId);
                }
            }
            await BroadcastGameStateAsync();
        }

        /// <summary>Whether the game can be safely deleted (no connections AND in lobby or game over).</summary>
        public bool CanBeDeleted
        {
            get { lock (_lock) { return _connections.Count == 0 && (_phase == GamePhase.Lobby || _phase == GamePhase.GameOver); } }
        }

        public async Task<bool> ReconnectPlayerAsync(string newConnectionId, string playerName, string playerId)
        {
            bool found = false;
            lock (_lock)
            {
                var player = _players.FirstOrDefault(p => p.PlayerId == playerId);
                if (player != null)
                {
                    var oldConnectionId = player.ConnectionId;
                    _connections.Remove(oldConnectionId);
                    _connections[newConnectionId] = true;
                    player.ConnectionId = newConnectionId;
                    found = true;
                }
                else if (_phase == GamePhase.Lobby)
                {
                    // Player not found — join as new player in lobby
                    _connections[newConnectionId] = true;
                    _players.Add(new Player { PlayerId = playerId, ConnectionId = newConnectionId, Name = playerName });
                    found = true;
                }
            }

            if (found)
            {
                await _hubContext.Groups.AddToGroupAsync(newConnectionId, GameCode);
                await BroadcastGameStateAsync();
            }
            return found;
        }

        #endregion

        #region Game Flow

        public async Task StartGameAsync(bool allowSinglePlayer = false, bool populateBoards = false)
        {
            lock (_lock)
            {
                int minPlayers = allowSinglePlayer ? 1 : 2;
                if (_phase != GamePhase.Lobby || _players.Count < minPlayers)
                    return;

                // Deal initial hands
                foreach (var player in _players)
                {
                    player.Hand.AddRange(_deck.Draw(GameConfig.InitialHandSize));
                }

                _currentPlayerIndex = 0;
                _playsUsed = 0;
                _phase = GamePhase.Draw;

                if (populateBoards)
                {
                    PopulateBoardsForDebug();
                }
            }
            await BroadcastGameStateAsync();
        }

        /// <summary>
        /// Adds bot players to the lobby for debug purposes.
        /// </summary>
        public void AddBotPlayers(int count)
        {
            lock (_lock)
            {
                if (_phase != GamePhase.Lobby) return;
                var botNames = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve" };
                for (int i = 0; i < count && _players.Count < 5; i++)
                {
                    var name = botNames[i % botNames.Length];
                    var botId = $"bot-{Guid.NewGuid():N}";
                    _players.Add(new Player { PlayerId = botId, ConnectionId = botId, Name = name });
                    _connections[botId] = true;
                }
            }
        }

        /// <summary>
        /// Randomly populates all players' boards with properties, money, and cards in hand.
        /// Only used in debug mode.
        /// </summary>
        private void PopulateBoardsForDebug()
        {
            var rng = new Random(42); // deterministic seed for reproducibility

            foreach (var player in _players)
            {
                // Give each player some bank cards (M1–M5, random mix)
                int bankCards = rng.Next(3, 8);
                for (int i = 0; i < bankCards; i++)
                {
                    var denominations = new[] { 1, 1, 2, 2, 3, 4, 5 };
                    int val = denominations[rng.Next(denominations.Length)];
                    player.Bank.Add(_deck.CreateCard(CardType.Money, val, $"M{val}"));
                }

                // Give each player 2-4 property sets of random colors
                var availableColors = new List<PropertyColor>(
                    (PropertyColor[])Enum.GetValues(typeof(PropertyColor)));

                int setCount = rng.Next(2, 5);
                for (int s = 0; s < setCount && availableColors.Count > 0; s++)
                {
                    int colorIdx = rng.Next(availableColors.Count);
                    var color = availableColors[colorIdx];
                    availableColors.RemoveAt(colorIdx);

                    var propDefs = PropertyNames.ByColor.ContainsKey(color)
                        ? PropertyNames.ByColor[color]
                        : null;
                    if (propDefs == null) continue;

                    int setSize = GameConfig.SetSize.ContainsKey(color)
                        ? GameConfig.SetSize[color]
                        : propDefs.Length;

                    // Add some or all properties in this color
                    int cardsInSet = rng.Next(1, setSize + 1);
                    var set = player.GetOrCreatePropertySet(color);
                    for (int c = 0; c < cardsInSet && c < propDefs.Length; c++)
                    {
                        int propValue = GameConfig.PropertyValue.TryGetValue(color, out var pv) ? pv : 0;
                        set.Cards.Add(_deck.CreateCard(
                            CardType.Property, propValue, propDefs[c].DisplayName,
                            color: color, cardId: propDefs[c].CardId));
                    }

                    // Sometimes add house/hotel on complete sets
                    if (set.IsComplete && rng.Next(3) == 0
                        && color != PropertyColor.Railroad && color != PropertyColor.Utility)
                    {
                        set.HasHouse = true;
                        if (rng.Next(2) == 0) set.HasHotel = true;
                    }
                }

                // Give extra hand cards
                int handSlotsRemaining = Math.Max(0, GameConfig.MaxHandSize - player.Hand.Count);
                int extraCards = Math.Min(rng.Next(2, 5), handSlotsRemaining);
                if (extraCards > 0)
                {
                    player.Hand.AddRange(_deck.Draw(Math.Min(extraCards, _deck.DrawPileCount)));
                }
            }
        }

        public async Task DrawCardsAsync(string connectionId)
        {
            lock (_lock)
            {
                if (_phase != GamePhase.Draw)
                    return;

                var player = GetCurrentPlayer();
                if (player == null || player.ConnectionId != connectionId)
                    return;

                int drawCount = player.Hand.Count == 0
                    ? GameConfig.DrawWhenEmpty
                    : GameConfig.DrawPerTurn;

                player.Hand.AddRange(_deck.Draw(drawCount));
                _phase = GamePhase.Play;
                _playsUsed = 0;
            }
            await BroadcastGameStateAsync();
        }

        public async Task PlayCardAsync(string connectionId, int cardId, PlayCardRequest request)
        {
            lock (_lock)
            {
                if (_phase != GamePhase.Play)
                    return;

                var player = GetCurrentPlayer();
                if (player == null || player.ConnectionId != connectionId)
                    return;

                if (_playsUsed >= GameConfig.MaxPlaysPerTurn)
                    return;

                var card = player.Hand.FirstOrDefault(c => c.Id == cardId);
                if (card == null)
                    return;

                bool played = ProcessCardPlay(player, card, request);
                if (!played)
                    return;

                LogAction(player.Name, DescribeCardPlay(player, card, request),
                    cardPlayed: card,
                    targetPlayerName: request.TargetPlayerId != null
                        ? _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId)?.Name
                        : null);
                player.Hand.Remove(card);
                _playsUsed++;

                // Check win condition
                if (player.UniqueCompletedSetCount >= GameConfig.SetsToWin)
                {
                    _phase = GamePhase.GameOver;
                    _winnerId = player.ConnectionId;
                }
                // If pending action, wait for response
                else if (_pendingAction != null)
                {
                    _phase = GamePhase.AwaitingResponse;
                    // Auto-respond for any bot targets so the game doesn't stall
                    ResolveBotPendingActions();
                }
            }
            await BroadcastGameStateAsync();
        }

        public async Task EndTurnAsync(string connectionId)
        {
            lock (_lock)
            {
                if (_phase != GamePhase.Play && _phase != GamePhase.Discard)
                    return;

                var player = GetCurrentPlayer();
                if (player == null || player.ConnectionId != connectionId)
                    return;

                // Must discard if over hand limit
                if (player.Hand.Count > GameConfig.MaxHandSize)
                {
                    _phase = GamePhase.Discard;
                }
                else
                {
                    AdvanceTurn();
                }
            }
            await BroadcastGameStateAsync();
        }

        public async Task DiscardCardAsync(string connectionId, int cardId)
        {
            lock (_lock)
            {
                if (_phase != GamePhase.Discard)
                    return;

                var player = GetCurrentPlayer();
                if (player == null || player.ConnectionId != connectionId)
                    return;

                var card = player.Hand.FirstOrDefault(c => c.Id == cardId);
                if (card == null)
                    return;

                player.Hand.Remove(card);
                _deck.Discard(card);

                if (player.Hand.Count <= GameConfig.MaxHandSize)
                {
                    AdvanceTurn();
                }
            }
            await BroadcastGameStateAsync();
        }

        public async Task RespondToActionAsync(string connectionId, ActionResponse response)
        {
            lock (_lock)
            {
                if (_phase != GamePhase.AwaitingResponse || _pendingAction == null)
                    return;

                if (!_pendingAction.TargetPlayerIds.Contains(connectionId))
                    return;

                ProcessResponse(connectionId, response);
                // Auto-respond for any bot targets created by JSN chains or action resolution
                ResolveBotPendingActions();
            }
            await BroadcastGameStateAsync();
        }

        #endregion

        #region Card Play Processing

        private bool ProcessCardPlay(Player player, Card card, PlayCardRequest request)
        {
            // Any card can be banked as money
            if (request.PlayAsMoney)
            {
                player.Bank.Add(card);
                return true;
            }

            switch (card.CardType)
            {
                case CardType.Money:
                    player.Bank.Add(card);
                    return true;

                case CardType.Property:
                    PlayProperty(player, card, card.Color!.Value);
                    return true;

                case CardType.PropertyWildcard:
                    if (card.IsMulticolorWild)
                    {
                        if (request.WildcardColor != null)
                        {
                            card.ActiveColor = request.WildcardColor;
                            PlayProperty(player, card, request.WildcardColor.Value);
                        }
                        else
                        {
                            // Goes to unbound area
                            card.ActiveColor = null;
                            player.UnboundWilds.Add(card);
                        }
                    }
                    else
                    {
                        var color = request.WildcardColor ?? card.ActiveColor ?? card.Color!.Value;
                        card.ActiveColor = color;
                        PlayProperty(player, card, color);
                    }
                    return true;

                case CardType.Rent:
                    return PlayRent(player, card, request);

                case CardType.Action:
                    return PlayAction(player, card, request);

                default:
                    return false;
            }
        }

        private void PlayProperty(Player player, Card card, PropertyColor color)
        {
            var set = player.GetOrCreatePropertySet(color);
            set.Cards.Add(card);
        }

        private bool PlayRent(Player player, Card card, PlayCardRequest request)
        {
            if (request.RentColor == null)
                return false;

            var color = request.RentColor.Value;

            // Validate the player has properties of this color
            var set = player.PropertySets.FirstOrDefault(s => s.Color == color);
            if (set == null || set.Cards.Count == 0)
                return false;

            // Validate the rent card can charge for this color
            if (!card.IsWildRent && (card.RentColors == null || !card.RentColors.Contains(color)))
                return false;

            int rent = set.CalculateRent();

            // Apply Double the Rent cards (each doubles, and each counts as a play)
            if (request.DoubleRentCardIds != null)
            {
                foreach (var doubleId in request.DoubleRentCardIds)
                {
                    if (_playsUsed + 1 >= GameConfig.MaxPlaysPerTurn) break;
                    var doubleCard = player.Hand.FirstOrDefault(c => c.Id == doubleId && c.ActionKind == ActionType.DoubleTheRent);
                    if (doubleCard != null)
                    {
                        player.Hand.Remove(doubleCard);
                        _playsUsed++;
                        _deck.Discard(doubleCard);
                        rent *= 2;
                    }
                }
            }

            _deck.Discard(card);

            List<string> targets;
            if (card.IsWildRent)
            {
                // Wild rent targets one player
                if (request.TargetPlayerId == null)
                    return false;
                targets = new List<string> { request.TargetPlayerId };
            }
            else
            {
                // Dual color rent targets all other players
                targets = _players
                    .Where(p => p.ConnectionId != player.ConnectionId)
                    .Select(p => p.ConnectionId)
                    .ToList();
            }

            if (targets.Count > 0 && rent > 0)
            {
                _pendingAction = new PendingAction
                {
                    Type = PendingActionType.PayRent,
                    SourcePlayerId = player.ConnectionId,
                    SourcePlayerName = player.Name,
                    TargetPlayerIds = targets,
                    Amount = rent,
                };
            }

            return true;
        }

        private bool PlayAction(Player player, Card card, PlayCardRequest request)
        {
            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    player.Hand.AddRange(_deck.Draw(2));
                    _deck.Discard(card);
                    return true;

                case ActionType.DebtCollector:
                    if (request.TargetPlayerId == null) return false;
                    _deck.Discard(card);
                    _pendingAction = new PendingAction
                    {
                        Type = PendingActionType.PayDebtCollector,
                        SourcePlayerId = player.ConnectionId,
                        SourcePlayerName = player.Name,
                        TargetPlayerIds = new List<string> { request.TargetPlayerId },
                        Amount = GameConfig.DebtCollectorAmount,
                    };
                    return true;

                case ActionType.ItsMyBirthday:
                    _deck.Discard(card);
                    _pendingAction = new PendingAction
                    {
                        Type = PendingActionType.PayBirthday,
                        SourcePlayerId = player.ConnectionId,
                        SourcePlayerName = player.Name,
                        TargetPlayerIds = _players
                            .Where(p => p.ConnectionId != player.ConnectionId)
                            .Select(p => p.ConnectionId)
                            .ToList(),
                        Amount = GameConfig.BirthdayAmount,
                    };
                    return true;

                case ActionType.SlyDeal:
                    if (request.TargetPlayerId == null || request.TargetCardId == null) return false;
                    {
                        var target = _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId);
                        if (target == null) return false;
                        var stealable = target.GetStealableProperties();
                        var targetCard = stealable.FirstOrDefault(c => c.Id == request.TargetCardId);
                        if (targetCard == null) return false;

                        _deck.Discard(card);
                        _pendingAction = new PendingAction
                        {
                            Type = PendingActionType.RespondToSlyDeal,
                            SourcePlayerId = player.ConnectionId,
                            SourcePlayerName = player.Name,
                            TargetPlayerIds = new List<string> { request.TargetPlayerId },
                            TargetCardId = request.TargetCardId,
                            TargetCardName = targetCard.Name,
                        };
                    }
                    return true;

                case ActionType.ForceDeal:
                    if (request.TargetPlayerId == null || request.TargetCardId == null || request.OfferedCardId == null) return false;
                    {
                        var target = _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId);
                        if (target == null) return false;
                        var stealable = target.GetStealableProperties();
                        var targetCard = stealable.FirstOrDefault(c => c.Id == request.TargetCardId);
                        if (targetCard == null) return false;
                        var offered = player.GetStealableProperties();
                        var offeredCard = offered.FirstOrDefault(c => c.Id == request.OfferedCardId);
                        if (offeredCard == null) return false;

                        _deck.Discard(card);
                        _pendingAction = new PendingAction
                        {
                            Type = PendingActionType.RespondToForceDeal,
                            SourcePlayerId = player.ConnectionId,
                            SourcePlayerName = player.Name,
                            TargetPlayerIds = new List<string> { request.TargetPlayerId },
                            TargetCardId = request.TargetCardId,
                            TargetCardName = targetCard.Name,
                            OfferedCardId = request.OfferedCardId,
                            OfferedCardName = offeredCard.Name,
                        };
                    }
                    return true;

                case ActionType.DealBreaker:
                    if (request.TargetPlayerId == null || request.TargetSetColor == null) return false;
                    {
                        var target = _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId);
                        if (target == null) return false;
                        var sets = target.GetCompletePropertySets();
                        if (!sets.Any(s => s.Color == request.TargetSetColor)) return false;

                        _deck.Discard(card);
                        _pendingAction = new PendingAction
                        {
                            Type = PendingActionType.RespondToDealBreaker,
                            SourcePlayerId = player.ConnectionId,
                            SourcePlayerName = player.Name,
                            TargetPlayerIds = new List<string> { request.TargetPlayerId },
                            TargetSetColor = request.TargetSetColor,
                        };
                    }
                    return true;

                case ActionType.House:
                    return PlayHouseOrHotel(player, card, request, isHotel: false);

                case ActionType.Hotel:
                    return PlayHouseOrHotel(player, card, request, isHotel: true);

                case ActionType.JustSayNo:
                    // Can only be played reactively, not proactively
                    return false;

                case ActionType.DoubleTheRent:
                    // Must be played with a rent card, handled in PlayRent
                    return false;

                default:
                    return false;
            }
        }

        private bool PlayHouseOrHotel(Player player, Card card, PlayCardRequest request, bool isHotel)
        {
            if (request.TargetSetColor == null) return false;

            var set = player.PropertySets.FirstOrDefault(s => s.Color == request.TargetSetColor);
            if (set == null || !set.IsComplete) return false;

            // Can't put house/hotel on Railroad or Utility
            if (set.Color == PropertyColor.Railroad || set.Color == PropertyColor.Utility)
                return false;

            if (isHotel)
            {
                if (!set.HasHouse || set.HasHotel) return false;
                set.HasHotel = true;
            }
            else
            {
                if (set.HasHouse) return false;
                set.HasHouse = true;
            }

            _deck.Discard(card);
            return true;
        }

        #endregion

        #region Response Processing

        /// <summary>
        /// Validates that the payment meets requirements. Returns null if valid, or error message if invalid.
        /// Rules: If player can afford the full amount, they must pay at least that much.
        /// If player can't afford it, they must pay everything they have.
        /// </summary>
        private string? ValidatePayment(Player payer, List<int> cardIds, int amountOwed)
        {
            var payableCards = payer.GetPayableCards();
            int totalAssets = payableCards.Sum(c => c.MoneyValue);
            int selectedTotal = 0;

            foreach (var id in cardIds)
            {
                var card = payableCards.FirstOrDefault(c => c.Id == id);
                if (card == null) return "Invalid card selected.";
                selectedTotal += card.MoneyValue;
            }

            if (totalAssets >= amountOwed)
            {
                // Player can afford it — must pay at least the required amount
                if (selectedTotal < amountOwed)
                    return $"You must pay at least M{amountOwed}. Selected: M{selectedTotal}.";
            }
            else
            {
                // Player can't afford it — must pay everything
                if (cardIds.Count < payableCards.Count)
                    return "You can't afford the full amount — you must pay everything you have.";
            }

            return null; // Valid
        }

        private void ProcessResponse(string connectionId, ActionResponse response)
        {
            if (_pendingAction == null) return;

            // Handle Just Say No
            if (response.PlayJustSayNo)
            {
                var responder = _players.FirstOrDefault(p => p.ConnectionId == connectionId);
                var justSayNo = responder?.Hand.FirstOrDefault(c => c.ActionKind == ActionType.JustSayNo);
                if (justSayNo != null && responder != null)
                {
                    responder.Hand.Remove(justSayNo);
                    _deck.Discard(justSayNo);
                    LogAction(responder.Name, "played Just Say No!", cardPlayed: justSayNo);

                    // Save original action info the first time a JSN is played (not on subsequent counter-JSNs)
                    if (_pendingAction.OriginalSourcePlayerId == null)
                    {
                        _pendingAction.OriginalSourcePlayerId = _pendingAction.SourcePlayerId;
                        _pendingAction.OriginalActionType = _pendingAction.Type;
                        _pendingAction.OriginalTargetPlayerIds = new List<string>(_pendingAction.TargetPlayerIds);
                    }

                    // Now the source player needs to respond (they can counter with their own JSN)
                    _pendingAction.JustSayNoResponderId = _pendingAction.SourcePlayerId;
                    _pendingAction.Type = PendingActionType.JustSayNoChain;
                    _pendingAction.TargetPlayerIds = new List<string> { _pendingAction.SourcePlayerId };
                    // Swap source and target for the chain
                    _pendingAction.SourcePlayerId = connectionId;
                    return;
                }
            }

            // Handle Just Say No chain acceptance (declining to counter)
            if (_pendingAction.Type == PendingActionType.JustSayNoChain && !response.PlayJustSayNo)
            {
                if (_pendingAction.SourcePlayerId == _pendingAction.OriginalSourcePlayerId
                    && _pendingAction.OriginalActionType != null
                    && _pendingAction.OriginalTargetPlayerIds != null)
                {
                    // The original action's source played the last JSN counter — action proceeds.
                    // Restore the original action and execute it.
                    _pendingAction.Type = _pendingAction.OriginalActionType.Value;
                    _pendingAction.SourcePlayerId = _pendingAction.OriginalSourcePlayerId;
                    _pendingAction.TargetPlayerIds = new List<string>(_pendingAction.OriginalTargetPlayerIds);

                    // Execute steal/swap actions immediately
                    switch (_pendingAction.Type)
                    {
                        case PendingActionType.RespondToSlyDeal:
                            ExecuteSlyDeal();
                            break;
                        case PendingActionType.RespondToForceDeal:
                            ExecuteForceDeal();
                            break;
                        case PendingActionType.RespondToDealBreaker:
                            ExecuteDealBreaker();
                            break;
                    }

                    // Clear pending action if all targets have responded (steal/swap actions clear TargetPlayerIds)
                    if (_pendingAction != null && _pendingAction.TargetPlayerIds.Count == 0)
                    {
                        _pendingAction = null;
                        _phase = GamePhase.Play;
                    }
                    // For payment actions the targets remain; the client will be prompted to pay.
                    return;
                }
                else
                {
                    // The action was successfully blocked
                    _pendingAction = null;
                    _phase = GamePhase.Play;
                    return;
                }
            }

            // Handle steal/swap actions — execute BEFORE removing from target list
            // (ExecuteSlyDeal etc. read TargetPlayerIds[0] to find the target)
            bool handled = false;
            switch (_pendingAction.Type)
            {
                case PendingActionType.RespondToSlyDeal:
                    ExecuteSlyDeal();
                    handled = true;
                    break;
                case PendingActionType.RespondToForceDeal:
                    ExecuteForceDeal();
                    handled = true;
                    break;
                case PendingActionType.RespondToDealBreaker:
                    ExecuteDealBreaker();
                    handled = true;
                    break;
            }

            // Handle payment (for rent/debt/birthday)
            if (!handled)
            {
                var payer = _players.FirstOrDefault(p => p.ConnectionId == connectionId);
                if (payer != null)
                {
                    var payableCards = payer.GetPayableCards();
                    int totalAssets = payableCards.Sum(c => c.MoneyValue);

                    if (totalAssets < _pendingAction.Amount)
                    {
                        // Insolvent: automatically take everything the player has
                        _lastPaymentError = null;
                        _lastPaymentErrorConnectionId = null;
                        if (payableCards.Count > 0)
                            ProcessPayment(connectionId, payableCards.Select(c => c.Id).ToList());
                        else
                            _pendingAction.TargetPlayerIds.Remove(connectionId);
                    }
                    else if (response.PaymentCardIds != null && response.PaymentCardIds.Count > 0)
                    {
                        // Solvent: validate that they're paying at least the required amount
                        var error = ValidatePayment(payer, response.PaymentCardIds, _pendingAction.Amount);
                        if (error != null)
                        {
                            // Invalid payment — store rejection reason (will be sent via state broadcast)
                            _lastPaymentError = error;
                            _lastPaymentErrorConnectionId = connectionId;
                            return; // Don't process, let client retry
                        }
                        _lastPaymentError = null;
                        _lastPaymentErrorConnectionId = null;
                        ProcessPayment(connectionId, response.PaymentCardIds);
                    }
                    else
                    {
                        // Solvent but no cards selected — reject
                        _lastPaymentError = "You must select cards to pay with.";
                        _lastPaymentErrorConnectionId = connectionId;
                        return;
                    }
                }
            }

            // Check if all targets have responded
            if (_pendingAction != null && _pendingAction.TargetPlayerIds.Count == 0)
            {
                _pendingAction = null;
                _phase = GamePhase.Play;

                // Check win after receiving properties
                var currentPlayer = GetCurrentPlayer();
                if (currentPlayer != null && currentPlayer.UniqueCompletedSetCount >= GameConfig.SetsToWin)
                {
                    _phase = GamePhase.GameOver;
                    _winnerId = currentPlayer.ConnectionId;
                }
            }
        }

        /// <summary>Clears house/hotel improvements from a set when it is broken up.</summary>
        private static void ClearImprovements(PropertySet set)
        {
            if (set.HasHouse || set.HasHotel)
            {
                set.HasHouse = false;
                set.HasHotel = false;
            }
        }

        private void ProcessPayment(string payerId, List<int> cardIds)
        {
            if (_pendingAction == null) return;

            var payer = _players.FirstOrDefault(p => p.ConnectionId == payerId);
            var receiver = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.SourcePlayerId);
            if (payer == null || receiver == null) return;

            int totalPaid = 0;
            var paidCards = new List<Card>();
            foreach (var cardId in cardIds)
            {
                // Check bank
                var card = payer.Bank.FirstOrDefault(c => c.Id == cardId);
                if (card != null)
                {
                    totalPaid += card.MoneyValue;
                    paidCards.Add(card);
                    payer.Bank.Remove(card);
                    receiver.Bank.Add(card);
                    continue;
                }

                // Check property sets
                foreach (var set in payer.PropertySets.ToList())
                {
                    card = set.Cards.FirstOrDefault(c => c.Id == cardId);
                    if (card != null)
                    {
                        totalPaid += card.MoneyValue;
                        paidCards.Add(card);
                        set.Cards.Remove(card);
                        // If this set had a house/hotel, discard them when breaking the set
                        ClearImprovements(set);
                        // Property goes to receiver's property area
                        var receiverColor = card.ActiveColor ?? card.Color ?? set.Color;
                        var receiverSet = receiver.GetOrCreatePropertySet(receiverColor);
                        receiverSet.Cards.Add(card);

                        // Clean up empty sets
                        if (set.Cards.Count == 0)
                            payer.PropertySets.Remove(set);
                        break;
                    }
                }
            }

            if (totalPaid > 0)
                LogAction(payer.Name, $"paid M{totalPaid} to {receiver.Name}",
                    targetPlayerName: receiver.Name,
                    targetCards: paidCards);

            _pendingAction.TargetPlayerIds.Remove(payerId);
        }

        private void ExecuteSlyDeal()
        {
            if (_pendingAction == null) return;

            var source = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.SourcePlayerId);
            var target = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.TargetPlayerIds[0]);
            if (source == null || target == null || _pendingAction.TargetCardId == null) return;

            Card? stolenCard = null;
            foreach (var set in target.PropertySets.ToList())
            {
                var card = set.Cards.FirstOrDefault(c => c.Id == _pendingAction.TargetCardId);
                if (card != null)
                {
                    stolenCard = card;
                    set.Cards.Remove(card);
                    // Discard house/hotel when the set is broken
                    ClearImprovements(set);
                    var color = card.ActiveColor ?? card.Color ?? set.Color;
                    PlayProperty(source, card, color);
                    if (set.Cards.Count == 0) target.PropertySets.Remove(set);
                    break;
                }
            }

            LogAction(source.Name, $"stole {_pendingAction.TargetCardName ?? "a card"} from {target.Name}",
                targetPlayerName: target.Name,
                targetCards: stolenCard != null ? new List<Card> { stolenCard } : null);
            _pendingAction.TargetPlayerIds.Clear();
        }

        private void ExecuteForceDeal()
        {
            if (_pendingAction == null) return;

            var source = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.SourcePlayerId);
            var target = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.TargetPlayerIds[0]);
            if (source == null || target == null) return;

            Card? stolenCard = null;
            Card? offeredCard = null;

            // Remove target card from target
            foreach (var set in target.PropertySets.ToList())
            {
                stolenCard = set.Cards.FirstOrDefault(c => c.Id == _pendingAction.TargetCardId);
                if (stolenCard != null)
                {
                    set.Cards.Remove(stolenCard);
                    // Discard house/hotel when the set is broken
                    ClearImprovements(set);
                    if (set.Cards.Count == 0) target.PropertySets.Remove(set);
                    break;
                }
            }

            // Remove offered card from source
            foreach (var set in source.PropertySets.ToList())
            {
                offeredCard = set.Cards.FirstOrDefault(c => c.Id == _pendingAction.OfferedCardId);
                if (offeredCard != null)
                {
                    set.Cards.Remove(offeredCard);
                    // Discard house/hotel when the set is broken
                    ClearImprovements(set);
                    if (set.Cards.Count == 0) source.PropertySets.Remove(set);
                    break;
                }
            }

            // Swap
            if (stolenCard != null)
            {
                var color = stolenCard.ActiveColor ?? stolenCard.Color;
                if (color.HasValue) PlayProperty(source, stolenCard, color.Value);
            }
            if (offeredCard != null)
            {
                var color = offeredCard.ActiveColor ?? offeredCard.Color;
                if (color.HasValue) PlayProperty(target, offeredCard, color.Value);
            }

            LogAction(source.Name,
                $"force-swapped with {target.Name}",
                targetPlayerName: target.Name,
                sourceCards: offeredCard != null ? new List<Card> { offeredCard } : null,
                targetCards: stolenCard != null ? new List<Card> { stolenCard } : null);
            _pendingAction.TargetPlayerIds.Clear();
        }

        private void ExecuteDealBreaker()
        {
            if (_pendingAction == null) return;

            var source = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.SourcePlayerId);
            var target = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.TargetPlayerIds[0]);
            if (source == null || target == null || _pendingAction.TargetSetColor == null) return;

            var targetSet = target.PropertySets.FirstOrDefault(s => s.Color == _pendingAction.TargetSetColor && s.IsComplete);
            if (targetSet == null) return;

            target.PropertySets.Remove(targetSet);

            // Transfer entire set including house/hotel
            var newSet = source.GetOrCreatePropertySet(targetSet.Color);
            newSet.Cards.AddRange(targetSet.Cards);
            newSet.HasHouse = targetSet.HasHouse;
            newSet.HasHotel = targetSet.HasHotel;

            LogAction(source.Name, $"took {target.Name}'s complete {targetSet.Color} set!",
                targetPlayerName: target.Name,
                targetCards: targetSet.Cards.ToList());
            _pendingAction.TargetPlayerIds.Clear();
        }

        #endregion

        #region Helpers

        private void LogAction(string playerName, string text,
            Card? cardPlayed = null, string? targetPlayerName = null,
            List<Card>? sourceCards = null, List<Card>? targetCards = null)
        {
            _recentActions.Add(new GameAction
            {
                Id = _nextActionId++,
                PlayerName = playerName,
                Text = text,
                CardPlayed = cardPlayed,
                TargetPlayerName = targetPlayerName,
                SourceCards = sourceCards,
                TargetCards = targetCards,
            });
            if (_recentActions.Count > MaxRecentActions)
                _recentActions.RemoveAt(0);
        }

        private string DescribeCardPlay(Player player, Card card, PlayCardRequest request)
        {
            if (request.PlayAsMoney)
                return card.CardType == CardType.Money
                    ? $"banked M{card.MoneyValue}"
                    : $"banked {card.Name}";

            switch (card.CardType)
            {
                case CardType.Money:
                    return $"banked M{card.MoneyValue}";
                case CardType.Property:
                    return $"placed {card.Name}";
                case CardType.PropertyWildcard:
                    return $"placed {card.Name}";
                case CardType.Rent:
                {
                    var color = request.RentColor?.ToString() ?? "?";
                    return $"charged {color} Rent";
                }
                case CardType.Action:
                    return DescribeAction(card, request);
                default:
                    return $"played {card.Name}";
            }
        }

        private string DescribeAction(Card card, PlayCardRequest request)
        {
            switch (card.ActionKind)
            {
                case ActionType.PassGo:
                    return "played Pass Go";
                case ActionType.DebtCollector:
                {
                    var target = _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId);
                    return $"played Debt Collector on {target?.Name ?? "a player"}";
                }
                case ActionType.ItsMyBirthday:
                    return "played It's My Birthday";
                case ActionType.SlyDeal:
                {
                    var target = _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId);
                    return $"played Sly Deal on {target?.Name ?? "a player"}";
                }
                case ActionType.ForceDeal:
                {
                    var target = _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId);
                    return $"played Force Deal with {target?.Name ?? "a player"}";
                }
                case ActionType.DealBreaker:
                {
                    var target = _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId);
                    var color = request.TargetSetColor?.ToString() ?? "?";
                    return $"played Deal Breaker on {target?.Name ?? "a player"}'s {color} set";
                }
                case ActionType.JustSayNo:
                    return "played Just Say No!";
                case ActionType.House:
                    return $"added House to {request.TargetSetColor} set";
                case ActionType.Hotel:
                    return $"added Hotel to {request.TargetSetColor} set";
                default:
                    return $"played {card.Name}";
            }
        }

        private void AdvanceTurn()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
            _playsUsed = 0;
            _pendingAction = null;
            _phase = GamePhase.Draw;

            // If next player is a bot, auto-play their entire turn
            var next = GetCurrentPlayer();
            if (next != null && BotAI.IsBot(next.ConnectionId))
            {
                PlayBotTurn(next);
            }
        }

        private void PlayBotTurn(Player bot)
        {
            // Draw
            int drawCount = bot.Hand.Count == 0 ? GameConfig.DrawWhenEmpty : GameConfig.DrawPerTurn;
            bot.Hand.AddRange(_deck.Draw(drawCount));
            _phase = GamePhase.Play;

            // Play cards
            BotAI.PlayTurn(bot, _players, _deck, (player, card, request) =>
            {
                player.Hand.Remove(card);
                ProcessCardPlay(player, card, request);
                _playsUsed++;

                LogAction(player.Name, DescribeCardPlay(player, card, request),
                    cardPlayed: card,
                    targetPlayerName: request.TargetPlayerId != null
                        ? _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId)?.Name
                        : null);

                // If this created a pending action targeting other bots, resolve it
                ResolveBotPendingActions();
            }, GameConfig.MaxPlaysPerTurn);

            // Discard if needed
            if (bot.Hand.Count > GameConfig.MaxHandSize)
            {
                var discards = BotAI.PickDiscards(bot, GameConfig.MaxHandSize);
                foreach (var cardId in discards)
                {
                    var card = bot.Hand.FirstOrDefault(c => c.Id == cardId);
                    if (card != null)
                    {
                        bot.Hand.Remove(card);
                        _deck.Discard(card);
                    }
                }
            }

            // Check win
            if (bot.UniqueCompletedSetCount >= GameConfig.SetsToWin)
            {
                _phase = GamePhase.GameOver;
                _winnerId = bot.ConnectionId;
                return;
            }

            // Advance to next player
            AdvanceTurn();
        }

        /// <summary>
        /// If there's a pending action and all remaining targets are bots, auto-respond for them.
        /// </summary>
        private void ResolveBotPendingActions()
        {
            if (_pendingAction == null) return;

            // Process bot responses until only human targets remain (or none)
            var botTargets = _pendingAction.TargetPlayerIds
                .Where(BotAI.IsBot)
                .ToList();

            foreach (var botId in botTargets)
            {
                if (_pendingAction == null) break;
                var bot = _players.FirstOrDefault(p => p.ConnectionId == botId);
                if (bot == null) continue;

                var response = BotAI.BuildResponse(bot);
                ProcessResponse(botId, response);
            }
        }

        private Player? GetCurrentPlayer()
        {
            if (_currentPlayerIndex < 0 || _currentPlayerIndex >= _players.Count)
                return null;
            return _players[_currentPlayerIndex];
        }

        private GameState BuildGameState(string? forConnectionId = null)
        {
            var state = new GameState
            {
                Phase = _phase,
                GameCode = GameCode,
                CurrentPlayerIndex = _currentPlayerIndex,
                PlaysUsed = _playsUsed,
                DrawPileCount = _deck.DrawPileCount,
                DiscardPileCount = _deck.DiscardPileCount,
                TopDiscard = _deck.TopDiscard,
                PendingAction = _pendingAction,
                WinnerId = _winnerId,
                WinnerName = _winnerId != null ? _players.FirstOrDefault(p => p.ConnectionId == _winnerId)?.Name : null,
                PaymentError = forConnectionId == _lastPaymentErrorConnectionId ? _lastPaymentError : null,
                RecentActions = _recentActions.ToList(),
            };

            foreach (var player in _players)
            {
                var ps = new PlayerState
                {
                    PlayerId = player.PlayerId,
                    ConnectionId = player.ConnectionId,
                    Name = player.Name,
                    HandCount = player.Hand.Count,
                    Bank = player.Bank.ToList(),
                    UnboundWilds = player.UnboundWilds.ToList(),
                    CompletedSetCount = player.CompletedSetCount,
                    UniqueCompletedSetCount = player.UniqueCompletedSetCount,
                };

                // Only include hand for the requesting player
                if (forConnectionId == player.ConnectionId)
                {
                    ps.Hand = player.Hand.Select(card => CloneCardForViewer(card, player)).ToList();
                }

                foreach (var set in player.PropertySets)
                {
                    ps.PropertySets.Add(new PropertySetState
                    {
                        SetId = set.SetId,
                        Color = set.Color,
                        Cards = set.Cards.ToList(),
                        IsComplete = set.IsComplete,
                        HasHouse = set.HasHouse,
                        HasHotel = set.HasHotel,
                        Rent = set.CalculateRent(),
                        RequiredSize = set.RequiredSize,
                    });
                }

                state.Players.Add(ps);
            }

            return state;
        }

        private Card CloneCardForViewer(Card card, Player player)
        {
            return new Card
            {
                Id = card.Id,
                CardId = card.CardId,
                CardType = card.CardType,
                MoneyValue = card.MoneyValue,
                Name = card.Name,
                Color = card.Color,
                AltColor = card.AltColor,
                IsMulticolorWild = card.IsMulticolorWild,
                RentColors = card.RentColors?.ToList(),
                IsWildRent = card.IsWildRent,
                ActionKind = card.ActionKind,
                ActiveColor = card.ActiveColor,
                IsPlayable = ComputeCardPlayability(card, player),
            };
        }

        private bool ComputeCardPlayability(Card card, Player player)
        {
            var typedCard = CardFactory.Create(card);
            return typedCard.IsPlayable(new CardPlayabilityContext
            {
                Player = player,
                Players = _players
            });
        }

        #endregion

        #region Broadcasting

        public async Task BroadcastGameStateAsync()
        {
            List<Player> playersCopy;
            lock (_lock)
            {
                playersCopy = _players.ToList();
            }

            // Send personalized state to each player (with their own hand)
            foreach (var player in playersCopy)
            {
                GameState state;
                lock (_lock)
                {
                    state = BuildGameState(player.ConnectionId);
                }
                await _hubContext.Clients.Client(player.ConnectionId)
                    .SendAsync("gameStateUpdated", state);
            }
        }

        #endregion

        #region Property Management

        /// <summary>
        /// Move any property card to a specific set, a new set, or unbound.
        /// targetSetId > 0: move to existing set by ID
        /// targetSetId = 0 with targetColor set: create new set of that color
        /// targetSetId = -1: move to unbound (multi-color wilds only)
        /// </summary>
        public async Task MovePropertyAsync(string connectionId, int cardId, int targetSetId, PropertyColor? targetColor)
        {
            bool playerFound;
            lock (_lock)
            {
                var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId);
                playerFound = player != null;
                if (playerFound)
                    TryMoveProperty(player!, cardId, targetSetId, targetColor);
            }
            // Always broadcast when player exists so the client receives the authoritative state,
            // even if the move was rejected (e.g. wrong turn or wrong phase).
            if (playerFound) await BroadcastGameStateAsync();
        }

        private void TryMoveProperty(Player player, int cardId, int targetSetId, PropertyColor? targetColor)
        {
            var currentPlayer = GetCurrentPlayer();
            if (currentPlayer == null || currentPlayer.ConnectionId != player.ConnectionId) return;
            if (_phase != GamePhase.Play) return;

            // Find and remove the card from wherever it is
            Card? card = null;

            card = player.UnboundWilds.FirstOrDefault(c => c.Id == cardId);
            if (card != null)
            {
                player.UnboundWilds.Remove(card);
            }
            else
            {
                foreach (var set in player.PropertySets.ToList())
                {
                    card = set.Cards.FirstOrDefault(c => c.Id == cardId);
                    if (card != null)
                    {
                        set.Cards.Remove(card);
                        if (!set.IsComplete)
                        {
                            set.HasHouse = false;
                            set.HasHotel = false;
                        }
                        if (set.Cards.Count == 0)
                            player.PropertySets.Remove(set);
                        break;
                    }
                }
            }

            if (card == null) return;

            // Move to unbound
            if (targetSetId == -1)
            {
                if (!(card.CardType == CardType.PropertyWildcard && card.IsMulticolorWild)) return;
                card.ActiveColor = null;
                player.UnboundWilds.Add(card);
                return;
            }

            // Determine the color for validation
            var color = targetColor;
            if (targetSetId > 0)
            {
                var existingSet = player.PropertySets.FirstOrDefault(s => s.SetId == targetSetId);
                if (existingSet == null) return;
                color = existingSet.Color;
            }

            if (color == null) return;

            // Validate the card can go to this color
            if (card.CardType == CardType.Property && card.Color != color) return;
            if (card.CardType == CardType.PropertyWildcard && !card.IsMulticolorWild
                && card.Color != color && card.AltColor != color) return;

            // Check set size limit
            if (targetSetId > 0)
            {
                var existingSet = player.PropertySets.First(s => s.SetId == targetSetId);
                if (existingSet.Cards.Count >= existingSet.RequiredSize) return;
                card.ActiveColor = color;
                existingSet.Cards.Add(card);
            }
            else
            {
                // Create new set
                card.ActiveColor = color;
                var newSet = new PropertySet { Color = color.Value };
                newSet.Cards.Add(card);
                player.PropertySets.Add(newSet);
            }
        }

        /// <summary>
        /// Flip a dual-color wildcard to its other color.
        /// Only allowed during the owning player's turn.
        /// </summary>
        public async Task FlipWildcardAsync(string connectionId, int cardId)
        {
            lock (_lock)
            {
                var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId);
                if (player == null) return;

                // Can only rearrange during your turn
                var currentPlayer = GetCurrentPlayer();
                if (currentPlayer == null || currentPlayer.ConnectionId != connectionId) return;
                if (_phase != GamePhase.Play) return;

                // Find the card in property sets
                foreach (var set in player.PropertySets)
                {
                    var card = set.Cards.FirstOrDefault(c => c.Id == cardId);
                    if (card != null && card.CardType == CardType.PropertyWildcard && !card.IsMulticolorWild)
                    {
                        // Flip to other color
                        var newColor = card.ActiveColor == card.Color ? card.AltColor : card.Color;
                        if (newColor == null) return;

                        // Remove from current set
                        set.Cards.Remove(card);
                        if (set.Cards.Count == 0)
                            player.PropertySets.Remove(set);

                        // Update active color and add to new set
                        card.ActiveColor = newColor;
                        var newSet = player.GetOrCreatePropertySet(newColor.Value);
                        newSet.Cards.Add(card);
                        break;
                    }
                }
            }
            await BroadcastGameStateAsync();
        }

        #endregion

        #region Debug

        /// <summary>
        /// Returns all cards in the draw pile, discard pile, and each player's hand.
        /// Only for debug mode.
        /// </summary>
        public DebugDeckInfo GetDebugDeckInfo()
        {
            lock (_lock)
            {
                var info = new DebugDeckInfo
                {
                    DrawPile = _deck.GetDrawPileSnapshot(),
                    DiscardPile = _deck.GetDiscardPileSnapshot(),
                };

                foreach (var player in _players)
                {
                    info.PlayerHands.Add(new DebugPlayerHand
                    {
                        PlayerName = player.Name,
                        Cards = player.Hand.ToList(),
                    });
                }

                return info;
            }
        }

        #endregion

        #region Test Helpers (internal)

        /// <summary>Get player by connectionId. Internal for testing.</summary>
        internal Player? GetPlayer(string connectionId)
        {
            lock (_lock) { return _players.FirstOrDefault(p => p.ConnectionId == connectionId); }
        }

        /// <summary>Get internal deck for test manipulation.</summary>
        internal Deck GetDeck() => _deck;

        /// <summary>Get current phase.</summary>
        internal GamePhase Phase { get { lock (_lock) { return _phase; } } }

        /// <summary>Get plays used this turn.</summary>
        internal int PlaysUsed { get { lock (_lock) { return _playsUsed; } } }

        /// <summary>Get current pending action.</summary>
        internal PendingAction? PendingAction { get { lock (_lock) { return _pendingAction; } } }

        #endregion
    }

    public class DebugDeckInfo
    {
        public List<Card> DrawPile { get; set; } = new();
        public List<Card> DiscardPile { get; set; } = new();
        public List<DebugPlayerHand> PlayerHands { get; set; } = new();
    }

    public class DebugPlayerHand
    {
        public string PlayerName { get; set; } = "";
        public List<Card> Cards { get; set; } = new();
    }

    /// <summary>
    /// Request data for playing a card.
    /// </summary>
    public class PlayCardRequest
    {
        public bool PlayAsMoney { get; set; }
        public PropertyColor? WildcardColor { get; set; }
        public PropertyColor? RentColor { get; set; }
        public string? TargetPlayerId { get; set; }
        public int? TargetCardId { get; set; }
        public int? OfferedCardId { get; set; }
        public PropertyColor? TargetSetColor { get; set; }
        public List<int>? DoubleRentCardIds { get; set; }
    }

    /// <summary>
    /// Response data from a player being targeted by an action.
    /// </summary>
    public class ActionResponse
    {
        public bool PlayJustSayNo { get; set; }
        public List<int>? PaymentCardIds { get; set; }
    }
}
