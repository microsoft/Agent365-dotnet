// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.A365.Tooling.LocalMcp.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Services;

/// <summary>
/// Service for sending Windows Push Notification Service (WNS) notifications.
/// </summary>
public class WnsNotificationService : IWnsNotificationService
{
    private readonly WnsConfiguration _config;
    private readonly ILogger<WnsNotificationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="WnsNotificationService"/> class.
    /// </summary>
    /// <param name="options">The WNS configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public WnsNotificationService(
        IOptions<WnsConfiguration> options,
        ILogger<WnsNotificationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;

        if (!IsConfigured)
        {
            _logger.LogWarning("[WNS SERVICE] WnsConfiguration is incomplete. Local MCP functionality will not be available.");
        }
        else
        {
            _logger.LogInformation("[WNS SERVICE] Initialized successfully with TenantId: {TenantId}, ClientId: {ClientId}",
                _config.TenantId, _config.ClientId);
        }
    }

    /// <inheritdoc />
    public bool IsConfigured =>
        !string.IsNullOrEmpty(_config.TenantId) &&
        !string.IsNullOrEmpty(_config.ClientId) &&
        !string.IsNullOrEmpty(_config.ClientSecret);

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync()
    {
        // Return cached token if still valid
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
        {
            _logger.LogDebug("[WNS SERVICE] Using cached access token (expires: {Expiry})", _tokenExpiry);
            return _accessToken;
        }

        _logger.LogInformation("[WNS SERVICE] Requesting new access token from Azure AD...");

        using var client = _httpClientFactory.CreateClient();

        var tokenEndpoint = $"https://login.microsoftonline.com/{_config.TenantId}/oauth2/v2.0/token";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _config.ClientId,
            ["client_secret"] = _config.ClientSecret,
            ["scope"] = "https://wns.windows.com/.default"
        });

        try
        {
            var response = await client.PostAsync(tokenEndpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[WNS SERVICE] Token request failed with status {StatusCode}: {Response}",
                    response.StatusCode, responseBody);
                response.EnsureSuccessStatusCode();
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            _accessToken = result.GetProperty("access_token").GetString();

            var expiresInProperty = result.GetProperty("expires_in");
            var expiresIn = expiresInProperty.ValueKind == JsonValueKind.String
                ? int.Parse(expiresInProperty.GetString()!)
                : expiresInProperty.GetInt32();

            // Cache token with 5-minute buffer before expiry
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 300);

            _logger.LogInformation("[WNS SERVICE] Access token acquired (expires: {Expiry})", _tokenExpiry);

            return _accessToken!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WNS SERVICE] Failed to acquire access token");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> SendNotificationAsync(
        string channelUri, string callbackUrl, string? serverId = null)
    {
        _logger.LogInformation("[WNS SERVICE] Sending notification to channel: {ChannelUri}",
            channelUri.Substring(0, Math.Min(60, channelUri.Length)) + "...");
        _logger.LogInformation("[WNS SERVICE] Callback URL: {CallbackUrl}", callbackUrl);

        try
        {
            var accessToken = await GetAccessTokenAsync();

            var notification = new
            {
                callback = callbackUrl,
                serverId = serverId,
                timestamp = DateTime.UtcNow
            };

            return await SendWnsRawNotificationAsync(channelUri, notification, accessToken);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Exception: {ex.Message}";
            _logger.LogError(ex, "[WNS SERVICE] Error sending notification");
            return (false, errorMessage);
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> SendDiscoveryNotificationAsync(
        string channelUri, string requestId, string callbackUrl)
    {
        _logger.LogInformation("[WNS SERVICE] Sending DISCOVERY notification");
        _logger.LogInformation("[WNS SERVICE] Request ID: {RequestId}", requestId);
        _logger.LogInformation("[WNS SERVICE] Callback URL: {CallbackUrl}", callbackUrl);

        try
        {
            var accessToken = await GetAccessTokenAsync();

            var notification = new
            {
                type = "list_servers",
                requestId = requestId,
                callbackUrl = callbackUrl,
                timestamp = DateTime.UtcNow
            };

            return await SendWnsRawNotificationAsync(channelUri, notification, accessToken);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Exception: {ex.Message}";
            _logger.LogError(ex, "[WNS SERVICE] Error sending discovery notification");
            return (false, errorMessage);
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> SendWnsRawNotificationAsync(
        string channelUri, object payload, string accessToken)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        _logger.LogInformation("[WNS SERVICE] Payload: {Payload}", payloadJson);

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var request = new HttpRequestMessage(HttpMethod.Post, channelUri);
        request.Content = new ByteArrayContent(payloadBytes);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        request.Content.Headers.ContentLength = payloadBytes.Length;

        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        request.Headers.TryAddWithoutValidation("X-WNS-Type", "wns/raw");
        request.Headers.TryAddWithoutValidation("X-WNS-RequestForStatus", "true");

        _logger.LogInformation("[WNS SERVICE] Sending HTTP POST to WNS (payload size: {Size} bytes)", payloadBytes.Length);

        var response = await client.SendAsync(request);

        _logger.LogInformation("[WNS SERVICE] WNS response status: {StatusCode}", response.StatusCode);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("[WNS SERVICE] Notification sent successfully");

            if (response.Headers.TryGetValues("X-WNS-NotificationStatus", out var notifStatus))
            {
                _logger.LogInformation("[WNS SERVICE] Notification Status: {Status}",
                    string.Join(", ", notifStatus));
            }

            return (true, null);
        }
        else
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var errorMessage = $"WNS returned {response.StatusCode}";

            if (response.Headers.TryGetValues("X-WNS-Status", out var wnsStatus))
            {
                errorMessage += $" | X-WNS-Status: {string.Join(", ", wnsStatus)}";
            }

            if (response.Headers.TryGetValues("X-WNS-Error-Description", out var wnsError))
            {
                errorMessage += $" | Error: {string.Join(", ", wnsError)}";
            }

            if (!string.IsNullOrEmpty(responseBody))
            {
                errorMessage += $" | Body: {responseBody}";
            }

            _logger.LogError("[WNS SERVICE] Notification failed: {ErrorMessage}", errorMessage);
            return (false, errorMessage);
        }
    }
}
