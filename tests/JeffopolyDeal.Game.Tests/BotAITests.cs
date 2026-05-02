using JeffopolyDeal.ISMCTS;
using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for the BotAI static helper: card selection, discards, targeting, and integration.
    /// </summary>
    public class BotAITests
    {
        // ── IsBot ────────────────────────────────────────────────────────

        [Fact]
        public void IsBot_ReturnsTrueForBotConnectionId()
        {
            Assert.True(SmartBotAI.IsBot("bot-Alice"));
            Assert.True(SmartBotAI.IsBot("bot-123"));
        }

        [Fact]
        public void IsBot_ReturnsFalseForRegularConnectionId()
        {
            Assert.False(SmartBotAI.IsBot("conn-Alice"));
            Assert.False(SmartBotAI.IsBot("player-1"));
            Assert.False(SmartBotAI.IsBot(""));
        }

        // ── PickCardToPlay (tested indirectly via PlayTurn) ──────────────

        [Fact]
        public void PlayTurn_PrefersHighScoreCardFirst()
        {
            var bot = CreateBot("bot-test");
            var others = new List<Player> { CreateBot("bot-other") };
            var allPlayers = new List<Player> { bot, others[0] };

            // Give bot a money card (score 20) and a property card (score 30+)
            bot.Hand.Add(CreateMoneyCard(2, 3));
            bot.Hand.Add(CreatePropertyCard(1, PropertyColor.Brown));

            Card? firstPlayed = null;
            SmartBotAI.PlayTurn(bot, allPlayers, CreateDummyDeck(), (b, card, req) =>
            {
                firstPlayed ??= card;
                b.Hand.Remove(card);
                if (req.PlayAsMoney == true)
                    b.Bank.Add(card);
                return false; // stop after one play
            }, maxPlays: 1, config: ISMCTSConfig.Heuristic);

            Assert.NotNull(firstPlayed);
            // SmartBotAI prefers property (score 30) over money (score 20)
            Assert.Equal(CardType.Property, firstPlayed!.CardType);
        }

        [Fact]
        public void PlayTurn_PrefersPassGoEarlyInTurn()
        {
            var bot = CreateBot("bot-test");
            var others = new List<Player> { CreateBot("bot-other") };
            var allPlayers = new List<Player> { bot, others[0] };

            // PassGo scores 80 with 3 remaining, property scores 30
            bot.Hand.Add(CreatePropertyCard(2, PropertyColor.Green));
            bot.Hand.Add(CreateActionCard(1, ActionType.PassGo, 1));

            Card? firstPlayed = null;
            SmartBotAI.PlayTurn(bot, allPlayers, CreateDummyDeck(), (b, card, req) =>
            {
                firstPlayed ??= card;
                b.Hand.Remove(card);
                return false;
            }, maxPlays: 3, config: ISMCTSConfig.Heuristic);

            Assert.NotNull(firstPlayed);
            // SmartBotAI prefers PassGo early (score 80) over property (score 30)
            Assert.Equal(ActionType.PassGo, firstPlayed!.ActionKind);
        }

        [Fact]
        public void PlayTurn_NeverPlaysJustSayNoOrDoubleTheRent()
        {
            var bot = CreateBot("bot-test");
            var others = new List<Player> { CreateBot("bot-other") };
            var allPlayers = new List<Player> { bot, others[0] };

            // Hand contains only JSN and DTR — SmartBotAI scores both as null (never play)
            bot.Hand.Add(CreateActionCard(1, ActionType.JustSayNo, 4));
            bot.Hand.Add(CreateActionCard(2, ActionType.DoubleTheRent, 1));

            var playedCards = new List<(Card card, PlayCardRequest req)>();
            SmartBotAI.PlayTurn(bot, allPlayers, CreateDummyDeck(), (b, card, req) =>
            {
                playedCards.Add((card, req));
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3, config: ISMCTSConfig.Heuristic);

            // SmartBotAI never plays JSN or DTR proactively
            Assert.Empty(playedCards);
        }

        [Fact]
        public void PlayTurn_ReturnsImmediatelyWhenHandIsEmpty()
        {
            var bot = CreateBot("bot-test");
            var allPlayers = new List<Player> { bot };

            // Hand is empty
            int playCount = 0;
            SmartBotAI.PlayTurn(bot, allPlayers, CreateDummyDeck(), (b, card, req) =>
            {
                playCount++;
                return true;
            }, maxPlays: 3, config: ISMCTSConfig.Heuristic);

            Assert.Equal(0, playCount);
        }

        // ── PickDiscards ─────────────────────────────────────────────────

        [Fact]
        public void PickDiscards_KeepsJustSayNoCards()
        {
            var bot = CreateBot("bot-test");
            var lowMoney = CreateMoneyCard(1, 1);
            var jsn = CreateActionCard(2, ActionType.JustSayNo, 4);
            var anotherLow = CreateMoneyCard(3, 1);

            bot.Hand.AddRange(new[] { lowMoney, jsn, anotherLow });

            // max hand size 2 → must discard 1
            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 2);

            Assert.Single(discards);
            // Should discard a low-value money card, NOT the JSN
            Assert.DoesNotContain(jsn.Id, discards);
        }

        [Fact]
        public void PickDiscards_DiscardsLowestValueFirst()
        {
            var bot = CreateBot("bot-test");
            var money1 = CreateMoneyCard(1, 1);
            var money5 = CreateMoneyCard(2, 5);
            var money3 = CreateMoneyCard(3, 3);
            var money2 = CreateMoneyCard(4, 2);

            bot.Hand.AddRange(new[] { money1, money5, money3, money2 });

            // max hand size 2 → discard 2
            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 2);

            Assert.Equal(2, discards.Count);
            // The two lowest value cards (1M and 2M) should be discarded
            Assert.Contains(money1.Id, discards);
            Assert.Contains(money2.Id, discards);
        }

        [Fact]
        public void PickDiscards_ReturnsEmptyWhenUnderLimit()
        {
            var bot = CreateBot("bot-test");
            bot.Hand.Add(CreateMoneyCard(1, 5));

            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 7);

            Assert.Empty(discards);
        }

        [Fact]
        public void PickDiscards_KeepsRentOverLowValueCards()
        {
            var bot = CreateBot("bot-test");
            var lowMoney = CreateMoneyCard(1, 1);
            var rent = CreateRentCard(2, PropertyColor.Brown, PropertyColor.LightBlue);
            var anotherLow = CreateMoneyCard(3, 1);

            bot.Hand.AddRange(new[] { lowMoney, rent, anotherLow });

            // max hand size 2 → discard 1
            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 2);

            Assert.Single(discards);
            Assert.DoesNotContain(rent.Id, discards);
        }

        // ── PickRichestTarget (tested indirectly via PlayTurn + DebtCollector) ──

        [Fact]
        public void PlayTurn_DebtCollectorTargetsRichestPlayer()
        {
            var bot = CreateBot("bot-test");
            var poor = CreateBot("bot-poor");
            var rich = CreateBot("bot-rich");

            // Give rich player more assets
            rich.Bank.Add(CreateMoneyCard(100, 5));
            rich.Bank.Add(CreateMoneyCard(101, 5));
            poor.Bank.Add(CreateMoneyCard(102, 1));

            var allPlayers = new List<Player> { bot, poor, rich };
            bot.Hand.Add(CreateActionCard(1, ActionType.DebtCollector, 3));

            PlayCardRequest? capturedReq = null;
            SmartBotAI.PlayTurn(bot, allPlayers, CreateDummyDeck(), (b, card, req) =>
            {
                capturedReq = req;
                b.Hand.Remove(card);
                return false;
            }, maxPlays: 1, config: ISMCTSConfig.Heuristic);

            Assert.NotNull(capturedReq);
            Assert.Equal(rich.ConnectionId, capturedReq!.TargetPlayerId);
        }

        // ── BuildResponse ────────────────────────────────────────────────

        [Fact]
        public void BuildResponse_PaysWithOptimalCardsWhenNoJSN()
        {
            var bot = CreateBot("bot-test");
            // No JSN in hand, some money in bank
            bot.Bank.Add(CreateMoneyCard(1, 1));
            bot.Bank.Add(CreateMoneyCard(2, 5));

            var pending = new PendingAction
            {
                Type = PendingActionType.PayRent,
                Amount = 3,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { bot.ConnectionId },
            };
            var allPlayers = new List<Player> { bot, CreateBot("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            // Without JSN, should always choose to pay
            Assert.False(response.PlayJustSayNo);
            Assert.NotNull(response.PaymentCardIds);
            Assert.NotEmpty(response.PaymentCardIds!);
        }

        // ── Integration: bot auto-plays turn ─────────────────────────────

        [Fact]
        public async Task WhenHumanEndsTurn_BotAutoPlaysItsTurn()
        {
            var h = new TestGameHarness();
            var human = await h.AddPlayerAsync("Human");
            var bot = await h.AddBotAsync("BotAuto");
            await h.Game.StartGameAsync(allowSinglePlayer: false, startingPlayerIndex: 0);

            // Human draws and ends turn — bot should auto-play
            await h.DrawAsync(human);

            // Make bot's state fully deterministic: clear hand, stack deck with money
            var botPlayer = h.Game.GetPlayer(bot)!;
            botPlayer.Hand.Clear();
            botPlayer.Hand.Add(CreateMoneyCard(900, 2));
            botPlayer.Hand.Add(CreatePropertyCard(901, PropertyColor.Brown));
            h.StackDeckWithMoney();

            int handBefore = botPlayer.Hand.Count;

            await h.EndTurnAsync(human);

            // Bot drew 2 money cards + played its hand. Turn should be back to human.
            int handAfter = botPlayer.Hand.Count;
            bool handChanged = handAfter != handBefore;

            var state = h.GetState(human);
            Assert.NotNull(state);
            Assert.Equal(0, state!.CurrentPlayerIndex);
            Assert.True(handChanged, "Bot's hand should change after auto-playing its turn");
        }

        [Fact]
        public async Task WhenHumanEndsTurn_BotPlaysCardsOnBoard()
        {
            var h = new TestGameHarness();
            var human = await h.AddPlayerAsync("Human");
            var bot = await h.AddBotAsync("BotPlay");
            await h.Game.StartGameAsync(allowSinglePlayer: false, startingPlayerIndex: 0);

            await h.DrawAsync(human);

            // Stack deck with money so bot draws are deterministic
            h.StackDeckWithMoney();

            var botPlayer = h.Game.GetPlayer(bot)!;
            // Clear hand and give bot deterministic cards
            botPlayer.Hand.Clear();
            botPlayer.Hand.Add(CreateMoneyCard(900, 3));
            botPlayer.Hand.Add(CreatePropertyCard(901, PropertyColor.DarkBlue));

            int bankBefore = botPlayer.Bank.Count;
            int propsBefore = botPlayer.PropertySets.Sum(s => s.Cards.Count);

            await h.EndTurnAsync(human);

            int bankAfter = botPlayer.Bank.Count;
            int propsAfter = botPlayer.PropertySets.Sum(s => s.Cards.Count);

            // Bot should have placed money in bank and/or property on board
            bool somethingPlayed = bankAfter > bankBefore || propsAfter > propsBefore;
            Assert.True(somethingPlayed, "Bot should play cards onto the board during its turn");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static Player CreateBot(string connectionId)
        {
            return new Player
            {
                ConnectionId = connectionId,
                PlayerId = connectionId,
                Name = connectionId,
            };
        }

        private static Card CreateMoneyCard(int id, int value) => new()
        {
            Id = id,
            CardType = CardType.Money,
            MoneyValue = value,
            Name = $"{value}M",
        };

        private static Card CreatePropertyCard(int id, PropertyColor color) => new()
        {
            Id = id,
            CardType = CardType.Property,
            Color = color,
            Name = $"{color} Property",
        };

        private static Card CreateActionCard(int id, ActionType action, int moneyValue) => new()
        {
            Id = id,
            CardType = CardType.Action,
            ActionKind = action,
            MoneyValue = moneyValue,
            Name = action.ToString(),
        };

        private static Card CreateRentCard(int id, PropertyColor color1, PropertyColor color2) => new()
        {
            Id = id,
            CardType = CardType.Rent,
            RentColors = new List<PropertyColor> { color1, color2 },
            MoneyValue = 1,
            Name = $"{color1}/{color2} Rent",
        };

        /// <summary>
        /// Creates a minimal Deck instance for PlayTurn calls.
        /// The deck itself isn't used by the card-picking logic we test.
        /// </summary>
        private static Deck CreateDummyDeck() => new();
    }
}
