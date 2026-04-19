using JeffopolyDeal.Hubs;
using Microsoft.AspNetCore.Builder;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSingleton<JeffopolyDeal.GameCache>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    // In dev, the Vite dev server serves the SPA on port 5173.
    // Vite proxies /hub/* back to this .NET server.
    // No need to serve static files or SPA fallback here.
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();

    // In production, serve the Vite build output from wwwroot/
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.UseRouting();
app.MapHub<GameHub>("/hub/game");

// API endpoint: returns the full unshuffled deck for the test/debug page
app.MapGet("/api/deck", (HttpRequest request) =>
{
    var theme = request.Query["theme"].FirstOrDefault();
    var cards = JeffopolyDeal.Models.Deck.GetOrderedDeck(theme);
    return Microsoft.AspNetCore.Http.Results.Json(cards, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    });
});

app.Run();
