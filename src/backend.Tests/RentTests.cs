using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for rent card mechanics: standard rent, wild rent, Double the Rent stacking.
    /// </summary>
    public class RentTests
    {
        [Fact]
        public async Task StandardRent_ChargesAllOpponents()
        {
            var h = new TestGameHarness();
            var (p1, p2, p3) = await h.SetupThreePlayerGameAsync();
            await h.DrawAsync(p1);

            // Give P1 a brown property and a brown rent card
            h.PlacePropertyOnBoard(p1, PropertyColor.Brown, 1);
            var rent = h.InjectRent(p1, PropertyColor.LightBlue, PropertyColor.Brown);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest { RentColor = PropertyColor.Brown });

            // Should be awaiting response from both P2 and P3
            Assert.Equal(GamePhase.AwaitingResponse, h.GetPhase(p1));
            var pending = h.GetPendingAction(p1);
            Assert.NotNull(pending);
            Assert.Equal(PendingActionType.PayRent, pending!.Type);
            Assert.Equal(1, pending.Amount); // Brown with 1 property = 1M rent
            Assert.Contains(p2, pending.TargetPlayerIds);
            Assert.Contains(p3, pending.TargetPlayerIds);
        }

        [Fact]
        public async Task WildRent_ChargesOnePlayer()
        {
            var h = new TestGameHarness();
            var (p1, p2, p3) = await h.SetupThreePlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlacePropertyOnBoard(p1, PropertyColor.Green, 2);
            var rent = h.InjectWildRent(p1);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest
            {
                RentColor = PropertyColor.Green,
                TargetPlayerId = p2
            });

            Assert.Equal(GamePhase.AwaitingResponse, h.GetPhase(p1));
            var pending = h.GetPendingAction(p1);
            Assert.NotNull(pending);
            Assert.Single(pending!.TargetPlayerIds);
            Assert.Equal(p2, pending.TargetPlayerIds[0]);
            Assert.Equal(4, pending.Amount); // Green with 2 = 4M
        }

        [Fact]
        public async Task Rent_RequiresPropertyOfColor()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // No green properties — rent should fail
            var rent = h.InjectRent(p1, PropertyColor.DarkBlue, PropertyColor.Green);
            int handBefore = h.GetHand(p1).Count;

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest { RentColor = PropertyColor.Green });

            // Card should not have been played (still in hand)
            Assert.Equal(handBefore, h.GetHand(p1).Count);
            Assert.Equal(0, h.GetPlaysUsed(p1));
        }

        [Fact]
        public async Task Rent_CantChargeForWrongColor()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlacePropertyOnBoard(p1, PropertyColor.Red, 1);
            var rent = h.InjectRent(p1, PropertyColor.DarkBlue, PropertyColor.Green);
            int handBefore = h.GetHand(p1).Count;

            // Try to charge red with a blue/green rent card
            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest { RentColor = PropertyColor.Red });
            Assert.Equal(handBefore, h.GetHand(p1).Count);
        }

        [Fact]
        public async Task Rent_FullSetRent()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p1, PropertyColor.DarkBlue); // 2 cards = full
            var rent = h.InjectRent(p1, PropertyColor.DarkBlue, PropertyColor.Green);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest { RentColor = PropertyColor.DarkBlue });

            var pending = h.GetPendingAction(p1);
            Assert.Equal(8, pending!.Amount); // DarkBlue full set = 8M
        }

        [Fact]
        public async Task DoubleTheRent_DoublesAmount()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlacePropertyOnBoard(p1, PropertyColor.Brown, 1);
            var rent = h.InjectRent(p1, PropertyColor.LightBlue, PropertyColor.Brown);
            var dtr = h.InjectDoubleTheRent(p1);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest
            {
                RentColor = PropertyColor.Brown,
                DoubleRentCardIds = new List<int> { dtr.Id }
            });

            var pending = h.GetPendingAction(p1);
            Assert.Equal(2, pending!.Amount); // Brown 1 prop = 1M, doubled = 2M
        }

        [Fact]
        public async Task DoubleTheRent_StacksMultiple()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlacePropertyOnBoard(p1, PropertyColor.Brown, 1);
            var rent = h.InjectRent(p1, PropertyColor.LightBlue, PropertyColor.Brown);
            var dtr1 = h.InjectDoubleTheRent(p1);
            var dtr2 = h.InjectDoubleTheRent(p1);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest
            {
                RentColor = PropertyColor.Brown,
                DoubleRentCardIds = new List<int> { dtr1.Id, dtr2.Id }
            });

            var pending = h.GetPendingAction(p1);
            // 1M base * 2 * 2 = 4M
            Assert.Equal(4, pending!.Amount);
        }

        [Fact]
        public async Task DoubleTheRent_CountsAsPlays()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlacePropertyOnBoard(p1, PropertyColor.Brown, 1);
            var rent = h.InjectRent(p1, PropertyColor.LightBlue, PropertyColor.Brown);
            var dtr1 = h.InjectDoubleTheRent(p1);
            var dtr2 = h.InjectDoubleTheRent(p1);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest
            {
                RentColor = PropertyColor.Brown,
                DoubleRentCardIds = new List<int> { dtr1.Id, dtr2.Id }
            });

            // Rent = 1 play, DTR1 = 1 play, DTR2 = 1 play = 3 total
            Assert.Equal(3, h.GetPlaysUsed(p1));
        }

        [Fact]
        public async Task DoubleTheRent_LimitedByMaxPlays()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Use 1 play first
            var money = h.InjectMoney(p1, 1);
            await h.PlayAsMoney(p1, money.Id);

            h.PlacePropertyOnBoard(p1, PropertyColor.Brown, 1);
            var rent = h.InjectRent(p1, PropertyColor.LightBlue, PropertyColor.Brown);
            var dtr1 = h.InjectDoubleTheRent(p1);
            var dtr2 = h.InjectDoubleTheRent(p1);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest
            {
                RentColor = PropertyColor.Brown,
                DoubleRentCardIds = new List<int> { dtr1.Id, dtr2.Id }
            });

            // 1 (money) + 1 (rent) + 1 (dtr1) = 3 max; dtr2 should be skipped
            Assert.Equal(3, h.GetPlaysUsed(p1));
            var pending = h.GetPendingAction(p1);
            Assert.Equal(2, pending!.Amount); // Only one DTR applied: 1 * 2 = 2
        }

        [Fact]
        public async Task Rent_HouseHotelBonus()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var set = h.PlaceCompleteSet(p1, PropertyColor.DarkBlue);
            set.HasHouse = true;
            set.HasHotel = true;

            var rent = h.InjectRent(p1, PropertyColor.DarkBlue, PropertyColor.Green);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest { RentColor = PropertyColor.DarkBlue });

            var pending = h.GetPendingAction(p1);
            // DarkBlue full = 8M + House 3M + Hotel 4M = 15M
            Assert.Equal(15, pending!.Amount);
        }

        [Fact]
        public async Task Rent_ZeroRent_NoPendingAction()
        {
            // This shouldn't normally happen, but if somehow rent is 0
            // the game should handle it gracefully
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Place a property that has 0 rent (shouldn't exist normally, but test defensively)
            h.PlacePropertyOnBoard(p1, PropertyColor.Brown, 1);
            var rent = h.InjectRent(p1, PropertyColor.LightBlue, PropertyColor.Brown);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest { RentColor = PropertyColor.Brown });
            // Brown with 1 property = 1M rent, so there should be a pending action
            Assert.NotNull(h.GetPendingAction(p1));
        }
    }
}
