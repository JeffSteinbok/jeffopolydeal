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

        public async Task ConnectPlayerAsync(string connectionId, string playerName)
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
                // Don't remove from _players list during active game — they may reconnect
                if (_phase == GamePhase.Lobby)
                {
                    _players.RemoveAll(p => p.ConnectionId == connectionId);
                }
            }
            await BroadcastGameStateAsync();
        }

        public async Task ReconnectPlayerAsync(string oldConnectionId, string newConnectionId, string playerName)
        {
            lock (_lock)
            {
                _connections.Remove(oldConnectionId);
                _connections[newConnectionId] = true;

                var player = _players.FirstOrDefault(p => p.ConnectionId == oldConnectionId);
                if (player != null)
                {
                    player.ConnectionId = newConnectionId;
                }
                else if (_phase == GamePhase.Lobby)
                {
                    _players.Add(new Player { ConnectionId = newConnectionId, Name = playerName });
                }
            }

            await _hubContext.Groups.AddToGroupAsync(newConnectionId, GameCode);
            await BroadcastGameStateAsync();
        }

        #endregion

        #region Game Flow

        public async Task StartGameAsync(bool allowSinglePlayer = false)
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
            }
            await BroadcastGameStateAsync();
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
                    return;
                }

                AdvanceTurn();
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
                    if (_playsUsed >= GameConfig.MaxPlaysPerTurn) break;
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
                        if (!stealable.Any(c => c.Id == request.TargetCardId)) return false;

                        _deck.Discard(card);
                        _pendingAction = new PendingAction
                        {
                            Type = PendingActionType.RespondToSlyDeal,
                            SourcePlayerId = player.ConnectionId,
                            TargetPlayerIds = new List<string> { request.TargetPlayerId },
                            TargetCardId = request.TargetCardId,
                        };
                    }
                    return true;

                case ActionType.ForceDeal:
                    if (request.TargetPlayerId == null || request.TargetCardId == null || request.OfferedCardId == null) return false;
                    {
                        var target = _players.FirstOrDefault(p => p.ConnectionId == request.TargetPlayerId);
                        if (target == null) return false;
                        var stealable = target.GetStealableProperties();
                        if (!stealable.Any(c => c.Id == request.TargetCardId)) return false;
                        var offered = player.GetStealableProperties();
                        if (!offered.Any(c => c.Id == request.OfferedCardId)) return false;

                        _deck.Discard(card);
                        _pendingAction = new PendingAction
                        {
                            Type = PendingActionType.RespondToForceDeal,
                            SourcePlayerId = player.ConnectionId,
                            TargetPlayerIds = new List<string> { request.TargetPlayerId },
                            TargetCardId = request.TargetCardId,
                            OfferedCardId = request.OfferedCardId,
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
                // The action was successfully blocked
                _pendingAction = null;
                _phase = GamePhase.Play;
                return;
            }

            // Handle payment
            if (response.PaymentCardIds != null && response.PaymentCardIds.Count > 0)
            {
                ProcessPayment(connectionId, response.PaymentCardIds);
            }
            else
            {
                // Player has nothing to pay with, auto-complete
                _pendingAction.TargetPlayerIds.Remove(connectionId);
            }

            // Handle steal/swap completion (when no Just Say No)
            switch (_pendingAction?.Type)
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

        private void ProcessPayment(string payerId, List<int> cardIds)
        {
            if (_pendingAction == null) return;

            var payer = _players.FirstOrDefault(p => p.ConnectionId == payerId);
            var receiver = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.SourcePlayerId);
            if (payer == null || receiver == null) return;

            foreach (var cardId in cardIds)
            {
                // Check bank
                var card = payer.Bank.FirstOrDefault(c => c.Id == cardId);
                if (card != null)
                {
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
                        set.Cards.Remove(card);
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

            _pendingAction.TargetPlayerIds.Remove(payerId);
        }

        private void ExecuteSlyDeal()
        {
            if (_pendingAction == null) return;

            var source = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.SourcePlayerId);
            var target = _players.FirstOrDefault(p => p.ConnectionId == _pendingAction.TargetPlayerIds[0]);
            if (source == null || target == null || _pendingAction.TargetCardId == null) return;

            foreach (var set in target.PropertySets.ToList())
            {
                var card = set.Cards.FirstOrDefault(c => c.Id == _pendingAction.TargetCardId);
                if (card != null)
                {
                    set.Cards.Remove(card);
                    var color = card.ActiveColor ?? card.Color ?? set.Color;
                    PlayProperty(source, card, color);
                    if (set.Cards.Count == 0) target.PropertySets.Remove(set);
                    break;
                }
            }

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

            _pendingAction.TargetPlayerIds.Clear();
        }

        #endregion

        #region Helpers

        private void AdvanceTurn()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
            _playsUsed = 0;
            _pendingAction = null;
            _phase = GamePhase.Draw;
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
            };

            foreach (var player in _players)
            {
                var ps = new PlayerState
                {
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
                    ps.Hand = player.Hand.ToList();
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
            lock (_lock)
            {
                var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId);
                if (player == null) return;

                var currentPlayer = GetCurrentPlayer();
                if (currentPlayer == null || currentPlayer.ConnectionId != connectionId) return;
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
            await BroadcastGameStateAsync();
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
