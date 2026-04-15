using JeffopolyDeal.Hubs;
using JeffopolyDeal.Models;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JeffopolyDeal.Tests
{
    public class GameCacheTests
    {
        private static IHubContext<GameHub> CreateMockHubContext(ConcurrentDictionary<string, GameState> states)
        {
            var hubContext = Substitute.For<IHubContext<GameHub>>();
            var clients = Substitute.For<IHubClients>();
            var groups = Substitute.For<IGroupManager>();

            hubContext.Clients.Returns(clients);
            hubContext.Groups.Returns(groups);

            clients.Client(Arg.Any<string>()).Returns(callInfo =>
            {
                var connId = callInfo.Arg<string>();
                var proxy = Substitute.For<ISingleClientProxy>();
                proxy.SendCoreAsync(
                    Arg.Any<string>(),
                    Arg.Any<object?[]>(),
                    Arg.Any<CancellationToken>()
                ).Returns(callInfo2 =>
                {
                    var method = callInfo2.Arg<string>();
                    var args = callInfo2.Arg<object?[]>();
                    if (method == "gameStateUpdated" && args.Length > 0 && args[0] is GameState state)
                    {
                        states[connId] = state;
                    }
                    return Task.CompletedTask;
                });
                return proxy;
            });

            groups.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            groups.RemoveFromGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            return hubContext;
        }

        [Fact]
        public async Task EndGame_RemovesGameStateFromCache()
        {
            var hubContext = CreateMockHubContext(new ConcurrentDictionary<string, GameState>());
            var cache = new GameCache(hubContext);

            const string code = "ABCD";
            cache.CreateGame(code);
            await cache.JoinGameAsync("conn-1", code, "Alice", "player-1");

            await cache.EndGameAsync("conn-1");

            var canRejoin = await cache.RejoinGameAsync("conn-2", code, "Bob", "player-2");
            Assert.False(canRejoin);
        }

        [Fact]
        public async Task StartGame_WithAddBots_AllowsSingleHumanToStart()
        {
            var states = new ConcurrentDictionary<string, GameState>();
            var hubContext = CreateMockHubContext(states);
            var cache = new GameCache(hubContext);

            const string code = "WXYZ";
            cache.CreateGame(code);
            await cache.JoinGameAsync("conn-1", code, "Alice", "player-1");

            await cache.StartGameAsync(code, allowSinglePlayer: false, populateBoards: false, addBots: true);

            Assert.True(states.TryGetValue("conn-1", out var state));
            Assert.NotNull(state);
            Assert.Equal(GamePhase.Draw, state!.Phase);
            Assert.True(state.Players.Count >= 4);
        }
    }
}
