using System;
using System.Collections.Generic;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using JeffopolyDeal.Hubs;
using JeffopolyDeal.Notifications;
using Microsoft.AspNetCore.Builder;
using System.Net.Http;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Application Insights via OpenTelemetry (reads APPLICATIONINSIGHTS_CONNECTION_STRING from config)
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSingleton<IPushTokenStore, PushTokenStore>();
builder.Services.AddHttpClient<ITurnNotificationService, ApnsTurnNotificationService>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // APNs is HTTP/2 only and keeps long-lived connections. App Service was
        // reusing pooled connections Apple had already closed, which surfaces as
        // "The response ended prematurely". Keep them fresh and let more than
        // one exist so a single bad connection cannot stall every notification.
        EnableMultipleHttp2Connections = true,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
        ConnectTimeout = TimeSpan.FromSeconds(10),
        KeepAlivePingDelay = TimeSpan.FromSeconds(20),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
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

// Lets iOS open shared game links in the installed app instead of Safari.
// Served from an endpoint rather than wwwroot because the file has no
// extension, and static file serving will not guess a content type for it.
app.MapGet("/.well-known/apple-app-site-association", () =>
    Microsoft.AspNetCore.Http.Results.Json(new
    {
        applinks = new
        {
            details = new[]
            {
                new
                {
                    appIDs = new[] { "Y7KVX7666P.net.steinbok.jeffopolydeal" },
                    components = new[]
                    {
                        // Shared invites look like https://host/?join=ABCD
                        new { query = new { join = "?*" } },
                    },
                },
            },
        },
    }, contentType: "application/json"));

// Diagnostic: can this host reach APNs over HTTP/2 at all? Turn notifications
// failed here for a long time with "The response ended prematurely", and there
// was no way to tell a platform limitation from a bug in our own client.
app.MapGet("/api/apns-selftest", async () =>
{
    var results = new List<object>();

    foreach (var (label, version, policy) in new[]
    {
        ("http2-exact", System.Net.HttpVersion.Version20, HttpVersionPolicy.RequestVersionExact),
        ("http2-orhigher", System.Net.HttpVersion.Version20, HttpVersionPolicy.RequestVersionOrHigher),
        ("http11", System.Net.HttpVersion.Version11, HttpVersionPolicy.RequestVersionOrLower),
    })
    {
        using var handler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.push.apple.com/3/device/0000")
            {
                Version = version,
                VersionPolicy = policy,
                Content = new StringContent("{}"),
            };
            using var res = await client.SendAsync(req);
            results.Add(new { label, ok = true, status = (int)res.StatusCode, negotiated = res.Version.ToString() });
        }
        catch (Exception ex)
        {
            results.Add(new { label, ok = false, error = ex.GetType().Name, detail = ex.InnerException?.Message ?? ex.Message });
        }
    }

    return Microsoft.AspNetCore.Http.Results.Json(new { os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, results });
});

// API endpoint: returns game configuration (rent tables, set sizes) — single source of truth
app.MapGet("/api/gameconfig", () =>
{
    var config = JeffopolyDeal.Models.GameConfigData.FromStatic();
    return Microsoft.AspNetCore.Http.Results.Json(config, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    });
});

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
