using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for action cards: Pass Go, Debt Collector, Birthday, Sly Deal, Force Deal, Deal Breaker.
    /// </summary>
    public class ActionCardTests
    {
        [Fact]
        public async Task PassGo_Draws2Cards()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var passGo = h.InjectAction(p1, ActionType.PassGo, 1, "Pass Go");
            int handBefore = h.GetHand(p1).Count;

            await h.PlayCardAsync(p1, passGo.Id, new PlayCardRequest());

            // Hand should have +2 cards minus the Pass Go card played = net +1
            Assert.Equal(handBefore + 1, h.GetHand(p1).Count);
        }

        [Fact]
        public async Task DebtCollector_ChargesTarget5M()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3, "Debt Collector");

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest { TargetPlayerId = p2 });

            Assert.Equal(GamePhase.AwaitingResponse, h.GetPhase(p1));
            var pending = h.GetPendingAction(p1);
            Assert.Equal(PendingActionType.PayDebtCollector, pending!.Type);
            Assert.Equal(5, pending.Amount);
            Assert.Single(pending.TargetPlayerIds);
            Assert.Equal(p2, pending.TargetPlayerIds[0]);
        }

        [Fact]
        public async Task DebtCollector_RequiresTarget()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3, "Debt Collector");
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest()); // No target
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1)); // Should not have played
        }

        [Fact]
        public async Task Birthday_ChargesAllPlayers2M()
        {
            var h = new TestGameHarness();
            var (p1, p2, p3) = await h.SetupThreePlayerGameAsync();
            await h.DrawAsync(p1);

            var bday = h.InjectAction(p1, ActionType.ItsMyBirthday, 2, "It's My Birthday");

            await h.PlayCardAsync(p1, bday.Id, new PlayCardRequest());

            var pending = h.GetPendingAction(p1);
            Assert.Equal(PendingActionType.PayBirthday, pending!.Type);
            Assert.Equal(2, pending.Amount);
            Assert.Equal(2, pending.TargetPlayerIds.Count);
            Assert.DoesNotContain(p1, pending.TargetPlayerIds);
        }

        [Fact]
        public async Task SlyDeal_StealsProperty()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // Give P2 a stealable property (not complete set)
            var targetSet = h.PlacePropertyOnBoard(p2, PropertyColor.Green, 1);
            var targetCard = targetSet.Cards[0];

            var sly = h.InjectAction(p1, ActionType.SlyDeal, 3, "Sly Deal");

            await h.PlayCardAsync(p1, sly.Id, new PlayCardRequest
            {
                TargetPlayerId = p2,
                TargetCardId = targetCard.Id
            });

            Assert.Equal(GamePhase.AwaitingResponse, h.GetPhase(p1));
            var pending = h.GetPendingAction(p1);
            Assert.Equal(PendingActionType.RespondToSlyDeal, pending!.Type);

            // P2 accepts (no JSN)
            await h.RespondAsync(p2, new ActionResponse());

            // Card should now be in P1's property area
            Assert.Equal(GamePhase.Play, h.GetPhase(p1));
            var p1State = h.GetPlayerState(p1, p1);
            Assert.Contains(p1State!.PropertySets, s => s.Cards.Any(c => c.Id == targetCard.Id));
        }

        [Fact]
        public async Task SlyDeal_CantStealFromCompleteSet()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var completeSet = h.PlaceCompleteSet(p2, PropertyColor.Brown);
            var targetCard = completeSet.Cards[0];

            var sly = h.InjectAction(p1, ActionType.SlyDeal, 3, "Sly Deal");
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, sly.Id, new PlayCardRequest
            {
                TargetPlayerId = p2,
                TargetCardId = targetCard.Id
            });

            // Should not have played — card is in a complete set
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1));
        }

        [Fact]
        public async Task ForceDeal_SwapsProperties()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var p1Set = h.PlacePropertyOnBoard(p1, PropertyColor.Red, 1);
            var p1Card = p1Set.Cards[0];
            var p2Set = h.PlacePropertyOnBoard(p2, PropertyColor.Green, 1);
            var p2Card = p2Set.Cards[0];

            var force = h.InjectAction(p1, ActionType.ForceDeal, 3, "Force Deal");

            await h.PlayCardAsync(p1, force.Id, new PlayCardRequest
            {
                TargetPlayerId = p2,
                TargetCardId = p2Card.Id,
                OfferedCardId = p1Card.Id
            });

            // P2 accepts
            await h.RespondAsync(p2, new ActionResponse());

            // P1 should have green card, P2 should have red card
            var p1State = h.GetPlayerState(p1, p1);
            var p2State = h.GetPlayerState(p1, p2);
            Assert.Contains(p1State!.PropertySets, s => s.Cards.Any(c => c.Id == p2Card.Id));
            Assert.Contains(p2State!.PropertySets, s => s.Cards.Any(c => c.Id == p1Card.Id));
        }

        [Fact]
        public async Task ForceDeal_CantTargetCompleteSet()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var p1Set = h.PlacePropertyOnBoard(p1, PropertyColor.Red, 1);
            var p1Card = p1Set.Cards[0];
            var completeSet = h.PlaceCompleteSet(p2, PropertyColor.Brown);
            var p2Card = completeSet.Cards[0];

            var force = h.InjectAction(p1, ActionType.ForceDeal, 3, "Force Deal");
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, force.Id, new PlayCardRequest
            {
                TargetPlayerId = p2,
                TargetCardId = p2Card.Id,
                OfferedCardId = p1Card.Id
            });

            Assert.Equal(playsBefore, h.GetPlaysUsed(p1));
        }

        [Fact]
        public async Task DealBreaker_StealsCompleteSet()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p2, PropertyColor.Brown);

            var db = h.InjectAction(p1, ActionType.DealBreaker, 5, "Deal Breaker");

            await h.PlayCardAsync(p1, db.Id, new PlayCardRequest
            {
                TargetPlayerId = p2,
                TargetSetColor = PropertyColor.Brown
            });

            // P2 accepts
            await h.RespondAsync(p2, new ActionResponse());

            // P1 should have the brown set
            var p1State = h.GetPlayerState(p1, p1);
            Assert.True(p1State!.PropertySets.Any(s => s.Color == PropertyColor.Brown && s.IsComplete));

            // P2 should no longer have it
            var p2State = h.GetPlayerState(p1, p2);
            Assert.False(p2State!.PropertySets.Any(s => s.Color == PropertyColor.Brown));
        }

        [Fact]
        public async Task DealBreaker_IncludesHouseAndHotel()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var set = h.PlaceCompleteSet(p2, PropertyColor.Red);
            set.HasHouse = true;
            set.HasHotel = true;

            var db = h.InjectAction(p1, ActionType.DealBreaker, 5, "Deal Breaker");

            await h.PlayCardAsync(p1, db.Id, new PlayCardRequest
            {
                TargetPlayerId = p2,
                TargetSetColor = PropertyColor.Red
            });
            await h.RespondAsync(p2, new ActionResponse());

            var p1State = h.GetPlayerState(p1, p1);
            var redSet = p1State!.PropertySets.FirstOrDefault(s => s.Color == PropertyColor.Red);
            Assert.NotNull(redSet);
            Assert.True(redSet!.HasHouse);
            Assert.True(redSet.HasHotel);
        }

        [Fact]
        public async Task DealBreaker_NoCompleteSet_Wasted()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // P2 has no complete sets
            h.PlacePropertyOnBoard(p2, PropertyColor.Green, 1);

            var db = h.InjectAction(p1, ActionType.DealBreaker, 5, "Deal Breaker");
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, db.Id, new PlayCardRequest
            {
                TargetPlayerId = p2,
                TargetSetColor = PropertyColor.Green
            });

            // Should not play — no complete set of that color
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1));
        }

        [Fact]
        public async Task House_RequiresCompleteSet()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlacePropertyOnBoard(p1, PropertyColor.Green, 2); // Incomplete (needs 3)
            var house = h.InjectAction(p1, ActionType.House, 3, "House");
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, house.Id, new PlayCardRequest { TargetSetColor = PropertyColor.Green });
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1));
        }

        [Fact]
        public async Task House_PlacesOnCompleteSet()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p1, PropertyColor.Green);
            var house = h.InjectAction(p1, ActionType.House, 3, "House");

            await h.PlayCardAsync(p1, house.Id, new PlayCardRequest { TargetSetColor = PropertyColor.Green });

            var p1State = h.GetPlayerState(p1, p1);
            var greenSet = p1State!.PropertySets.First(s => s.Color == PropertyColor.Green);
            Assert.True(greenSet.HasHouse);
        }

        [Fact]
        public async Task Hotel_RequiresHouse()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p1, PropertyColor.Green);
            var hotel = h.InjectAction(p1, ActionType.Hotel, 4, "Hotel");
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, hotel.Id, new PlayCardRequest { TargetSetColor = PropertyColor.Green });
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1)); // Can't place hotel without house
        }

        [Fact]
        public async Task Hotel_PlacesOnSetWithHouse()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var set = h.PlaceCompleteSet(p1, PropertyColor.Green);
            set.HasHouse = true;
            var hotel = h.InjectAction(p1, ActionType.Hotel, 4, "Hotel");

            await h.PlayCardAsync(p1, hotel.Id, new PlayCardRequest { TargetSetColor = PropertyColor.Green });

            var p1State = h.GetPlayerState(p1, p1);
            var greenSet = p1State!.PropertySets.First(s => s.Color == PropertyColor.Green);
            Assert.True(greenSet.HasHotel);
        }

        [Fact]
        public async Task House_NotOnRailroad()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p1, PropertyColor.Railroad);
            var house = h.InjectAction(p1, ActionType.House, 3, "House");
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, house.Id, new PlayCardRequest { TargetSetColor = PropertyColor.Railroad });
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1));
        }

        [Fact]
        public async Task House_NotOnUtility()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlaceCompleteSet(p1, PropertyColor.Utility);
            var house = h.InjectAction(p1, ActionType.House, 3, "House");
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, house.Id, new PlayCardRequest { TargetSetColor = PropertyColor.Utility });
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1));
        }

        [Fact]
        public async Task JustSayNo_CantBePlayedProactively()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var jsn = h.InjectJustSayNo(p1);
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, jsn.Id, new PlayCardRequest());
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1)); // Can't play proactively
        }

        [Fact]
        public async Task DoubleTheRent_CantBePlayedAlone()
        {
            var h = new TestGameHarness();
            var (p1, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var dtr = h.InjectDoubleTheRent(p1);
            int playsBefore = h.GetPlaysUsed(p1);

            await h.PlayCardAsync(p1, dtr.Id, new PlayCardRequest());
            Assert.Equal(playsBefore, h.GetPlaysUsed(p1));
        }
    }
}
