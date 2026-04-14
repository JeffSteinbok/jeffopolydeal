using JeffopolyDeal.Hubs;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using System.Threading.Tasks;
using Xunit;

namespace JeffopolyDeal.Tests
{
    public class GameCacheTests
    {
        [Fact]
        public async Task EndGame_RemovesGameStateFromCache()
        {
            var hubContext = Substitute.For<IHubContext<GameHub>>();
            var cache = new GameCache(hubContext);

            const string code = "ABCD";
            cache.CreateGame(code);
            await cache.JoinGameAsync("conn-1", code, "Alice", "player-1");

            await cache.EndGameAsync("conn-1");

            var canRejoin = await cache.RejoinGameAsync("conn-2", code, "Bob", "player-2");
            Assert.False(canRejoin);
        }
    }
}
