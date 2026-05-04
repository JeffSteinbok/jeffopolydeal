using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for playing money cards and property cards.
    /// </summary>
    public class MoneyAndPropertyTests
    {
        [Fact]
        public async Task PlayMoney_AddsToBank()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var money = h.FindMoneyInHand(p1);
            if (money == null) return; // No money in hand — skip

            await h.PlayCardAsync(p1, money.Id, new PlayCardRequest());
            var ps = h.GetPlayerState(p1, p1);
            Assert.Contains(ps!.Bank, c => c.Id == money.Id);
        }

        [Fact]
        public async Task PlayProperty_AddsToPropertySet()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var prop = h.FindPropertyInHand(p1);
            if (prop == null) return;

            await h.PlayCardAsync(p1, prop.Id, new PlayCardRequest());
            var ps = h.GetPlayerState(p1, p1);
            Assert.Contains(ps!.PropertySets, s => s.Cards.Any(c => c.Id == prop.Id));
        }

        [Fact]
        public async Task PlayActionCardAsMoney_AddsToBank()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var action = h.FindCardInHand(p1, CardType.Action);
            if (action == null) return;

            await h.PlayAsMoney(p1, action.Id);
            var ps = h.GetPlayerState(p1, p1);
            Assert.Contains(ps!.Bank, c => c.Id == action.Id);
        }

        [Fact]
        public async Task PlayRentCardAsMoney_AddsToBank()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var rent = h.FindRentInHand(p1);
            if (rent == null) return;

            await h.PlayAsMoney(p1, rent.Id);
            var ps = h.GetPlayerState(p1, p1);
            Assert.Contains(ps!.Bank, c => c.Id == rent.Id);
        }

        [Fact]
        public async Task PlayCard_CountsAsPlay()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            Assert.Equal(0, h.GetPlaysUsed(p1));

            // Find a card that can be banked as money (money, action, or rent — not property/wildcard)
            var card = h.FindCardInHand(p1, CardType.Money)
                ?? h.FindCardInHand(p1, CardType.Action)
                ?? h.FindCardInHand(p1, CardType.Rent);
            if (card == null) return; // extremely rare: hand is all properties

            await h.PlayAsMoney(p1, card.Id);
            Assert.Equal(1, h.GetPlaysUsed(p1));
        }
    }
}
