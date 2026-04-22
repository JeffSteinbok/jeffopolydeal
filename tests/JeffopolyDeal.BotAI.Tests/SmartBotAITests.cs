using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    public class SmartBotAITests
    {
        [Fact]
        public void BuildResponse_PaysOptimalAmount_NotOverpaying()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 1));
            bot.Bank.Add(CreateMoney(2, 1));
            bot.Bank.Add(CreateMoney(3, 2));
            bot.Bank.Add(CreateMoney(4, 5));

            var pending = new PendingAction
            {
                Type = PendingActionType.PayRent,
                Amount = 3,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            Assert.False(response.PlayJustSayNo);
            var paidCards = bot.GetPayableCards()
                .Where(c => !c.IsMulticolorWild)
                .Where(c => response.PaymentCardIds!.Contains(c.Id))
                .ToList();
            int totalPaid = paidCards.Sum(c => c.MoneyValue);
            Assert.True(totalPaid >= 3);
            // Should not overpay: exact 3 is possible (1+2)
            Assert.Equal(3, totalPaid);
        }

        [Fact]
        public void BuildResponse_UsesJSNAgainstDealBreaker()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(new Card { Id = 99, CardType = CardType.Action, ActionKind = ActionType.JustSayNo, MoneyValue = 4 });

            var pending = new PendingAction
            {
                Type = PendingActionType.RespondToDealBreaker,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
                TargetSetColor = PropertyColor.Brown,
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            Assert.True(response.PlayJustSayNo);
        }

        [Fact]
        public void BuildResponse_DoesNotWasteJSNOnBirthday()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(new Card { Id = 99, CardType = CardType.Action, ActionKind = ActionType.JustSayNo, MoneyValue = 4 });
            bot.Bank.Add(CreateMoney(1, 5));

            var pending = new PendingAction
            {
                Type = PendingActionType.PayBirthday,
                Amount = 2,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            Assert.False(response.PlayJustSayNo);
        }

        [Fact]
        public void PlayTurn_PlaysPassGoBeforeOtherActions()
        {
            var bot = CreatePlayer("bot-1");
            var other = CreatePlayer("p1");
            var allPlayers = new List<Player> { bot, other };

            bot.Hand.Add(CreateMoney(1, 3));
            bot.Hand.Add(new Card { Id = 2, CardType = CardType.Action, ActionKind = ActionType.PassGo, MoneyValue = 1 });

            var playOrder = new List<Card>();
            SmartBotAI.PlayTurn(bot, allPlayers, new Deck(), (b, card, req) =>
            {
                playOrder.Add(card);
                b.Hand.Remove(card);
                return true;
            }, maxPlays: 3);

            Assert.True(playOrder.Count >= 2);
            // PassGo (score 80 with 3 plays) should be played before Money (score 20)
            Assert.Equal(ActionType.PassGo, playOrder[0].ActionKind);
        }

        [Fact]
        public void PickDiscards_DiscardsLowestPriorityCards()
        {
            var bot = CreatePlayer("bot-1");

            // JSN (priority 100) - should be kept
            bot.Hand.Add(new Card { Id = 1, CardType = CardType.Action, ActionKind = ActionType.JustSayNo, MoneyValue = 4 });
            // DealBreaker (priority 70) - should be kept
            bot.Hand.Add(new Card { Id = 2, CardType = CardType.Action, ActionKind = ActionType.DealBreaker, MoneyValue = 5 });
            // Money $1 (priority 1) - should be discarded
            bot.Hand.Add(CreateMoney(3, 1));
            // Money $2 (priority 2) - should be discarded
            bot.Hand.Add(CreateMoney(4, 2));

            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 2);

            Assert.Equal(2, discards.Count);
            Assert.Contains(3, discards); // $1 money
            Assert.Contains(4, discards); // $2 money
        }

        [Fact]
        public void PickDiscards_KeepsSetCompletingProperties()
        {
            var bot = CreatePlayer("bot-1");

            // Near-complete set (1 of 2 for Brown)
            var set = new PropertySet { Color = PropertyColor.Brown };
            set.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            bot.PropertySets.Add(set);

            // Hand: completing Brown property (priority 90), random action (priority 20), money (priority 1)
            bot.Hand.Add(new Card { Id = 1, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            bot.Hand.Add(new Card { Id = 2, CardType = CardType.Action, ActionKind = ActionType.ItsMyBirthday, MoneyValue = 2 });
            bot.Hand.Add(CreateMoney(3, 1));

            var discards = SmartBotAI.PickDiscards(bot, maxHandSize: 2);

            Assert.Single(discards);
            // Should discard money ($1, priority 1), NOT the Brown property (priority 90)
            Assert.Contains(3, discards);
        }

        [Fact]
        public void BuildResponse_DoesNotJSNSmallRentWhenBankCovers()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(new Card { Id = 99, CardType = CardType.Action, ActionKind = ActionType.JustSayNo, MoneyValue = 4 });
            bot.Bank.Add(CreateMoney(1, 5));
            bot.Bank.Add(CreateMoney(2, 5));

            var pending = new PendingAction
            {
                Type = PendingActionType.PayRent,
                Amount = 3,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            // Bank covers the rent, so JSN is wasted here
            Assert.False(response.PlayJustSayNo);
        }

        [Fact]
        public void BuildResponse_JSNsLargeRentWhenBankInsufficient()
        {
            var bot = CreatePlayer("bot-1");
            bot.Hand.Add(new Card { Id = 99, CardType = CardType.Action, ActionKind = ActionType.JustSayNo, MoneyValue = 4 });
            bot.Bank.Add(CreateMoney(1, 2));

            // Near-complete set to protect
            var set = new PropertySet { Color = PropertyColor.Red };
            set.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            set.Cards.Add(new Card { Id = 11, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 });
            bot.PropertySets.Add(set);

            var pending = new PendingAction
            {
                Type = PendingActionType.PayRent,
                Amount = 8,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            // Large rent that would force property sacrifice — should JSN
            Assert.True(response.PlayJustSayNo);
        }

        [Fact]
        public void BuildResponse_AcceptsSlyDealWithoutPayment()
        {
            var bot = CreatePlayer("bot-1");
            // No JSN
            var set = new PropertySet { Color = PropertyColor.Brown };
            set.Cards.Add(new Card { Id = 10, CardType = CardType.Property, Color = PropertyColor.Brown, MoneyValue = 1 });
            bot.PropertySets.Add(set);

            var pending = new PendingAction
            {
                Type = PendingActionType.RespondToSlyDeal,
                SourcePlayerId = "other",
                TargetPlayerIds = new List<string> { "bot-1" },
                TargetCardId = 10,
            };
            var allPlayers = new List<Player> { bot, CreatePlayer("other") };

            var response = SmartBotAI.BuildResponse(bot, pending, allPlayers);

            Assert.False(response.PlayJustSayNo);
            Assert.NotNull(response.PaymentCardIds);
            Assert.Empty(response.PaymentCardIds!);
        }

        // ── Helpers ──────────────────────────────────────────────────────

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
    }
}
