// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Services;

/// <summary>
/// Service for checking the Intune management status of a Windows device via WNS.
/// </summary>
public class IntuneStatusService : IIntuneStatusService
{
    private readonly ISessionManager _sessionManager;
    private readonly IWnsNotificationService _wnsService;
    private readonly LocalMcpProxyOptions _options;
    private readonly ILogger<IntuneStatusService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntuneStatusService"/> class.
    /// </summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="wnsService">The WNS notification service.</param>
    /// <param name="options">The LocalMcp proxy options.</param>
    /// <param name="logger">The logger.</param>
    public IntuneStatusService(
        ISessionManager sessionManager,
        IWnsNotificationService wnsService,
        IOptions<LocalMcpProxyOptions> options,
        ILogger<IntuneStatusService> logger)
    {
        _sessionManager = sessionManager;
        _wnsService = wnsService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IntuneStatusResult?> CheckIntuneStatusAsync(
        string clientName,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[INTUNE SERVICE] Checking Intune status for client '{ClientName}'", clientName);

        var client = _sessionManager.GetClient(clientName);
        if (client == null)
        {
            _logger.LogWarning("[INTUNE SERVICE] Client '{ClientName}' not found", clientName);
            return null;
        }

        if (string.IsNullOrEmpty(_options.BaseUrl))
        {
            _logger.LogError("[INTUNE SERVICE] BaseUrl is not configured in LocalMcpProxyOptions. Cannot generate callback URL.");
            return new IntuneStatusResult
            {
                RequestId = string.Empty,
                Status = "error",
                ErrorMessage = "BaseUrl is not configured. Set LocalMcpProxy:BaseUrl in configuration."
            };
        }

        var requestId = Guid.NewGuid().ToString();
        var callbackUrl = $"{_options.BaseUrl}/api/intune-response/{requestId}";

        // Create pending result
        _sessionManager.CreatePendingIntuneStatusResult(requestId);

        _logger.LogInformation("[INTUNE SERVICE] Sending Intune check notification, requestId: {RequestId}", requestId);

        // Send WNS notification
        var (success, errorMessage) = await _wnsService.SendIntuneCheckNotificationAsync(
            client.ChannelUri, requestId, callbackUrl);

        if (!success)
        {
            _logger.LogError("[INTUNE SERVICE] Failed to send Intune check notification: {Error}", errorMessage);
            return new IntuneStatusResult
            {
                RequestId = requestId,
                Status = "error",
                ErrorMessage = errorMessage ?? "Failed to send WNS notification"
            };
        }

        // Poll for result
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = _sessionManager.GetIntuneStatusResult(requestId);

            if (result != null && result.Status == "completed")
            {
                _logger.LogInformation("[INTUNE SERVICE] Intune status received: IsIntuneManaged={IsManaged}",
                    result.IsIntuneManaged);
                return result;
            }

            await Task.Delay(500, cancellationToken);
        }

        _logger.LogWarning("[INTUNE SERVICE] Intune check timed out after {Timeout}s", timeoutSeconds);
        return new IntuneStatusResult
        {
            RequestId = requestId,
            Status = "timeout",
            ErrorMessage = $"Intune check timed out after {timeoutSeconds} seconds"
        };
    }

    /// <inheritdoc />
    public async Task<bool> IsDeviceIntuneManagedAsync(
        string clientName,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await CheckIntuneStatusAsync(clientName, timeoutSeconds, cancellationToken);
        return result?.IsIntuneManaged ?? false;
    }
}
