using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JeffopolyDeal.Notifications;

public sealed class ApnsTurnNotificationService : ITurnNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IPushTokenStore _tokenStore;
    private readonly ILogger<ApnsTurnNotificationService> _logger;
    private readonly string? _teamId;
    private readonly string? _keyId;
    private readonly string? _privateKey;
    private readonly string _topic;
    private readonly object _tokenLock = new();
    private string? _providerToken;
    private DateTimeOffset _providerTokenExpiresAt;

    public ApnsTurnNotificationService(
        HttpClient httpClient,
        IPushTokenStore tokenStore,
        IConfiguration configuration,
        ILogger<ApnsTurnNotificationService> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _logger = logger;
        _teamId = configuration["APNS:TEAM_ID"];
        _keyId = configuration["APNS:KEY_ID"];
        _privateKey = configuration["APNS:PRIVATE_KEY"]?.Replace("\\n", "\n", StringComparison.Ordinal);
        _topic = configuration["APNS:TOPIC"] ?? "net.steinbok.jeffopolydeal";

        var useSandbox = bool.TryParse(configuration["APNS:USE_SANDBOX"], out var sandbox) && sandbox;
        _httpClient.BaseAddress = new Uri(
            useSandbox ? "https://api.sandbox.push.apple.com" : "https://api.push.apple.com");
        _httpClient.DefaultRequestVersion = HttpVersion.Version20;
        _httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
    }

    public async Task NotifyTurnAsync(
        string playerId,
        string playerName,
        string gameCode,
        string hostName,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("APNs turn notification skipped: APNS configuration is incomplete");
            return;
        }

        var tokens = _tokenStore.GetTokens(playerId);
        if (tokens.Count == 0)
        {
            // The common real-world failure, and it used to be silent.
            _logger.LogInformation(
                "APNs turn notification skipped: no device tokens registered for player {PlayerId} in {GameCode}",
                playerId, gameCode);
            return;
        }

        _logger.LogInformation(
            "Sending APNs turn notification to {Count} device(s) for player {PlayerId} in {GameCode}",
            tokens.Count, playerId, gameCode);

        var providerToken = GetProviderToken();
        foreach (var deviceToken in tokens)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"/3/device/{deviceToken}")
                {
                    // Set on the request, not just as a client default. A request
                    // defaults to HTTP/1.1, and APNs speaks only HTTP/2 — it closes
                    // the connection, which surfaces as "the response ended
                    // prematurely" from the HTTP/1.1 header parser.
                    Version = HttpVersion.Version20,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", providerToken);
                request.Headers.TryAddWithoutValidation("apns-topic", _topic);
                request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
                request.Headers.TryAddWithoutValidation("apns-priority", "10");
                request.Content = JsonContent.Create(new
                {
                    aps = new
                    {
                        alert = new
                        {
                            title = "It's your turn!",
                            body = $"Draw cards in {hostName}'s Game."
                        },
                        sound = "default",
                        thread_id = gameCode
                    },
                    gameCode
                });

                using var response = await SendWithRetryAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                    continue;

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "APNs rejected turn notification for {PlayerId} with {StatusCode}: {Response}",
                    playerId, response.StatusCode, responseBody);

                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Gone)
                    _tokenStore.Remove(playerId, deviceToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "APNs turn notification failed for {PlayerId}", playerId);
            }
        }
    }

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_teamId)
        && !string.IsNullOrWhiteSpace(_keyId)
        && !string.IsNullOrWhiteSpace(_privateKey);

    /// <summary>
    /// A pooled HTTP/2 connection Apple has already closed fails the first
    /// request that touches it and succeeds on a fresh one. Retry once rather
    /// than dropping a notification for a connection-level hiccup.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or HttpIOException)
        {
            _logger.LogWarning(ex, "APNs request failed at the connection level; retrying once");

            using var retry = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };
            foreach (var header in request.Headers)
                retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
            retry.Content = request.Content;
            return await _httpClient.SendAsync(retry, cancellationToken);
        }
    }

    private string GetProviderToken()
    {
        lock (_tokenLock)
        {
            var now = DateTimeOffset.UtcNow;
            if (_providerToken != null && now < _providerTokenExpiresAt)
                return _providerToken;

            var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
            {
                alg = "ES256",
                kid = _keyId
            }));
            var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
            {
                iss = _teamId,
                iat = now.ToUnixTimeSeconds()
            }));
            var unsignedToken = $"{header}.{payload}";

            using var key = ECDsa.Create();
            key.ImportFromPem(_privateKey);
            var signature = key.SignData(
                Encoding.ASCII.GetBytes(unsignedToken),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            _providerToken = $"{unsignedToken}.{Base64UrlEncode(signature)}";
            _providerTokenExpiresAt = now.AddMinutes(50);
            return _providerToken;
        }
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
