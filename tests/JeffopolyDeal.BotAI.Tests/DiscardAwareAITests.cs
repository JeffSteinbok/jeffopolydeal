using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    /// <summary>
    /// Tests for discard-aware AI and receiver-win-avoidance payment logic.
    /// Covers:
    ///   - Issue #96: PaymentSolver avoids giving property cards that would win the game
    ///                for the receiver.
    ///   - Issue #97: BoardAnalyzer discard utility methods; CardEvaluator and SmartBotAI
    ///                take the discard pile into account.
    /// </summary>
    public class DiscardAwareAITests
    {
        // ═══════════════════════════════════════════════════════════════════
        // Issue #96 — PaymentSolver: do not help receiver win
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void FindOptimalPayment_WithReceiver_AvoidsPropertyThatWouldWin()
        {
            // Receiver already has 2 complete sets and a DarkBlue set with 1 of 2 cards.
            // Paying a DarkBlue property would complete that set → receiver wins.
            // The bot must sacrifice a different card instead.

            var bot = CreatePlayer("bot-1");
            var receiver = CreatePlayer("human-1");

            // Bot: $3 in bank + one DarkBlue property (MoneyValue=4)
            bot.Bank.Add(CreateMoney(50, 3));
            var darkBlueProp = new Card { Id = 10, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue, Name = "DB Prop" };
            var botSet = new PropertySet { Color = PropertyColor.DarkBlue };
            botSet.Cards.Add(darkBlueProp);
            bot.PropertySets.Add(botSet);

            // Receiver: 2 complete sets + DarkBlue set with 1 of 2 cards
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Utility, 2));
            var receiverDbSet = new PropertySet { Color = PropertyColor.DarkBlue };
            var receiverDbCard = new Card { Id = 20, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue, Name = "DB Recv" };
            receiverDbSet.Cards.Add(receiverDbCard);
            receiver.PropertySets.Add(receiverDbSet);

            // Owe 3 — bank covers it without needing the property
            var payment = PaymentSolver.FindOptimalPayment(bot, 3, receiver);

            Assert.True(payment.Sum(c => c.MoneyValue) >= 3, "Must pay at least 3.");
            Assert.DoesNotContain(darkBlueProp, payment);
        }

        [Fact]
        public void FindOptimalPayment_WithReceiver_PaysDarkBlueIfInsolvent()
        {
            // Even if the property would let receiver win, insolvent bot must pay everything.
            var bot = CreatePlayer("bot-1");
            var receiver = CreatePlayer("human-1");

            bot.Bank.Add(CreateMoney(50, 1));
            var darkBlueProp = new Card { Id = 10, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue, Name = "DB Prop" };
            var botSet = new PropertySet { Color = PropertyColor.DarkBlue };
            botSet.Cards.Add(darkBlueProp);
            bot.PropertySets.Add(botSet);

            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Utility, 2));
            var receiverDbSet = new PropertySet { Color = PropertyColor.DarkBlue };
            receiverDbSet.Cards.Add(new Card { Id = 20, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue, Name = "DB Recv" });
            receiver.PropertySets.Add(receiverDbSet);

            // Owe 10 but only have 5 total → insolvent
            var payment = PaymentSolver.FindOptimalPayment(bot, 10, receiver);

            // Must pay everything (insolvent rule)
            Assert.Equal(5, payment.Sum(c => c.MoneyValue));
        }

        [Fact]
        public void FindOptimalPayment_WithReceiver_SafeToPayNonWinningProperty()
        {
            // Receiver is NOT near winning (only 1 complete set) — bot may pay a property
            // normally without special restrictions.
            var bot = CreatePlayer("bot-1");
            var receiver = CreatePlayer("human-1");

            bot.Bank.Add(CreateMoney(50, 1));
            var redProp = new Card { Id = 10, CardType = CardType.Property, MoneyValue = 3,
                Color = PropertyColor.Red, ActiveColor = PropertyColor.Red, Name = "Red Prop" };
            var botRedSet = new PropertySet { Color = PropertyColor.Red };
            botRedSet.Cards.Add(redProp);
            bot.PropertySets.Add(botRedSet);

            // Receiver has only 1 complete set — not near winning
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            var payment = PaymentSolver.FindOptimalPayment(bot, 3, receiver);

            // Red property IS a valid payment option (not restricted)
            Assert.True(payment.Sum(c => c.MoneyValue) >= 3);
        }

        [Fact]
        public void CardStrategicValue_WithReceiverNearWin_WinningCardIsMaxValue()
        {
            var bot = CreatePlayer("bot-1");
            var receiver = CreatePlayer("human-1");

            var darkBlueProp = new Card { Id = 10, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue, Name = "DB Prop" };
            var botSet = new PropertySet { Color = PropertyColor.DarkBlue };
            botSet.Cards.Add(darkBlueProp);
            bot.PropertySets.Add(botSet);

            // Receiver: 2 complete sets + DarkBlue with 1 of 2 → paying the DB card wins it
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Utility, 2));
            var receiverDbSet = new PropertySet { Color = PropertyColor.DarkBlue };
            receiverDbSet.Cards.Add(new Card { Id = 20, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue });
            receiver.PropertySets.Add(receiverDbSet);

            int value = PaymentSolver.CardStrategicValue(bot, darkBlueProp, receiver);
            Assert.Equal(1000, value);
        }

        [Fact]
        public void CardStrategicValue_WithReceiverNotNearWin_NormalValue()
        {
            var bot = CreatePlayer("bot-1");
            var receiver = CreatePlayer("human-1");

            // Bot has a single-card DarkBlue set
            var darkBlueProp = new Card { Id = 10, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue, Name = "DB Prop" };
            var botSet = new PropertySet { Color = PropertyColor.DarkBlue };
            botSet.Cards.Add(darkBlueProp);
            bot.PropertySets.Add(botSet);

            // Receiver has only 1 complete set — far from winning
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));

            int value = PaymentSolver.CardStrategicValue(bot, darkBlueProp, receiver);
            Assert.NotEqual(1000, value);
        }

        [Fact]
        public void FindOptimalPayment_WithReceiver_WildcardThatWouldCompleteSetIsAvoided()
        {
            // Receiver has 2 complete sets + a near-complete Red set (2 of 3).
            // Bot has a Red/Yellow wildcard as its only valuable asset.
            // Paying that wildcard would complete Red for the receiver → avoid it if possible.

            var bot = CreatePlayer("bot-1");
            var receiver = CreatePlayer("human-1");

            bot.Bank.Add(CreateMoney(50, 4)); // $4 bank, enough to cover
            var wild = new Card { Id = 10, CardType = CardType.PropertyWildcard, MoneyValue = 3,
                Color = PropertyColor.Red, AltColor = PropertyColor.Yellow,
                ActiveColor = PropertyColor.Red, Name = "Red/Yellow Wild" };
            var botSet = new PropertySet { Color = PropertyColor.Red };
            botSet.Cards.Add(wild);
            bot.PropertySets.Add(botSet);

            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Utility, 2));
            var receiverRedSet = new PropertySet { Color = PropertyColor.Red };
            for (int i = 0; i < 2; i++)
                receiverRedSet.Cards.Add(new Card { Id = 20 + i, CardType = CardType.Property,
                    Color = PropertyColor.Red, MoneyValue = 3 });
            receiver.PropertySets.Add(receiverRedSet); // 2 of 3 Red cards

            // Owe 3 — bank can cover it
            var payment = PaymentSolver.FindOptimalPayment(bot, 3, receiver);

            Assert.True(payment.Sum(c => c.MoneyValue) >= 3);
            Assert.DoesNotContain(wild, payment);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Issue #97 — BoardAnalyzer: discard pile awareness
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void CountDiscarded_ReturnsCorrectCountForActionType()
        {
            var discardPile = new List<Card>
            {
                new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.JustSayNo },
                new Card { Id = 2, CardType = CardType.Action, ActionKind = ActionType.JustSayNo },
                new Card { Id = 3, CardType = CardType.Action, ActionKind = ActionType.SlyDeal },
                new Card { Id = 4, CardType = CardType.Money, MoneyValue = 1 },
            };

            Assert.Equal(2, BoardAnalyzer.CountDiscarded(discardPile, ActionType.JustSayNo));
            Assert.Equal(1, BoardAnalyzer.CountDiscarded(discardPile, ActionType.SlyDeal));
            Assert.Equal(0, BoardAnalyzer.CountDiscarded(discardPile, ActionType.DealBreaker));
        }

        [Fact]
        public void CountDiscarded_EmptyDiscardReturnsZero()
        {
            Assert.Equal(0, BoardAnalyzer.CountDiscarded(new List<Card>(), ActionType.JustSayNo));
        }

        [Fact]
        public void JsnRemainingInUnknown_AllDiscarded_ReturnsZero()
        {
            var bot = CreatePlayer("bot-1");
            var others = new List<Player> { bot, CreatePlayer("p2") };
            var discard = new List<Card>
            {
                new Card { Id = 1, ActionKind = ActionType.JustSayNo },
                new Card { Id = 2, ActionKind = ActionType.JustSayNo },
                new Card { Id = 3, ActionKind = ActionType.JustSayNo },
            };

            int remaining = BoardAnalyzer.JsnRemainingInUnknown(bot, others, discard);
            Assert.Equal(0, remaining);
        }

        [Fact]
        public void JsnRemainingInUnknown_BotHoldsAllThree_ReturnsZero()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(new Card { Id = 1, ActionKind = ActionType.JustSayNo });
            bot.Hand.Add(new Card { Id = 2, ActionKind = ActionType.JustSayNo });
            bot.Hand.Add(new Card { Id = 3, ActionKind = ActionType.JustSayNo });

            var others = new List<Player> { bot, CreatePlayer("p2") };
            var discard = new List<Card>();

            int remaining = BoardAnalyzer.JsnRemainingInUnknown(bot, others, discard);
            Assert.Equal(0, remaining);
        }

        [Fact]
        public void JsnRemainingInUnknown_NoneDiscardedNoneInHand_ReturnsThree()
        {
            var bot = CreatePlayer("bot-1");
            var others = new List<Player> { bot, CreatePlayer("p2") };

            int remaining = BoardAnalyzer.JsnRemainingInUnknown(bot, others, new List<Card>());
            Assert.Equal(3, remaining);
        }

        [Fact]
        public void JsnRemainingInUnknown_OneDiscardedOneInHand_ReturnsOne()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(new Card { Id = 1, ActionKind = ActionType.JustSayNo });
            var others = new List<Player> { bot, CreatePlayer("p2") };
            var discard = new List<Card>
            {
                new Card { Id = 2, ActionKind = ActionType.JustSayNo },
            };

            int remaining = BoardAnalyzer.JsnRemainingInUnknown(bot, others, discard);
            Assert.Equal(1, remaining);
        }

        [Fact]
        public void EstimateJsnHeldProbability_ZeroRemaining_ReturnsZero()
        {
            double prob = BoardAnalyzer.EstimateJsnHeldProbability(7, 0, 50);
            Assert.Equal(0.0, prob);
        }

        [Fact]
        public void EstimateJsnHeldProbability_HandSizeEqualsUnknown_ReturnsOne()
        {
            // If opponent's hand is as large as the entire unknown pool, they must have
            // at least one JSN (assuming jsnInUnknown > 0).
            double prob = BoardAnalyzer.EstimateJsnHeldProbability(10, 2, 10);
            Assert.Equal(1.0, prob);
        }

        [Fact]
        public void EstimateJsnHeldProbability_LargePool_SmallHand_LowProbability()
        {
            // 1 JSN in a pool of 100 cards, opponent draws 5 → very low probability
            double prob = BoardAnalyzer.EstimateJsnHeldProbability(5, 1, 100);
            Assert.True(prob < 0.10, $"Expected < 10%, got {prob:P1}");
        }

        [Fact]
        public void EstimateJsnHeldProbability_AllJsnInSmallPool_HighProbability()
        {
            // 3 JSN in a pool of 10 unknown cards, opponent draws 7 → very high probability
            double prob = BoardAnalyzer.EstimateJsnHeldProbability(7, 3, 10);
            Assert.True(prob > 0.80, $"Expected > 80%, got {prob:P1}");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Issue #97 — SmartBotAI JSN threshold responds to discard pile
        // ═══════════════════════════════════════════════════════════════════

        [Fact]
        public void BuildResponse_AllJsnDiscarded_LowersThresholdForJsnPlay()
        {
            // With all 3 JSN discarded the effective threshold drops from $5 to $4.
            // A $4 rent charge (bank can't cover it) should now trigger JSN.
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(new Card { Id = 99, CardType = CardType.Action,
                ActionKind = ActionType.JustSayNo, MoneyValue = 4 });
            bot.Bank.Add(CreateMoney(50, 2)); // only $2 in bank — can't cover $4 rent

            var allDiscarded = new List<Card>
            {
                new Card { Id = 1, ActionKind = ActionType.JustSayNo },
                new Card { Id = 2, ActionKind = ActionType.JustSayNo },
                new Card { Id = 3, ActionKind = ActionType.JustSayNo },
            };

            var pending = new PendingAction
            {
                Type = PendingActionType.PayRent,
                Amount = 4,
                SourcePlayerId = "human-1",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("human-1") };

            // Without discard info, $4 is below the $5 threshold → would NOT play JSN
            var responseWithout = SmartBotAI.BuildResponse(bot, pending, allPlayers, null);
            Assert.False(responseWithout.PlayJustSayNo, "Without discard info, $4 should not trigger JSN.");

            // With all JSN discarded, threshold drops to $4 → SHOULD play JSN
            var responseWith = SmartBotAI.BuildResponse(bot, pending, allPlayers, allDiscarded);
            Assert.True(responseWith.PlayJustSayNo, "With all JSN discarded, $4 should trigger JSN (threshold = 4).");
        }

        [Fact]
        public void BuildResponse_NoJsnDiscarded_NormalThreshold()
        {
            // Normal case: no discard info change, $4 rent does not trigger JSN.
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(new Card { Id = 99, CardType = CardType.Action,
                ActionKind = ActionType.JustSayNo, MoneyValue = 4 });
            bot.Bank.Add(CreateMoney(50, 2)); // $2 bank, can't cover $4

            var pending = new PendingAction
            {
                Type = PendingActionType.PayRent,
                Amount = 4,
                SourcePlayerId = "human-1",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("human-1") };

            // No JSN discarded → remaining = 3 → normal threshold of $5 applies
            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers, new List<Card>());
            Assert.False(response.PlayJustSayNo);
        }

        [Fact]
        public void BuildResponse_PassesReceiverToPaymentSolver()
        {
            // End-to-end: when the receiver is near winning, the bot avoids paying
            // a property that would complete the receiver's winning set.

            var bot = CreatePlayer("bot-1");
            var receiver = CreatePlayer("human-1");

            bot.Bank.Add(CreateMoney(50, 5)); // enough bank to cover rent
            var dbProp = new Card { Id = 10, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue };
            var botSet = new PropertySet { Color = PropertyColor.DarkBlue };
            botSet.Cards.Add(dbProp);
            bot.PropertySets.Add(botSet);

            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Brown, 2));
            receiver.PropertySets.Add(CreateCompleteSet(PropertyColor.Utility, 2));
            var recvDbSet = new PropertySet { Color = PropertyColor.DarkBlue };
            recvDbSet.Cards.Add(new Card { Id = 20, CardType = CardType.Property, MoneyValue = 4,
                Color = PropertyColor.DarkBlue, ActiveColor = PropertyColor.DarkBlue });
            receiver.PropertySets.Add(recvDbSet);

            var pending = new PendingAction
            {
                Type = PendingActionType.PayRent,
                Amount = 4,
                SourcePlayerId = receiver.ConnectionId,
                TargetPlayerIds = new List<string> { bot.ConnectionId },
            };
            var allPlayers = new List<Player> { bot, receiver };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            // Bot must not include the DarkBlue property in its payment
            Assert.False(response.PlayJustSayNo);
            Assert.DoesNotContain(dbProp.Id, response.PaymentCardIds!);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static Player CreatePlayer(string id) => new()
        {
            ConnectionId = id,
            PlayerId = id,
            Name = id,
        };

        private static Card CreateMoney(int id, int value) => new()
        {
            Id = id,
            CardType = CardType.Money,
            MoneyValue = value,
            Name = $"{value}M",
        };

        private static PropertySet CreateCompleteSet(PropertyColor color, int size)
        {
            var set = new PropertySet { Color = color };
            for (int i = 0; i < size; i++)
                set.Cards.Add(new Card
                {
                    Id = new Random().Next(1000, 9999),
                    CardType = CardType.Property,
                    Color = color,
                    ActiveColor = color,
                    MoneyValue = 1,
                });
            return set;
        }
    }
}
