using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests for payment mechanics: paying with bank, property, overpayment, nothing to pay.
    /// </summary>
    public class PaymentTests
    {
        [Fact]
        public async Task Payment_BankCardsGoToReceiverBank()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var bankCard = h.PlaceMoneyInBank(p2, 3);
            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3, "Debt Collector");

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest { TargetPlayerId = p2 });
            await h.RespondAsync(p2, new ActionResponse { PaymentCardIds = new List<int> { bankCard.Id } });

            // Bank card should be in P1's bank
            var p1State = h.GetPlayerState(p1, p1);
            Assert.Contains(p1State!.Bank, c => c.Id == bankCard.Id);
        }

        [Fact]
        public async Task Payment_PropertyGoesToReceiverPropertyArea()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var propSet = h.PlacePropertyOnBoard(p2, PropertyColor.Green, 1);
            var propCard = propSet.Cards[0];
            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3, "Debt Collector");

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest { TargetPlayerId = p2 });
            await h.RespondAsync(p2, new ActionResponse { PaymentCardIds = new List<int> { propCard.Id } });

            // Property should be in P1's property area, NOT bank
            var p1State = h.GetPlayerState(p1, p1);
            Assert.Contains(p1State!.PropertySets, s => s.Cards.Any(c => c.Id == propCard.Id));
            Assert.DoesNotContain(p1State.Bank, c => c.Id == propCard.Id);
        }

        [Fact]
        public async Task Payment_NothingToPay_AutoCompletes()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // P2 has nothing on the table
            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3, "Debt Collector");

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest { TargetPlayerId = p2 });

            // P2 responds with empty payment
            await h.RespondAsync(p2, new ActionResponse { PaymentCardIds = new List<int>() });

            Assert.Equal(GamePhase.Play, h.GetPhase(p1));
        }

        [Fact]
        public async Task Payment_MixedBankAndProperty()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            var bankCard = h.PlaceMoneyInBank(p2, 2);
            var propSet = h.PlacePropertyOnBoard(p2, PropertyColor.Red, 1);
            var propCard = propSet.Cards[0];
            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3, "Debt Collector");

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest { TargetPlayerId = p2 });
            await h.RespondAsync(p2, new ActionResponse
            {
                PaymentCardIds = new List<int> { bankCard.Id, propCard.Id }
            });

            var p1State = h.GetPlayerState(p1, p1);
            Assert.Contains(p1State!.Bank, c => c.Id == bankCard.Id);
            Assert.Contains(p1State.PropertySets, s => s.Cards.Any(c => c.Id == propCard.Id));
        }

        [Fact]
        public async Task Payment_Birthday_AllPlayersRespond()
        {
            var h = new TestGameHarness();
            var (p1, p2, p3) = await h.SetupThreePlayerGameAsync();
            await h.DrawAsync(p1);

            var p2Bank = h.PlaceMoneyInBank(p2, 2);
            var p3Bank = h.PlaceMoneyInBank(p3, 2);

            var bday = h.InjectAction(p1, ActionType.ItsMyBirthday, 2);

            await h.PlayCardAsync(p1, bday.Id, new PlayCardRequest());

            Assert.Equal(GamePhase.AwaitingResponse, h.GetPhase(p1));

            // P2 pays
            await h.RespondAsync(p2, new ActionResponse { PaymentCardIds = new List<int> { p2Bank.Id } });

            // Still awaiting P3
            Assert.Equal(GamePhase.AwaitingResponse, h.GetPhase(p1));

            // P3 pays
            await h.RespondAsync(p3, new ActionResponse { PaymentCardIds = new List<int> { p3Bank.Id } });

            // Now back to play
            Assert.Equal(GamePhase.Play, h.GetPhase(p1));
        }

        [Fact]
        public async Task Payment_EmptyBoardPaysNothing()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // P2 has nothing on the table at all
            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3);

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest { TargetPlayerId = p2 });
            await h.RespondAsync(p2, new ActionResponse());

            // Should resolve back to Play
            Assert.Equal(GamePhase.Play, h.GetPhase(p1));
        }

        [Fact]
        public async Task Payment_Insolvent_AutoTakesEverything()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // P2 has only M2 but owes M5 — insolvent
            var bankCard = h.PlaceMoneyInBank(p2, 2);
            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3, "Debt Collector");

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest { TargetPlayerId = p2 });
            // Send empty payment — server should auto-take everything
            await h.RespondAsync(p2, new ActionResponse { PaymentCardIds = new List<int>() });

            // Game continues
            Assert.Equal(GamePhase.Play, h.GetPhase(p1));

            // P1 received P2's bank card
            var p1State = h.GetPlayerState(p1, p1);
            Assert.Contains(p1State!.Bank, c => c.Id == bankCard.Id);
        }

        [Fact]
        public async Task Payment_Insolvent_AutoTakesPropertyToo()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            // P2 has M1 in bank and a property (M0 money value) — still insolvent vs M5 debt
            var bankCard = h.PlaceMoneyInBank(p2, 1);
            var propSet = h.PlacePropertyOnBoard(p2, PropertyColor.Brown, 1);
            var propCard = propSet.Cards[0];
            var dc = h.InjectAction(p1, ActionType.DebtCollector, 3, "Debt Collector");

            await h.PlayCardAsync(p1, dc.Id, new PlayCardRequest { TargetPlayerId = p2 });
            await h.RespondAsync(p2, new ActionResponse { PaymentCardIds = new List<int>() });

            Assert.Equal(GamePhase.Play, h.GetPhase(p1));

            var p1State = h.GetPlayerState(p1, p1);
            Assert.Contains(p1State!.Bank, c => c.Id == bankCard.Id);
            Assert.Contains(p1State.PropertySets, s => s.Cards.Any(c => c.Id == propCard.Id));
        }

        [Fact]
        public async Task RentPayment_PlayerHasWildcard_WildcardNotUsedAsPayment()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlacePropertyOnBoard(p1, PropertyColor.Brown, 1);
            var rent = h.InjectRent(p1, PropertyColor.LightBlue, PropertyColor.Brown);

            var payer = h.Game.GetPlayer(p2)!;
            var wildcard = h.Game.GetDeck().CreateCard(
                CardType.PropertyWildcard, 0, "Green/DarkBlue Wildcard",
                color: PropertyColor.Green, altColor: PropertyColor.DarkBlue);
            wildcard.ActiveColor = PropertyColor.Green;
            payer.GetOrCreatePropertySet(PropertyColor.Green).Cards.Add(wildcard);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest { RentColor = PropertyColor.Brown });
            await h.RespondAsync(p2, new ActionResponse { PaymentCardIds = new List<int>() });

            Assert.Equal(GamePhase.Play, h.GetPhase(p1));

            var p2PlayerAfter = h.Game.GetPlayer(p2)!;
            Assert.Contains(p2PlayerAfter.PropertySets.SelectMany(s => s.Cards), c => c.Id == wildcard.Id);
        }

        [Fact]
        public async Task RentPayment_SelectingWildcardCard_IsRejected()
        {
            var h = new TestGameHarness();
            var (p1, p2) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(p1);

            h.PlacePropertyOnBoard(p1, PropertyColor.Brown, 1);
            var rent = h.InjectRent(p1, PropertyColor.LightBlue, PropertyColor.Brown);

            var payer = h.Game.GetPlayer(p2)!;
            var bankCard = h.PlaceMoneyInBank(p2, 1);
            var wildcard = h.Game.GetDeck().CreateCard(
                CardType.PropertyWildcard, 0, "Green/DarkBlue Wildcard",
                color: PropertyColor.Green, altColor: PropertyColor.DarkBlue);
            wildcard.ActiveColor = PropertyColor.Green;
            payer.GetOrCreatePropertySet(PropertyColor.Green).Cards.Add(wildcard);

            await h.PlayCardAsync(p1, rent.Id, new PlayCardRequest { RentColor = PropertyColor.Brown });
            await h.RespondAsync(p2, new ActionResponse { PaymentCardIds = new List<int> { wildcard.Id } });

            Assert.Equal(GamePhase.AwaitingResponse, h.GetPhase(p1));
            var pending = h.GetPendingAction(p1);
            Assert.Contains(p2, pending!.TargetPlayerIds);

            var p2PlayerAfter = h.Game.GetPlayer(p2)!;
            Assert.Contains(p2PlayerAfter.PropertySets.SelectMany(s => s.Cards), c => c.Id == wildcard.Id);
            Assert.Contains(p2PlayerAfter.Bank, c => c.Id == bankCard.Id);
        }
    }
}
