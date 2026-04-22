using JeffopolyDeal;
using JeffopolyDeal.Models;

namespace JeffopolyDeal.BotAI.Tests
{
    public class PaymentSolverTests
    {
        [Fact]
        public void FindOptimalPayment_PaysExactAmountWhenPossible()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 5));
            bot.Bank.Add(CreateMoney(2, 3));
            bot.Bank.Add(CreateMoney(3, 2));

            var payment = PaymentSolver.FindOptimalPayment(bot, 5);

            Assert.Equal(5, payment.Sum(c => c.MoneyValue));
            Assert.Single(payment);
            Assert.Equal(5, payment[0].MoneyValue);
        }

        [Fact]
        public void FindOptimalPayment_PrefersBankOverProperties()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 3));
            bot.Bank.Add(CreateMoney(2, 2));

            var prop = new Card { Id = 3, CardType = CardType.Property, MoneyValue = 3, Color = PropertyColor.Red };
            var set = new PropertySet { Color = PropertyColor.Red };
            set.Cards.Add(prop);
            bot.PropertySets.Add(set);

            var payment = PaymentSolver.FindOptimalPayment(bot, 3);

            // Should pay with bank $3, not the property
            Assert.All(payment, c => Assert.Contains(c, bot.Bank));
        }

        [Fact]
        public void FindOptimalPayment_MinimizesOverpay()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 1));
            bot.Bank.Add(CreateMoney(2, 1));
            bot.Bank.Add(CreateMoney(3, 2));
            bot.Bank.Add(CreateMoney(4, 5));

            // Owe 3: should pick 1+2=3 (exact) instead of 5 (overpay 2)
            var payment = PaymentSolver.FindOptimalPayment(bot, 3);

            Assert.Equal(3, payment.Sum(c => c.MoneyValue));
        }

        [Fact]
        public void FindOptimalPayment_InsolventPaysEverything()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 1));
            bot.Bank.Add(CreateMoney(2, 2));

            // Owe 10 but only have 3 total
            var payment = PaymentSolver.FindOptimalPayment(bot, 10);

            Assert.Equal(2, payment.Count);
            Assert.Equal(3, payment.Sum(c => c.MoneyValue));
        }

        [Fact]
        public void FindOptimalPayment_ProtectsNearCompleteSets()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 3));

            // Near-complete set (2 of 3 for Red)
            var set = new PropertySet { Color = PropertyColor.Red };
            var prop1 = new Card { Id = 10, CardType = CardType.Property, MoneyValue = 3, Color = PropertyColor.Red };
            var prop2 = new Card { Id = 11, CardType = CardType.Property, MoneyValue = 3, Color = PropertyColor.Red };
            set.Cards.Add(prop1);
            set.Cards.Add(prop2);
            bot.PropertySets.Add(set);

            var payment = PaymentSolver.FindOptimalPayment(bot, 3);

            // Should use bank money, not the near-complete set properties
            Assert.DoesNotContain(prop1, payment);
            Assert.DoesNotContain(prop2, payment);
        }

        [Fact]
        public void CardStrategicValue_BankIsZero()
        {
            var bot = CreatePlayer("bot-1");
            var bankCard = CreateMoney(1, 5);
            bot.Bank.Add(bankCard);

            Assert.Equal(0, PaymentSolver.CardStrategicValue(bot, bankCard));
        }

        [Fact]
        public void CardStrategicValue_CompleteSetPropertyIsHigh()
        {
            var bot = CreatePlayer("bot-1");
            var set = new PropertySet { Color = PropertyColor.Brown };
            var prop1 = new Card { Id = 1, CardType = CardType.Property, MoneyValue = 1, Color = PropertyColor.Brown };
            var prop2 = new Card { Id = 2, CardType = CardType.Property, MoneyValue = 1, Color = PropertyColor.Brown };
            set.Cards.Add(prop1);
            set.Cards.Add(prop2);
            bot.PropertySets.Add(set);

            int value = PaymentSolver.CardStrategicValue(bot, prop1);
            Assert.True(value >= 50, $"Complete set property should have high value, got {value}");
        }

        [Fact]
        public void CardStrategicValue_PartialSetPropertyIsMedium()
        {
            var bot = CreatePlayer("bot-1");
            var set = new PropertySet { Color = PropertyColor.Red };
            var prop = new Card { Id = 1, CardType = CardType.Property, MoneyValue = 3, Color = PropertyColor.Red };
            set.Cards.Add(prop);
            bot.PropertySets.Add(set);

            int value = PaymentSolver.CardStrategicValue(bot, prop);
            Assert.True(value > 0 && value < 50, $"Partial set property should have medium value, got {value}");
        }

        [Fact]
        public void FindOptimalPayment_ExcludesMulticolorWilds()
        {
            var bot = CreatePlayer("bot-1");
            bot.Bank.Add(CreateMoney(1, 5));

            var wild = new Card { Id = 10, CardType = CardType.PropertyWildcard, MoneyValue = 0, IsMulticolorWild = true };
            var set = new PropertySet { Color = PropertyColor.Brown };
            set.Cards.Add(wild);
            bot.PropertySets.Add(set);

            var payment = PaymentSolver.FindOptimalPayment(bot, 3);

            Assert.DoesNotContain(wild, payment);
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
