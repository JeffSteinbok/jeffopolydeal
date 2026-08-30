using JeffopolyDeal.Notifications;
using NSubstitute;
using System.Threading;
using Xunit;

namespace JeffopolyDeal.Tests
{
    public class TurnNotificationTests
    {
        [Fact]
        public async Task StartGame_NotifiesInitialHumanTurnOnce()
        {
            var notifications = Substitute.For<ITurnNotificationService>();
            var h = new TestGameHarness(turnNotificationService: notifications);
            await h.AddPlayerAsync("Alice");
            await h.AddPlayerAsync("Bob");

            await h.Game.StartGameAsync(allowSinglePlayer: false, startingPlayerIndex: 0);
            await h.Game.BroadcastGameStateAsync();

            await notifications.Received(1).NotifyTurnAsync(
                "player-Alice",
                "Alice",
                "TEST",
                "Alice",
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EndTurn_NotifiesNextHumanTurnOnce()
        {
            var notifications = Substitute.For<ITurnNotificationService>();
            var h = new TestGameHarness(turnNotificationService: notifications);
            var (alice, _) = await h.SetupTwoPlayerGameAsync();
            await h.DrawAsync(alice);

            await h.EndTurnAsync(alice);
            await h.Game.BroadcastGameStateAsync();

            await notifications.Received(1).NotifyTurnAsync(
                "player-Bob",
                "Bob",
                "TEST",
                "Alice",
                Arg.Any<CancellationToken>());
        }
    }
}
