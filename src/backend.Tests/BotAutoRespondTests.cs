using JeffopolyDeal.Models;
using Xunit;

namespace JeffopolyDeal.Tests
{
    /// <summary>
    /// Tests that AI (bot) players automatically respond to prompts so the game doesn't stall.
    /// </summary>
    public class BotAutoRespondTests
    {
        /// <summary>
        /// Verify the bot is no longer a pending-action target.
        /// After auto-respond, the bot should not appear in TargetPlayerIds (game not stuck on bot).
        /// </summary>
        private static void AssertBotNotStuck(TestGameHarness h, string humanConn, string botConn)
        {
            var pending = h.GetPendingAction(humanConn);
            if (pending != null)
            {
                Assert.DoesNotContain(botConn, pending.TargetPlayerIds);
            }
        }

        [Fact]
        public async Task WhenHumanPlaysRentTargetingBot_BotAutoRespondsAndGameContinues()
        {
            var h = new TestGameHarness();
            var human = await h.AddPlayerAsync("Human");
            var bot = await h.AddBotAsync("BotAlice");
            await h.Game.StartGameAsync(allowSinglePlayer: false);

            await h.DrawAsync(human);

            // Clear the bot's initial hand to ensure deterministic behaviour (no JSN)
            h.Game.GetPlayer(bot)!.Hand.Clear();

            // Give human a property set and rent card
            h.PlacePropertyOnBoard(human, PropertyColor.Brown, 1);
            var rent = h.InjectRent(human, PropertyColor.LightBlue, PropertyColor.Brown);

            await h.PlayCardAsync(human, rent.Id, new PlayCardRequest { RentColor = PropertyColor.Brown });

            // The bot should have auto-responded and is not stuck as a pending target.
            // With no JSN in hand and nothing to pay, the action fully resolves.
            AssertBotNotStuck(h, human, bot);
            Assert.Equal(GamePhase.Play, h.GetPhase(human));
        }

        [Fact]
        public async Task WhenHumanPlaysSlyDealTargetingBot_BotAutoRespondsAndGameContinues()
        {
            var h = new TestGameHarness();
            var human = await h.AddPlayerAsync("Human");
            var bot = await h.AddBotAsync("BotBob");
            await h.Game.StartGameAsync(allowSinglePlayer: false);

            await h.DrawAsync(human);

            // Clear the bot's initial hand to ensure deterministic behaviour (no JSN)
            h.Game.GetPlayer(bot)!.Hand.Clear();

            // Place a property on the bot's board (the target of Sly Deal)
            h.PlacePropertyOnBoard(bot, PropertyColor.Green, 1);

            // Give human a Sly Deal card
            var slyDeal = h.InjectAction(human, ActionType.SlyDeal, 3, "Sly Deal");

            // Find the bot's stealable property
            var botPlayer = h.Game.GetPlayer(bot);
            Assert.NotNull(botPlayer);
            var stealable = botPlayer!.GetStealableProperties();
            Assert.NotEmpty(stealable);

            await h.PlayCardAsync(human, slyDeal.Id, new PlayCardRequest
            {
                TargetPlayerId = bot,
                TargetCardId = stealable[0].Id,
            });

            // The bot should have auto-responded and is not stuck as a pending target
            AssertBotNotStuck(h, human, bot);
            Assert.NotEqual(GamePhase.AwaitingResponse, h.GetPhase(human));
        }

        [Fact]
        public async Task WhenHumanPlaysDebtCollectorTargetingBot_BotAutoRespondsAndGameContinues()
        {
            var h = new TestGameHarness();
            var human = await h.AddPlayerAsync("Human");
            var bot = await h.AddBotAsync("BotCharlie");
            await h.Game.StartGameAsync(allowSinglePlayer: false);

            await h.DrawAsync(human);

            // Clear the bot's initial hand to ensure deterministic behaviour (no JSN)
            h.Game.GetPlayer(bot)!.Hand.Clear();

            // Give the bot some money to pay with
            h.PlaceMoneyInBank(bot, 5);

            // Give human a Debt Collector card
            var debtCollector = h.InjectAction(human, ActionType.DebtCollector, 3, "Debt Collector");

            await h.PlayCardAsync(human, debtCollector.Id, new PlayCardRequest
            {
                TargetPlayerId = bot,
            });

            // The bot should have auto-responded and is not stuck as a pending target
            AssertBotNotStuck(h, human, bot);
            Assert.NotEqual(GamePhase.AwaitingResponse, h.GetPhase(human));
        }

        [Fact]
        public async Task WhenHumanPlaysBirthday_AllBotTargetsAutoRespond()
        {
            var h = new TestGameHarness();
            var human = await h.AddPlayerAsync("Human");
            var bot1 = await h.AddBotAsync("BotDiana");
            var bot2 = await h.AddBotAsync("BotEve");
            await h.Game.StartGameAsync(allowSinglePlayer: false);

            await h.DrawAsync(human);

            // Clear both bots' hands to ensure deterministic behaviour (no JSN)
            h.Game.GetPlayer(bot1)!.Hand.Clear();
            h.Game.GetPlayer(bot2)!.Hand.Clear();

            // Give both bots some money to pay with
            h.PlaceMoneyInBank(bot1, 2);
            h.PlaceMoneyInBank(bot2, 2);

            // Give human a Birthday card
            var birthday = h.InjectAction(human, ActionType.ItsMyBirthday, 2, "It's My Birthday!");

            await h.PlayCardAsync(human, birthday.Id, new PlayCardRequest());

            // Both bots should have auto-responded and are not stuck as pending targets
            AssertBotNotStuck(h, human, bot1);
            AssertBotNotStuck(h, human, bot2);
            Assert.NotEqual(GamePhase.AwaitingResponse, h.GetPhase(human));
        }
    }
}
