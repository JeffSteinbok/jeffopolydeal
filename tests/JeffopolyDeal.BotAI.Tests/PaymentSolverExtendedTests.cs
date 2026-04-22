using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    public class PaymentSolverExtendedTests
    {
        // ── GreedyPayment fallback for >15 cards ────────────────────────

        [Fact]
        public void FindOptimalPayment_GreedyFallbackForManyCards()
        {
            var bot = CreatePlayer("bot-1");

            // Add 16 bank cards (triggers greedy path)
            for (int i = 1; i <= 16; i++)
            {
                bot.Bank.Add(CreateMoney(i, 1));
            }

            var payment = PaymentSolver.FindOptimalPayment(bot, 3);

            Assert.True(payment.Sum(c => c.MoneyValue) >= 3);
            // Greedy should pick bank cards first (strategic value 0)
            Assert.True(payment.Count <= 5); // Should be efficient
        }

        [Fact]
        public void FindOptimalPayment_GreedyProtectsProperties()
        {
            var bot = CreatePlayer("bot-1");

            // Add 14 bank cards
            for (int i = 1; i <= 14; i++)
            {
                bot.Bank.Add(CreateMoney(i, 1));
            }

            // Add 3 property cards in near-complete set (high strategic value)
            var set = new PropertySet { Color = PropertyColor.Red };
            for (int i = 0; i < 2; i++)
            {
                var prop = new Card
                {
                    Id = 100 + i,
                    CardType = CardType.Property,
                    Color = PropertyColor.Red,
                    ActiveColor = PropertyColor.Red,
                    MoneyValue = 3,
                    Name = $"Red {i}"
                };
                set.Cards.Add(prop);
            }
            bot.PropertySets.Add(set);

            // Total payable = 14 + 6 = 20, which is > 15, so greedy kicks in
            var payment = PaymentSolver.FindOptimalPayment(bot, 5);

            // Should use bank cards, not properties
            Assert.True(payment.Sum(c => c.MoneyValue) >= 5);
            var propsPaid = payment.Where(c => c.CardType == CardType.Property).ToList();
            Assert.Empty(propsPaid);
        }

        // ── CardStrategicValue ──────────────────────────────────────────

        [Fact]
        public void CardStrategicValue_NearCompleteSetPropertyIsHigh()
        {
            var bot = CreatePlayer("bot-1");

            // Red: 2 of 3 → near-complete
            var set = new PropertySet { Color = PropertyColor.Red };
            var prop1 = new Card { Id = 1, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 };
            var prop2 = new Card { Id = 2, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 };
            set.Cards.Add(prop1);
            set.Cards.Add(prop2);
            bot.PropertySets.Add(set);

            int value = PaymentSolver.CardStrategicValue(bot, prop1);
            Assert.Equal(30, value);
        }

        [Fact]
        public void CardStrategicValue_SinglePropertyInSetIsLow()
        {
            var bot = CreatePlayer("bot-1");

            // Red: 1 of 3 → low value
            var set = new PropertySet { Color = PropertyColor.Red };
            var prop = new Card { Id = 1, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 };
            set.Cards.Add(prop);
            bot.PropertySets.Add(set);

            int value = PaymentSolver.CardStrategicValue(bot, prop);
            // 10 + set.Size(1) = 11
            Assert.Equal(11, value);
        }

        [Fact]
        public void CardStrategicValue_CardNotInAnySetIsLow()
        {
            var bot = CreatePlayer("bot-1");

            var card = new Card { Id = 1, CardType = CardType.Property, Color = PropertyColor.Red, MoneyValue = 3 };
            // Card not in any property set on the board

            int value = PaymentSolver.CardStrategicValue(bot, card);
            Assert.Equal(5, value);
        }

        [Fact]
        public void CardStrategicValue_CompleteSetPropertyIncludesRent()
        {
            var bot = CreatePlayer("bot-1");

            // DarkBlue: 2 of 2 → complete, rent = 8
            var set = new PropertySet { Color = PropertyColor.DarkBlue };
            var prop1 = new Card { Id = 1, CardType = CardType.Property, Color = PropertyColor.DarkBlue, MoneyValue = 4 };
            var prop2 = new Card { Id = 2, CardType = CardType.Property, Color = PropertyColor.DarkBlue, MoneyValue = 4 };
            set.Cards.Add(prop1);
            set.Cards.Add(prop2);
            bot.PropertySets.Add(set);

            int value = PaymentSolver.CardStrategicValue(bot, prop1);
            // 50 + rent(8) = 58
            Assert.Equal(58, value);
        }

        // ── Payment prefers bank over properties ────────────────────────

        [Fact]
        public void FindOptimalPayment_PrefersBankOverEqualValueProperty()
        {
            var bot = CreatePlayer("bot-1");

            // Bank: $3
            bot.Bank.Add(CreateMoney(1, 3));

            // Property: $3 in a partial set
            var prop = new Card
            {
                Id = 2,
                CardType = CardType.Property,
                Color = PropertyColor.Red,
                MoneyValue = 3,
                Name = "Red Prop"
            };
            var set = new PropertySet { Color = PropertyColor.Red };
            set.Cards.Add(prop);
            bot.PropertySets.Add(set);

            var payment = PaymentSolver.FindOptimalPayment(bot, 3);

            // Both cover exactly $3, but bank has strategic value 0
            Assert.Single(payment);
            Assert.Equal(1, payment[0].Id); // bank card, not property
        }

        [Fact]
        public void FindOptimalPayment_PaysWithMultipleBankCards()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 1));
            bot.Bank.Add(CreateMoney(2, 2));
            bot.Bank.Add(CreateMoney(3, 3));

            var payment = PaymentSolver.FindOptimalPayment(bot, 3);

            Assert.Equal(3, payment.Sum(c => c.MoneyValue));
        }

        [Fact]
        public void FindOptimalPayment_HandlesZeroDebt()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 5));

            // 0 owed → total assets (5) > 0, so ExactMinOverpay runs
            // Bitmask starts at mask=1, all subsets have total >= 0
            // The solver picks lowest overpay (which is $5 card, overpay=5)
            // since empty subset (mask=0) is not iterated
            var payment = PaymentSolver.FindOptimalPayment(bot, 0);

            // The solver will pick the single $5 card (lowest card count among all valid subsets)
            Assert.NotEmpty(payment);
        }

        [Fact]
        public void FindOptimalPayment_InsolventWithProperties()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 1));

            var prop = new Card
            {
                Id = 2,
                CardType = CardType.Property,
                Color = PropertyColor.Brown,
                MoneyValue = 1,
                Name = "Brown Prop"
            };
            var set = new PropertySet { Color = PropertyColor.Brown };
            set.Cards.Add(prop);
            bot.PropertySets.Add(set);

            // Owe 10 but only have 2 total → insolvent
            var payment = PaymentSolver.FindOptimalPayment(bot, 10);

            Assert.Equal(2, payment.Count);
            Assert.Equal(2, payment.Sum(c => c.MoneyValue));
        }

        [Fact]
        public void FindOptimalPayment_MinimizesCardCount()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 5));
            bot.Bank.Add(CreateMoney(2, 3));
            bot.Bank.Add(CreateMoney(3, 2));

            // Owe 5: should use single $5 card, not $3+$2
            var payment = PaymentSolver.FindOptimalPayment(bot, 5);

            Assert.Single(payment);
            Assert.Equal(5, payment[0].MoneyValue);
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
