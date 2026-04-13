using JeffopolyDeal.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
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

app.Run();
