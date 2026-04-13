using JeffopolyDeal;
using JeffopolyDeal.Hubs;
using JeffopolyDeal.Models;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Test harness that wraps Game with fake SignalR plumbing so tests can
    /// drive the game engine directly without a running server.
    /// 
    /// Captures all state broadcasts so tests can inspect what each player sees.
    /// </summary>
    public class TestGameHarness
    {
        private readonly Game _game;
        private readonly ConcurrentDictionary<string, GameState> _lastStates = new();

        public Game Game => _game;

        /// <summary>Returns the last GameState broadcast for a given connectionId.</summary>
        public GameState? GetState(string connectionId) =>
            _lastStates.TryGetValue(connectionId, out var s) ? s : null;

        /// <summary>Returns all captured states.</summary>
        public IReadOnlyDictionary<string, GameState> AllStates => _lastStates;

        public TestGameHarness(string gameCode = "TEST")
        {
            var hubContext = CreateMockHubContext();
            _game = new Game(hubContext, gameCode);
        }

        /// <summary>Add a player and return their connectionId.</summary>
        public async Task<string> AddPlayerAsync(string name)
        {
            var connectionId = $"conn-{name}";
            await _game.ConnectPlayerAsync(connectionId, name);
            return connectionId;
        }

        /// <summary>Set up a standard 2-player game and start it.</summary>
        public async Task<(string p1, string p2)> SetupTwoPlayerGameAsync(
            string p1Name = "Alice", string p2Name = "Bob")
        {
            var p1 = await AddPlayerAsync(p1Name);
            var p2 = await AddPlayerAsync(p2Name);
            await _game.StartGameAsync(allowSinglePlayer: false);
            return (p1, p2);
        }

        /// <summary>Set up a 3-player game and start it.</summary>
        public async Task<(string p1, string p2, string p3)> SetupThreePlayerGameAsync(
            string p1Name = "Alice", string p2Name = "Bob", string p3Name = "Charlie")
        {
            var p1 = await AddPlayerAsync(p1Name);
            var p2 = await AddPlayerAsync(p2Name);
            var p3 = await AddPlayerAsync(p3Name);
            await _game.StartGameAsync(allowSinglePlayer: false);
            return (p1, p2, p3);
        }

        /// <summary>Draw cards for the current player.</summary>
        public async Task DrawAsync(string connectionId) =>
            await _game.DrawCardsAsync(connectionId);

        /// <summary>Play a card with the given request.</summary>
        public async Task PlayCardAsync(string connectionId, int cardId, PlayCardRequest? request = null) =>
            await _game.PlayCardAsync(connectionId, cardId, request ?? new PlayCardRequest());

        /// <summary>Play a card as money.</summary>
        public async Task PlayAsMoney(string connectionId, int cardId) =>
            await _game.PlayCardAsync(connectionId, cardId, new PlayCardRequest { PlayAsMoney = true });

        /// <summary>End turn for the current player.</summary>
        public async Task EndTurnAsync(string connectionId) =>
            await _game.EndTurnAsync(connectionId);

        /// <summary>Discard a card.</summary>
        public async Task DiscardAsync(string connectionId, int cardId) =>
            await _game.DiscardCardAsync(connectionId, cardId);

        /// <summary>Respond to an action.</summary>
        public async Task RespondAsync(string connectionId, ActionResponse response) =>
            await _game.RespondToActionAsync(connectionId, response);

        /// <summary>Get a player's hand from the last broadcast state.</summary>
        public List<Card> GetHand(string connectionId)
        {
            var state = GetState(connectionId);
            var playerState = state?.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            return playerState?.Hand ?? new List<Card>();
        }

        /// <summary>Get the phase from the last broadcast.</summary>
        public GamePhase GetPhase(string connectionId)
        {
            var state = GetState(connectionId);
            return state?.Phase ?? GamePhase.Lobby;
        }

        /// <summary>Get the current player index.</summary>
        public int GetCurrentPlayerIndex(string connectionId)
        {
            var state = GetState(connectionId);
            return state?.CurrentPlayerIndex ?? -1;
        }

        /// <summary>Get the number of plays used this turn.</summary>
        public int GetPlaysUsed(string connectionId)
        {
            var state = GetState(connectionId);
            return state?.PlaysUsed ?? 0;
        }

        /// <summary>Get a specific player's state from the broadcast perspective of viewerConnectionId.</summary>
        public PlayerState? GetPlayerState(string viewerConnectionId, string targetConnectionId)
        {
            var state = GetState(viewerConnectionId);
            return state?.Players.FirstOrDefault(p => p.ConnectionId == targetConnectionId);
        }

        /// <summary>Get the pending action.</summary>
        public PendingAction? GetPendingAction(string connectionId)
        {
            var state = GetState(connectionId);
            return state?.PendingAction;
        }

        /// <summary>Find a card of a specific type in a player's hand.</summary>
        public Card? FindCardInHand(string connectionId, CardType type, ActionType? actionKind = null)
        {
            var hand = GetHand(connectionId);
            return hand.FirstOrDefault(c => c.CardType == type &&
                (actionKind == null || c.ActionKind == actionKind));
        }

        /// <summary>Find a card of specific card type and color in hand.</summary>
        public Card? FindPropertyInHand(string connectionId, PropertyColor? color = null)
        {
            var hand = GetHand(connectionId);
            return hand.FirstOrDefault(c => c.CardType == CardType.Property &&
                (color == null || c.Color == color));
        }

        /// <summary>Find a money card in hand.</summary>
        public Card? FindMoneyInHand(string connectionId, int? value = null)
        {
            var hand = GetHand(connectionId);
            return hand.FirstOrDefault(c => c.CardType == CardType.Money &&
                (value == null || c.MoneyValue == value));
        }

        /// <summary>Find a rent card in hand.</summary>
        public Card? FindRentInHand(string connectionId, PropertyColor? color = null)
        {
            var hand = GetHand(connectionId);
            return hand.FirstOrDefault(c => c.CardType == CardType.Rent &&
                (color == null || (c.RentColors != null && c.RentColors.Contains(color.Value))));
        }

        /// <summary>Complete a full turn: draw, skip plays, end turn.</summary>
        public async Task SkipTurnAsync(string connectionId)
        {
            await DrawAsync(connectionId);
            await EndTurnAsync(connectionId);
        }

        /// <summary>Inject a card directly into a player's hand for deterministic testing.</summary>
        public Card InjectCardIntoHand(string connectionId, CardType type, int moneyValue = 0,
            string name = "Test Card", PropertyColor? color = null, PropertyColor? altColor = null,
            ActionType? actionKind = null, List<PropertyColor>? rentColors = null,
            bool isWildRent = false, bool isMulticolorWild = false)
        {
            var player = _game.GetPlayer(connectionId);
            if (player == null) throw new InvalidOperationException($"Player {connectionId} not found");

            var card = _game.GetDeck().CreateCard(type, moneyValue, name, color, altColor,
                actionKind, rentColors, isWildRent, isMulticolorWild);
            player.Hand.Add(card);
            return card;
        }

        /// <summary>Inject a money card into a player's hand.</summary>
        public Card InjectMoney(string connectionId, int value = 1)
            => InjectCardIntoHand(connectionId, CardType.Money, value, $"{value}M");

        /// <summary>Inject a property card into a player's hand.</summary>
        public Card InjectProperty(string connectionId, PropertyColor color, string name = "Test Property")
            => InjectCardIntoHand(connectionId, CardType.Property, 0, name, color: color);

        /// <summary>Inject an action card into a player's hand.</summary>
        public Card InjectAction(string connectionId, ActionType action, int moneyValue = 0, string? name = null)
            => InjectCardIntoHand(connectionId, CardType.Action, moneyValue, name ?? action.ToString(), actionKind: action);

        /// <summary>Inject a rent card into a player's hand.</summary>
        public Card InjectRent(string connectionId, PropertyColor color1, PropertyColor color2, int moneyValue = 1)
            => InjectCardIntoHand(connectionId, CardType.Rent, moneyValue, $"{color1}/{color2} Rent",
                rentColors: new List<PropertyColor> { color1, color2 });

        /// <summary>Inject a wild rent card into a player's hand.</summary>
        public Card InjectWildRent(string connectionId, int moneyValue = 3)
            => InjectCardIntoHand(connectionId, CardType.Rent, moneyValue, "Wild Rent", isWildRent: true);

        /// <summary>Inject a Just Say No card into a player's hand.</summary>
        public Card InjectJustSayNo(string connectionId)
            => InjectAction(connectionId, ActionType.JustSayNo, 4, "Just Say No");

        /// <summary>Inject a Double the Rent card into a player's hand.</summary>
        public Card InjectDoubleTheRent(string connectionId)
            => InjectAction(connectionId, ActionType.DoubleTheRent, 1, "Double the Rent");

        /// <summary>Inject a property wildcard into a player's hand.</summary>
        public Card InjectPropertyWildcard(string connectionId, PropertyColor color1, PropertyColor color2, int moneyValue = 0)
            => InjectCardIntoHand(connectionId, CardType.PropertyWildcard, moneyValue,
                $"{color1}/{color2} Wildcard", color: color1, altColor: color2);

        /// <summary>Inject a multi-color wildcard into a player's hand.</summary>
        public Card InjectMulticolorWild(string connectionId)
            => InjectCardIntoHand(connectionId, CardType.PropertyWildcard, 0,
                "Multi-color Wildcard", isMulticolorWild: true);

        /// <summary>
        /// Place property cards directly on a player's board (bypasses hand/play flow).
        /// Returns the set they were added to.
        /// </summary>
        public PropertySet PlacePropertyOnBoard(string connectionId, PropertyColor color, int count = 1)
        {
            var player = _game.GetPlayer(connectionId)!;
            var set = player.GetOrCreatePropertySet(color);
            for (int i = 0; i < count; i++)
            {
                var card = _game.GetDeck().CreateCard(CardType.Property, 0, $"{color} #{i+1}", color: color);
                card.ActiveColor = color;
                set.Cards.Add(card);
            }
            return set;
        }

        /// <summary>Place a complete property set on a player's board.</summary>
        public PropertySet PlaceCompleteSet(string connectionId, PropertyColor color)
        {
            return PlacePropertyOnBoard(connectionId, color, GameConfig.SetSize[color]);
        }

        /// <summary>Place money directly into a player's bank (bypasses hand/play flow).</summary>
        public Card PlaceMoneyInBank(string connectionId, int value = 1)
        {
            var player = _game.GetPlayer(connectionId)!;
            var card = _game.GetDeck().CreateCard(CardType.Money, value, $"{value}M");
            player.Bank.Add(card);
            return card;
        }

        private IHubContext<GameHub> CreateMockHubContext()
        {
            var hubContext = Substitute.For<IHubContext<GameHub>>();
            var clients = Substitute.For<IHubClients>();
            var groups = Substitute.For<IGroupManager>();

            hubContext.Clients.Returns(clients);
            hubContext.Groups.Returns(groups);

            // Capture SendAsync calls to record game state per player
            clients.Client(Arg.Any<string>()).Returns(callInfo =>
            {
                var connId = callInfo.Arg<string>();
                var proxy = Substitute.For<ISingleClientProxy>();
                proxy.SendCoreAsync(
                    Arg.Any<string>(),
                    Arg.Any<object?[]>(),
                    Arg.Any<CancellationToken>()
                ).Returns(callInfo2 =>
                {
                    var method = callInfo2.Arg<string>();
                    var args = callInfo2.Arg<object?[]>();
                    if (method == "gameStateUpdated" && args.Length > 0 && args[0] is GameState state)
                    {
                        _lastStates[connId] = state;
                    }
                    return Task.CompletedTask;
                });
                return proxy;
            });

            groups.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            groups.RemoveFromGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return hubContext;
        }
    }
}
