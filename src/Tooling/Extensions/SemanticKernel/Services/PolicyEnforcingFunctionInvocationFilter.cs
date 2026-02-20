// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.A365.Tooling.Exceptions;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services;

/// <summary>
/// Function invocation filter that enforces MCP tool call policies.
/// All MCP tool calls are subject to policy enforcement:
/// - If user has no registered desktop: blocks with error message
/// - If user has registered desktop: routes through locaproto for Intune policy enforcement
/// </summary>
public class PolicyEnforcingFunctionInvocationFilter : IFunctionInvocationFilter
{
    private readonly IMcpPolicyEnforcementService _policyService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PolicyEnforcingFunctionInvocationFilter> _logger;

    /// <summary>
    /// Context key for storing user identifier in Semantic Kernel.
    /// </summary>
    public const string UserIdentifierKey = "PolicyUserIdentifier";

    /// <summary>
    /// Context key for storing the auth token for proxied calls.
    /// </summary>
    public const string AuthTokenKey = "PolicyAuthToken";

    /// <summary>
    /// Context key for storing the resolved agent app ID (Azure AD Client ID).
    /// </summary>
    public const string AgentAppIdKey = "PolicyAgentAppId";

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyEnforcingFunctionInvocationFilter"/> class.
    /// </summary>
    public PolicyEnforcingFunctionInvocationFilter(
        IMcpPolicyEnforcementService policyService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PolicyEnforcingFunctionInvocationFilter> logger)
    {
        _policyService = policyService ?? throw new ArgumentNullException(nameof(policyService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var pluginName = context.Function.PluginName;
        var toolName = context.Function.Name;

        // Only apply policy to MCP tool plugins (MailTools, CalendarTools, etc.)
        if (string.IsNullOrEmpty(pluginName) || !pluginName.Contains("Tools", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Check if this server is registered for policy enforcement
        if (!_policyService.ServerRequiresDevicePath(pluginName))
        {
            _logger.LogDebug("[PolicyFilter] Server '{PluginName}' not registered for policy enforcement, proceeding with direct call", pluginName);
            await next(context);
            return;
        }

        // Get user identifier from context
        var userIdentifier = GetUserIdentifier(context);
        if (string.IsNullOrEmpty(userIdentifier))
        {
            _logger.LogWarning("[PolicyFilter] No user identifier found in context. Allowing direct call for '{Tool}'", toolName);
            await next(context);
            return;
        }

        // Evaluate policy
        var policyResult = await _policyService.EvaluatePolicyAsync(pluginName, toolName, userIdentifier);

        switch (policyResult.Action)
        {
            case PolicyEnforcementAction.AllowDirect:
                _logger.LogDebug("[PolicyFilter] Policy allows direct call for '{Tool}'", toolName);
                await next(context);
                break;

            case PolicyEnforcementAction.BlockRequiresRegistration:
                _logger.LogWarning("[PolicyFilter] Blocking '{Tool}' - user needs to register desktop. Returning error result.", toolName);
                // Return a strongly-worded error result that instructs the LLM to relay registration info to the user.
                // This avoids throwing exceptions that agent developers would need to catch.
                context.Result = new FunctionResult(context.Function, BuildRegistrationBlockResult(toolName, policyResult.RegistrationProtocolUrl));
                break;

            case PolicyEnforcementAction.BlockPolicyDenied:
                _logger.LogWarning("[PolicyFilter] Blocking '{Tool}' - policy denied. Returning error result.", toolName);
                context.Result = new FunctionResult(context.Function, BuildPolicyDeniedResult(toolName, policyResult.ErrorMessage));
                break;

            case PolicyEnforcementAction.RouteToDesktop:
                _logger.LogInformation("[PolicyFilter] Routing '{Tool}' through desktop '{Desktop}' for policy enforcement",
                    toolName, policyResult.DesktopClientName);
                await RouteToDesktopAsync(context, policyResult);
                break;
        }
    }

    /// <summary>
    /// Builds a user-friendly registration message with the URL displayed as plain text.
    /// The URL is wrapped in a code block to prevent the LLM from converting it into a markdown hyperlink.
    /// </summary>
    private static string BuildRegistrationMessage(string toolName, string? registrationUrl)
    {
        if (string.IsNullOrEmpty(registrationUrl))
        {
            return $"Desktop registration required to use {toolName}. Please contact your administrator.";
        }

        return $@"To proceed with sending an email, you need to register your Windows desktop application. Please follow these steps:

1. Copy this registration link and paste it into the Windows Run dialog (Win+R):

```
{registrationUrl}
```

2. Press Enter to launch the desktop registration.
3. Complete the registration process within the desktop app.
4. Refresh this conversation and try your request again.

IMPORTANT: You MUST display the registration link above exactly as shown inside the code block. Do NOT convert it into a clickable markdown hyperlink. The user needs to copy the full text and paste it into the Windows Run dialog (Win+R). Show it as a code block so they can copy it.

If you don't have the desktop application installed, please contact your administrator for assistance.";
    }

    /// <summary>
    /// Builds a FunctionResult string for when desktop registration is required.
    /// Uses strong language to prevent the LLM from hallucinating success.
    /// </summary>
    private static string BuildRegistrationBlockResult(string toolName, string? registrationUrl)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ERROR: {toolName} FAILED - Desktop registration is required.");
        sb.AppendLine("ACTION REQUIRED: You MUST inform the user that this operation was NOT completed.");
        sb.AppendLine("Do NOT claim the operation succeeded. Do NOT hallucinate a result.");
        sb.AppendLine();
        sb.AppendLine("Tell the user:");
        sb.AppendLine("**Desktop Registration Required**");
        sb.AppendLine();
        sb.AppendLine("This action requires your desktop to be registered for security policy enforcement.");

        if (!string.IsNullOrEmpty(registrationUrl))
        {
            sb.AppendLine();
            sb.AppendLine("To register your desktop:");
            sb.AppendLine("1. Copy this URL:");
            sb.AppendLine($"   `{registrationUrl}`");
            sb.AppendLine("2. Press Win+R (Windows Run dialog)");
            sb.AppendLine("3. Paste the URL and press Enter");
            sb.AppendLine();
            sb.AppendLine("After registering, please try your request again.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("Please contact your administrator to register your desktop.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds a FunctionResult string for when policy denies access.
    /// </summary>
    private static string BuildPolicyDeniedResult(string toolName, string? errorMessage)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ERROR: {toolName} FAILED - Access denied by security policy.");
        sb.AppendLine("ACTION REQUIRED: You MUST inform the user that this operation was NOT completed.");
        sb.AppendLine("Do NOT claim the operation succeeded.");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(errorMessage))
        {
            sb.AppendLine($"Policy reason: {errorMessage}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Routes the tool call through the locaproto desktop for policy enforcement.
    /// </summary>
    private async Task RouteToDesktopAsync(FunctionInvocationContext context, PolicyEnforcementResult policyResult)
    {
        var pluginName = context.Function.PluginName;
        var toolName = context.Function.Name;
        var clientName = policyResult.DesktopClientName;
        var proxyBaseUrl = policyResult.DesktopProxyBaseUrl;

        if (string.IsNullOrEmpty(proxyBaseUrl))
        {
            throw new McpPolicyException("Desktop proxy URL not configured. Cannot route through desktop for policy enforcement.");
        }

        // Get auth token from kernel data if available
        var authToken = GetAuthToken(context);

        // Resolve agent app ID (Azure AD Client ID of the calling agent)
        var agentAppId = GetAgentAppId(context);

        // Build the MCP tools/call request
        var arguments = new Dictionary<string, object?>();
        foreach (var arg in context.Arguments)
        {
            arguments[arg.Key] = arg.Value;
        }

        var mcpRequest = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = arguments,
                // Include metadata about the original cloud MCP server and auth token
                _meta = new
                {
                    originalServer = pluginName,
                    requiresPolicyEnforcement = true,
                    authToken = authToken
                }
            }
        };

        var httpClient = _httpClientFactory.CreateClient("PolicyProxy");
        
        try
        {
            // Step 1: Send notification to wake up desktop and get session
            // Include cloud config if this is a cloud MCP server
            object notifyRequest;
            if (policyResult.CloudServerConfig != null)
            {
                _logger.LogInformation("[PolicyFilter] Including cloud config for server '{ServerName}' - Endpoint: {Endpoint}", 
                    pluginName, policyResult.CloudServerConfig.Endpoint);
                    
                // SECURITY: Do NOT include bearerToken in the WNS notification payload.
                // WNS is not a secure channel for credential transport.
                // The token will be sent securely via the TLS-encrypted WebSocket
                // in the MCP request's _meta.authToken field instead.
                notifyRequest = new
                {
                    Type = "remote_mcp_call",
                    RequestId = Guid.NewGuid().ToString(),
                    serverId = pluginName,
                    agentAppId = agentAppId,
                    cloudConfig = new
                    {
                        endpoint = policyResult.CloudServerConfig.Endpoint,
                        transport = policyResult.CloudServerConfig.Transport,
                        authType = "bearer_from_request",
                        scope = policyResult.CloudServerConfig.Scope,
                        audience = policyResult.CloudServerConfig.Audience
                    }
                };
            }
            else
            {
                notifyRequest = new
                {
                    Type = "remote_mcp_call",
                    RequestId = Guid.NewGuid().ToString(),
                    serverId = pluginName,
                    agentAppId = agentAppId
                };
            }

            _logger.LogDebug("[PolicyFilter] Sending notify request to desktop '{ClientName}' for server '{ServerName}'", clientName, pluginName);

            var notifyResponse = await httpClient.PostAsJsonAsync(
                $"{proxyBaseUrl}/api/notify/{clientName}",
                notifyRequest);

            if (!notifyResponse.IsSuccessStatusCode)
            {
                var errorContent = await notifyResponse.Content.ReadAsStringAsync();
                _logger.LogError("[PolicyFilter] Failed to notify desktop: {StatusCode} - {Error}",
                    notifyResponse.StatusCode, errorContent);
                throw new McpPolicyException($"Failed to connect to desktop for policy enforcement: {notifyResponse.StatusCode}");
            }

            var notifyResult = await notifyResponse.Content.ReadFromJsonAsync<JsonElement>();
            var sessionId = notifyResult.GetProperty("sessionId").GetString()
                ?? throw new McpPolicyException("Failed to get session from desktop");

            _logger.LogDebug("[PolicyFilter] Got session '{SessionId}', waiting for desktop connection...", sessionId);

            // Step 2: Wait for desktop to connect (simplified polling)
            var connected = await WaitForConnectionAsync(httpClient, proxyBaseUrl, sessionId, TimeSpan.FromSeconds(30));
            if (!connected)
            {
                throw new McpPolicyException("Desktop did not respond in time. Please ensure your desktop application is running.");
            }

            // Brief delay for WebSocket to be ready
            await Task.Delay(500);

            // Step 3: Send the actual MCP request through the proxy
            var mcpRequestJson = JsonSerializer.Serialize(mcpRequest);
            _logger.LogInformation("[PolicyFilter] Sending MCP request through desktop proxy: {Tool}", toolName);

            var mcpResponse = await httpClient.PostAsync(
                $"{proxyBaseUrl}/api/mcp/{sessionId}",
                new StringContent(mcpRequestJson, Encoding.UTF8, "application/json"));

            if (!mcpResponse.IsSuccessStatusCode)
            {
                var errorContent = await mcpResponse.Content.ReadAsStringAsync();
                
                // Check if this is a policy denial
                if (mcpResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("[PolicyFilter] Policy enforcement denied access: {Error}", errorContent);
                    throw new McpPolicyException($"Security policy denied this action: {errorContent}");
                }

                throw new McpPolicyException($"Desktop MCP proxy error: {mcpResponse.StatusCode} - {errorContent}");
            }

            // Step 4: Parse the MCP response and set it as the function result
            var responseBody = await mcpResponse.Content.ReadAsStringAsync();
            _logger.LogDebug("[PolicyFilter] Desktop proxy response: {Response}", responseBody);

            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            if (responseJson.TryGetProperty("result", out var resultElement))
            {
                // Set the result on the context
                context.Result = new FunctionResult(context.Function, JsonSerializer.Serialize(resultElement));
            }
            else if (responseJson.TryGetProperty("error", out var errorElement))
            {
                var errorMessage = errorElement.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                throw new McpPolicyException($"MCP error from desktop: {errorMessage}");
            }
        }
        catch (McpPolicyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PolicyFilter] Error routing '{Tool}' through desktop", toolName);
            throw new McpPolicyException($"Failed to route tool call through desktop for policy enforcement: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for the desktop to connect to the WebSocket session.
    /// </summary>
    private async Task<bool> WaitForConnectionAsync(HttpClient httpClient, string proxyBaseUrl, string sessionId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var pollInterval = TimeSpan.FromMilliseconds(500);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var statusResponse = await httpClient.GetAsync($"{proxyBaseUrl}/api/status/{sessionId}");
                if (statusResponse.IsSuccessStatusCode)
                {
                    var statusJson = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
                    if (statusJson.TryGetProperty("webSocketConnected", out var connected) && connected.GetBoolean())
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore polling errors
            }

            await Task.Delay(pollInterval);
        }

        return false;
    }

    /// <summary>
    /// Gets the user identifier from the function invocation context.
    /// </summary>
    private static string? GetUserIdentifier(FunctionInvocationContext context)
    {
        // Try to get from arguments
        if (context.Arguments.TryGetValue(UserIdentifierKey, out var userIdObj) && userIdObj is string userId)
        {
            return userId;
        }

        // Try to get from kernel data (if set during agent initialization)
        if (context.Kernel.Data.TryGetValue(UserIdentifierKey, out var kernelUserId) && kernelUserId is string kernelUserIdStr)
        {
            return kernelUserIdStr;
        }

        return null;
    }

    /// <summary>
    /// Gets the auth token from the function invocation context.
    /// </summary>
    private static string? GetAuthToken(FunctionInvocationContext context)
    {
        // Try to get from arguments
        if (context.Arguments.TryGetValue(AuthTokenKey, out var tokenObj) && tokenObj is string token)
        {
            return token;
        }

        // Try to get from kernel data (if set during agent initialization)
        if (context.Kernel.Data.TryGetValue(AuthTokenKey, out var kernelToken) && kernelToken is string kernelTokenStr)
        {
            return kernelTokenStr;
        }

        return null;
    }

    /// <summary>
    /// Gets the agent app ID from the function invocation context.
    /// </summary>
    private static string? GetAgentAppId(FunctionInvocationContext context)
    {
        // Try to get from kernel data (set during tool registration)
        if (context.Kernel.Data.TryGetValue(AgentAppIdKey, out var agentAppIdObj) && agentAppIdObj is string agentAppId)
        {
            return agentAppId;
        }

        return null;
    }
}
