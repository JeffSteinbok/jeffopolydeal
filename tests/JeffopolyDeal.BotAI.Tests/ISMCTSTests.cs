using JeffopolyDeal;
using JeffopolyDeal.ISMCTS;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    // =========================================================================
    // ISMCTSTests.cs — Tests for the ISMCTS engine and its components
    // =========================================================================
    //
    // These tests verify the correctness of:
    //   - SimulationState creation and cloning
    //   - Determinizer card pool math
    //   - MoveGenerator legal move enumeration
    //   - GameSimulator card processing
    //   - RolloutPolicy response logic
    //   - ISMCTSEngine integration (produces reasonable moves)
    //
    // Tests use controlled game states with known cards rather than random
    // decks to ensure deterministic, reproducible results.
    // =========================================================================

    public class ISMCTSTests
    {
        // =====================================================================
        // Helper methods — create test game objects
        // =====================================================================

        /// <summary>Create a player with a given connection ID.</summary>
        private static Player CreatePlayer(string connectionId) => new Player
        {
            PlayerId = connectionId,
            ConnectionId = connectionId,
            Name = connectionId,
        };

        /// <summary>Create a simple money card.</summary>
        private static Card CreateMoney(int id, int value) => new Card
        {
            Id = id,
            CardId = $"money{id}",
            CardType = CardType.Money,
            MoneyValue = value,
            Name = $"${value}M",
        };

        /// <summary>Create a property card.</summary>
        private static Card CreateProperty(int id, PropertyColor color, int value = 1) => new Card
        {
            Id = id,
            CardId = $"prop{id}",
            CardType = CardType.Property,
            MoneyValue = value,
            Color = color,
            ActiveColor = color,
            Name = $"{color} Property",
        };

        /// <summary>Create a rent card.</summary>
        private static Card CreateRent(int id, params PropertyColor[] colors) => new Card
        {
            Id = id,
            CardId = $"rent{id}",
            CardType = CardType.Rent,
            MoneyValue = 1,
            RentColors = colors.ToList(),
            Name = "Rent",
        };

        /// <summary>Create an action card.</summary>
        private static Card CreateAction(int id, ActionType kind, int value = 0) => new Card
        {
            Id = id,
            CardId = $"action{id}",
            CardType = CardType.Action,
            ActionKind = kind,
            MoneyValue = value,
            Name = kind.ToString(),
        };

        /// <summary>Create a PropertySet with the given number of property cards.</summary>
        private static PropertySet CreatePropertySet(PropertyColor color, int count, int startId = 1000)
        {
            var set = new PropertySet { Color = color };
            for (int i = 0; i < count; i++)
                set.Cards.Add(CreateProperty(startId + i, color));
            return set;
        }

        // =====================================================================
        // SimulationState tests
        // =====================================================================

        [Fact]
        public void SimulationState_FromGame_CapturesBotHand()
        {
            // Arrange: bot has 3 cards in hand
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateMoney(1, 1));
            bot.Hand.Add(CreateMoney(2, 2));
            bot.Hand.Add(CreateProperty(3, PropertyColor.Brown));

            var other = CreatePlayer("p1");
            other.Hand.Add(CreateMoney(10, 5)); // opponent's hand (hidden)

            var allPlayers = new List<Player> { bot, other };

            // Act
            var state = SimulationState.FromGame(bot, allPlayers, new Deck(), playsRemaining: 3);

            // Assert: bot's hand is captured, opponent's hand is empty (for determinizer)
            Assert.Equal(3, state.Players[0].Hand.Count);
            Assert.Empty(state.Players[1].Hand); // opponent hand NOT captured
            Assert.Equal(3, state.PlaysRemaining);
            Assert.Equal(0, state.CurrentPlayerIndex); // bot is current player
        }

        [Fact]
        public void SimulationState_Clone_IsDeepCopy()
        {
            // Arrange
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        Hand = new List<Card> { CreateMoney(1, 5) },
                        Bank = new List<Card> { CreateMoney(2, 3) },
                    },
                    new SimPlayer { PlayerId = "opponent" },
                },
                Deck = new SimDeck
                {
                    DrawPile = new List<Card> { CreateMoney(10, 1), CreateMoney(11, 1) },
                },
                PlaysRemaining = 2,
                CurrentPlayerIndex = 0,
                Phase = SimPhase.Playing,
            };

            // Act: clone and modify the clone
            var clone = state.Clone();
            clone.Players[0].Hand.Clear();
            clone.Players[0].Bank.Add(CreateMoney(99, 10));
            clone.Deck.DrawPile.Clear();
            clone.PlaysRemaining = 0;

            // Assert: original is unchanged
            Assert.Single(state.Players[0].Hand);
            Assert.Single(state.Players[0].Bank);
            Assert.Equal(2, state.Deck.DrawPile.Count);
            Assert.Equal(2, state.PlaysRemaining);
        }

        // =====================================================================
        // Determinizer tests
        // =====================================================================

        [Fact]
        public void Determinizer_DistributesAllUnknownCards()
        {
            // Arrange: 2 players, bot has 3 cards, opponent has 2 (unknown)
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateMoney(1, 1));
            bot.Hand.Add(CreateMoney(2, 2));
            bot.Hand.Add(CreateMoney(3, 3));

            var other = CreatePlayer("p1");
            // opponent hand empty in sim state — determinizer fills it

            // Total card pool: 10 cards
            var allCards = new List<Card>();
            for (int i = 1; i <= 10; i++)
                allCards.Add(CreateMoney(i, i));

            var allPlayers = new List<Player> { bot, other };
            var state = SimulationState.FromGame(bot, allPlayers, new Deck(), playsRemaining: 3);
            // Override deck to empty (we control all cards)
            state.Deck = new SimDeck();

            var handSizes = new int[] { 3, 2 }; // bot=3, opponent=2

            // Act
            Determinizer.Determinize(state, botIndex: 0, handSizes, allCards, new Random(42));

            // Assert
            Assert.Equal(3, state.Players[0].Hand.Count); // bot unchanged
            Assert.Equal(2, state.Players[1].Hand.Count); // opponent gets 2
            // Remaining 5 cards (10 - 3 bot - 2 opponent) should be in draw pile
            Assert.Equal(5, state.Deck.DrawPile.Count);

            // All card IDs should be unique (no duplicates)
            var allIds = state.Players[0].Hand.Select(c => c.Id)
                .Concat(state.Players[1].Hand.Select(c => c.Id))
                .Concat(state.Deck.DrawPile.Select(c => c.Id))
                .ToList();
            Assert.Equal(allIds.Count, allIds.Distinct().Count());
        }

        [Fact]
        public void Determinizer_ExcludesVisibleCards()
        {
            // Arrange: bot has cards, opponent has visible bank cards
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(CreateMoney(1, 1));

            var other = CreatePlayer("p1");
            other.Bank.Add(CreateMoney(2, 2)); // visible on board

            var allCards = new List<Card>();
            for (int i = 1; i <= 5; i++)
                allCards.Add(CreateMoney(i, i));

            var allPlayers = new List<Player> { bot, other };
            var state = SimulationState.FromGame(bot, allPlayers, new Deck(), playsRemaining: 3);
            state.Deck = new SimDeck();

            var handSizes = new int[] { 1, 1 }; // opponent has 1 hidden card

            // Act
            Determinizer.Determinize(state, botIndex: 0, handSizes, allCards, new Random(42));

            // Assert: opponent gets 1 card, and it's NOT card 1 (bot's) or card 2 (visible bank)
            Assert.Single(state.Players[1].Hand);
            var opponentCardId = state.Players[1].Hand[0].Id;
            Assert.NotEqual(1, opponentCardId); // not bot's card
            Assert.NotEqual(2, opponentCardId); // not visible bank card
        }

        // =====================================================================
        // MoveGenerator tests
        // =====================================================================

        [Fact]
        public void MoveGenerator_AlwaysIncludesEndTurn()
        {
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer { PlayerId = "bot", Hand = new List<Card>() },
                },
                PlaysRemaining = 3,
            };

            var moves = MoveGenerator.GetLegalMoves(state, 0);

            // Even with empty hand, end turn is always available
            Assert.Single(moves);
            Assert.True(moves[0].IsEndTurn);
        }

        [Fact]
        public void MoveGenerator_MoneyCardOnlyBanks()
        {
            // Money cards can only be played as money (banked)
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        Hand = new List<Card> { CreateMoney(1, 5) },
                    },
                },
                PlaysRemaining = 3,
            };

            var moves = MoveGenerator.GetLegalMoves(state, 0);

            // Should have: EndTurn + BankAsMoney (money card "play as money" is same as regular play)
            Assert.Equal(2, moves.Count); // EndTurn + 1 money move
            var moneyMove = moves.First(m => !m.IsEndTurn);
            Assert.True(moneyMove.PlayAsMoney);
        }

        [Fact]
        public void MoveGenerator_PropertyCardOnlyPlaysAsProperty()
        {
            // Property can only be played as property, not banked as money
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        Hand = new List<Card> { CreateProperty(1, PropertyColor.Brown) },
                    },
                },
                PlaysRemaining = 3,
            };

            var moves = MoveGenerator.GetLegalMoves(state, 0);

            // EndTurn + PlayAsProperty only (no PlayAsMoney)
            Assert.Equal(2, moves.Count);
            Assert.Contains(moves, m => !m.IsEndTurn && !m.PlayAsMoney);
            Assert.DoesNotContain(moves, m => !m.IsEndTurn && m.PlayAsMoney);
        }

        [Fact]
        public void MoveGenerator_RentCardGeneratesColorMoves()
        {
            // Rent card with matching properties should generate rent moves
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        Hand = new List<Card>
                        {
                            CreateRent(100, PropertyColor.Red, PropertyColor.Yellow),
                        },
                        PropertySets = new List<SimPropertySet>
                        {
                            new SimPropertySet
                            {
                                Color = PropertyColor.Red,
                                Cards = new List<Card> { CreateProperty(10, PropertyColor.Red) },
                            },
                        },
                    },
                    new SimPlayer { PlayerId = "opponent" },
                },
                PlaysRemaining = 3,
            };

            var moves = MoveGenerator.GetLegalMoves(state, 0);

            // Should have: EndTurn + PlayAsMoney + Rent(Red) [no Yellow since no Yellow properties]
            var rentMoves = moves.Where(m => !m.IsEndTurn && !m.PlayAsMoney && m.RentColor.HasValue).ToList();
            Assert.Single(rentMoves);
            Assert.Equal(PropertyColor.Red, rentMoves[0].RentColor);
        }

        [Fact]
        public void MoveGenerator_ExcludesJSNAndDTR()
        {
            // Just Say No and DoubleTheRent should NOT appear as any moves (kept in hand)
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        Hand = new List<Card>
                        {
                            CreateAction(1, ActionType.JustSayNo, 4),
                            CreateAction(2, ActionType.DoubleTheRent, 1),
                        },
                    },
                },
                PlaysRemaining = 3,
            };

            var moves = MoveGenerator.GetLegalMoves(state, 0);

            // Only EndTurn — JSN and DTR are reactive-only, never played proactively
            Assert.Single(moves);
            Assert.True(moves[0].IsEndTurn);
        }

        [Fact]
        public void MoveGenerator_DealBreakerTargetsCompleteSets()
        {
            // DealBreaker should only target complete sets
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        Hand = new List<Card> { CreateAction(1, ActionType.DealBreaker, 5) },
                    },
                    new SimPlayer
                    {
                        PlayerId = "opponent",
                        PropertySets = new List<SimPropertySet>
                        {
                            // Complete brown set (2/2)
                            new SimPropertySet
                            {
                                Color = PropertyColor.Brown,
                                Cards = new List<Card>
                                {
                                    CreateProperty(10, PropertyColor.Brown),
                                    CreateProperty(11, PropertyColor.Brown),
                                },
                            },
                            // Incomplete red set (1/3)
                            new SimPropertySet
                            {
                                Color = PropertyColor.Red,
                                Cards = new List<Card> { CreateProperty(20, PropertyColor.Red) },
                            },
                        },
                    },
                },
                PlaysRemaining = 3,
            };

            var moves = MoveGenerator.GetLegalMoves(state, 0);

            // Should generate DealBreaker targeting Brown (complete) but not Red (incomplete)
            var dbMoves = moves.Where(m => !m.IsEndTurn && !m.PlayAsMoney && m.TargetSetColor.HasValue).ToList();
            Assert.Single(dbMoves);
            Assert.Equal(PropertyColor.Brown, dbMoves[0].TargetSetColor);
            Assert.Equal(1, dbMoves[0].TargetPlayerIndex);
        }

        // =====================================================================
        // GameSimulator tests
        // =====================================================================

        [Fact]
        public void GameSimulator_PlayProperty_AddsToCorrectSet()
        {
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        Hand = new List<Card> { CreateProperty(1, PropertyColor.Brown) },
                    },
                },
                PlaysRemaining = 3,
                Phase = SimPhase.Playing,
            };

            var move = new SimMove
            {
                Card = state.Players[0].Hand[0],
                PlayAsMoney = false,
            };

            GameSimulator.ExecuteMove(state, 0, move, RolloutPolicy.BuildResponse);

            // Card should be in a Brown property set now
            Assert.Empty(state.Players[0].Hand);
            Assert.Single(state.Players[0].PropertySets);
            Assert.Equal(PropertyColor.Brown, state.Players[0].PropertySets[0].Color);
            Assert.Single(state.Players[0].PropertySets[0].Cards);
        }

        [Fact]
        public void GameSimulator_PlayAsMoney_BanksCard()
        {
            var card = CreateAction(1, ActionType.PassGo, 1);
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        Hand = new List<Card> { card },
                    },
                },
                PlaysRemaining = 3,
                Phase = SimPhase.Playing,
            };

            var move = new SimMove { Card = card, PlayAsMoney = true };
            GameSimulator.ExecuteMove(state, 0, move, RolloutPolicy.BuildResponse);

            Assert.Empty(state.Players[0].Hand);
            Assert.Single(state.Players[0].Bank);
            Assert.Equal(1, state.Players[0].Bank[0].MoneyValue);
        }

        [Fact]
        public void GameSimulator_DetectsWin()
        {
            // Set up a player one property away from winning (needs 3 unique complete sets)
            var state = new SimulationState
            {
                Players = new List<SimPlayer>
                {
                    new SimPlayer
                    {
                        PlayerId = "bot",
                        // 2 complete sets (Brown 2/2, Utility 2/2)
                        PropertySets = new List<SimPropertySet>
                        {
                            new SimPropertySet
                            {
                                Color = PropertyColor.Brown,
                                Cards = new List<Card>
                                {
                                    CreateProperty(10, PropertyColor.Brown),
                                    CreateProperty(11, PropertyColor.Brown),
                                },
                            },
                            new SimPropertySet
                            {
                                Color = PropertyColor.Utility,
                                Cards = new List<Card>
                                {
                                    CreateProperty(20, PropertyColor.Utility),
                                    CreateProperty(21, PropertyColor.Utility),
                                },
                            },
                            // DarkBlue 1/2 — one more completes it
                            new SimPropertySet
                            {
                                Color = PropertyColor.DarkBlue,
                                Cards = new List<Card>
                                {
                                    CreateProperty(30, PropertyColor.DarkBlue),
                                },
                            },
                        },
                        // Has the winning card in hand
                        Hand = new List<Card>
                        {
                            CreateProperty(31, PropertyColor.DarkBlue, 4),
                        },
                    },
                    new SimPlayer { PlayerId = "opponent" },
                },
                PlaysRemaining = 3,
                Phase = SimPhase.Playing,
                Deck = new SimDeck(),
            };

            // Play the winning property
            var move = new SimMove
            {
                Card = state.Players[0].Hand[0],
                PlayAsMoney = false,
            };
            GameSimulator.ExecuteMove(state, 0, move, RolloutPolicy.BuildResponse);

            // Bot should now have 3 unique complete sets
            Assert.Equal(3, state.Players[0].UniqueCompletedSetCount);
        }

        // =====================================================================
        // RolloutPolicy tests
        // =====================================================================

        [Fact]
        public void RolloutPolicy_JSN_AlwaysBlocksDealBreaker()
        {
            var player = new SimPlayer
            {
                PlayerId = "bot",
                Hand = new List<Card>
                {
                    CreateAction(99, ActionType.JustSayNo, 4),
                },
                PropertySets = new List<SimPropertySet>
                {
                    new SimPropertySet
                    {
                        Color = PropertyColor.Brown,
                        Cards = new List<Card>
                        {
                            CreateProperty(10, PropertyColor.Brown),
                            CreateProperty(11, PropertyColor.Brown),
                        },
                    },
                },
            };

            var state = new SimulationState
            {
                Players = new List<SimPlayer> { new SimPlayer { PlayerId = "attacker" }, player },
                PendingAction = new SimPendingAction
                {
                    Type = PendingActionType.RespondToDealBreaker,
                    SourcePlayerIndex = 0,
                    TargetPlayerIndices = new List<int> { 1 },
                    TargetSetColor = PropertyColor.Brown,
                },
                Phase = SimPhase.AwaitingResponse,
            };

            var response = RolloutPolicy.BuildResponse(state, 1);

            Assert.True(response.PlayJustSayNo);
        }

        [Fact]
        public void RolloutPolicy_PaymentPrefersBank()
        {
            // When paying rent, should use bank cards first
            var player = new SimPlayer
            {
                PlayerId = "bot",
                Bank = new List<Card>
                {
                    CreateMoney(1, 2),
                    CreateMoney(2, 3),
                },
                PropertySets = new List<SimPropertySet>
                {
                    new SimPropertySet
                    {
                        Color = PropertyColor.Red,
                        Cards = new List<Card> { CreateProperty(10, PropertyColor.Red, 3) },
                    },
                },
            };

            var state = new SimulationState
            {
                Players = new List<SimPlayer> { new SimPlayer { PlayerId = "attacker" }, player },
                PendingAction = new SimPendingAction
                {
                    Type = PendingActionType.PayRent,
                    SourcePlayerIndex = 0,
                    TargetPlayerIndices = new List<int> { 1 },
                    Amount = 3,
                },
                Phase = SimPhase.AwaitingResponse,
            };

            var response = RolloutPolicy.BuildResponse(state, 1);

            Assert.False(response.PlayJustSayNo);
            Assert.NotNull(response.PaymentCardIds);
            // Should pay with bank cards (ID 2 = $3), not property
            Assert.DoesNotContain(10, response.PaymentCardIds!);
        }

        // =====================================================================
        // ISMCTSEngine integration tests
        // =====================================================================

        [Fact]
        public void ISMCTSEngine_MoveKey_EndTurnIsStable()
        {
            var move = new SimMove { IsEndTurn = true };
            Assert.Equal("END", ISMCTSEngine.MoveKey(move));
        }

        [Fact]
        public void ISMCTSEngine_MoveKey_DistinguishesMoveTypes()
        {
            var card = CreateProperty(1, PropertyColor.Brown);

            var asMoney = new SimMove { Card = card, PlayAsMoney = true };
            var asProperty = new SimMove { Card = card, PlayAsMoney = false };

            Assert.NotEqual(ISMCTSEngine.MoveKey(asMoney), ISMCTSEngine.MoveKey(asProperty));
        }

        [Fact]
        public void ISMCTSEngine_ProducesValidMove()
        {
            // Minimal setup: bot has one money card, opponent exists
            var bot = CreatePlayer("bot-1");
            var money = CreateMoney(1, 5);
            bot.Hand.Add(money);

            var other = CreatePlayer("p1");
            other.Hand.Add(CreateMoney(10, 1));

            var allPlayers = new List<Player> { bot, other };
            var allCards = new List<Card> { money, CreateMoney(10, 1) };
            // Add more cards to the pool so determinization works
            for (int i = 20; i < 40; i++)
                allCards.Add(CreateMoney(i, 1));

            var state = SimulationState.FromGame(bot, allPlayers, new Deck(), playsRemaining: 3);

            var config = new ISMCTSConfig
            {
                Iterations = 50, // small for test speed
                TimeLimitMs = 100,
                MaxRolloutTurns = 5,
            };

            var bestMove = ISMCTSEngine.FindBestMove(
                state, botIndex: 0, allCards,
                opponentHandSizes: new int[] { 1, 1 },
                config: config);

            // Should produce a valid move (either bank the money or end turn)
            Assert.NotNull(bestMove);
            // The money card should be banked (only sensible play)
            if (!bestMove.IsEndTurn)
            {
                Assert.Equal(1, bestMove.Card?.Id);
                Assert.True(bestMove.PlayAsMoney);
            }
        }
    }
}
