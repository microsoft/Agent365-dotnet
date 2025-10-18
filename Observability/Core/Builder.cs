// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability;

using Azure.Identity;
using Azure.Monitor.Ingestion;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Common;
using Microsoft.Agents.A365.Observability.Tracing;
using Microsoft.Agents.A365.Observability.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry.Trace;

/// <summary>
/// Builder for configuring SDK with OpenTelemetry tracing.
/// </summary>
public sealed class Builder
{
    private readonly IServiceCollection _services;
    private string? _connectionString;
    private bool _isBuilt = false;
    private SentinelConfiguration? _sentinelConfiguration;
    private UserAuthorization? userAuth;
    private ITurnContext? turnContext;


    /// <summary>
    /// Initializes a new instance of the <see cref="Builder"/> class.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    internal Builder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the services collection for continued configuration.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Configures Microsoft Sentinel integration using environment variable configuration.
    /// This enables sending telemetry to Azure Analytics Workspace for security monitoring.
    /// Agent information will be extracted from activity context.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public Builder WithSentinel()
    {
        var configuration = SentinelConfiguration.Load();
        if (configuration == null || !configuration.IsValid())
        {
            Console.WriteLine("Sentinel configuration is not valid. Please ensure environment variables are correctly set.");
            return this;
        }

        _sentinelConfiguration = configuration;
        return this;
    }

    /// <summary>
    /// Configures Microsoft Sentinel integration.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public Builder WithSentinel(string tenantId, string clientId, string clientSecret, string endpoint, string ruleId, string streamName)
    {
        var configuration = new SentinelConfiguration
        {
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = clientSecret,
            Endpoint = endpoint,
            RuleId = ruleId,
            StreamName = streamName
        };
        if (!configuration.IsValid())
        {
            Console.WriteLine("Sentinel configuration is not valid.");
            return this;
        }

        _sentinelConfiguration = configuration;
        return this;
    }

    private bool IsSentinelConfigured()
    {
        return _sentinelConfiguration?.IsValid() == true;
    }

    /// <summary>
    /// Configures the Azure Monitor connection string for telemetry export.
    /// </summary>
    /// <param name="connectionString">The Azure Monitor connection string.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public Builder WithConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Configures the user authorization and turn context for agentic authentication.
    /// </summary>
    /// <param name="userAuth">The user authorization information.</param>
    /// <param name="turnContext">The turn context for the current conversation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public Builder WithAuth(UserAuthorization userAuth, ITurnContext turnContext)
    {
        this.userAuth = userAuth;
        this.turnContext = turnContext;
        return this;
    }

    /// <summary>
    /// Builds the AI configuration and returns the service collection.
    /// </summary>
    /// <returns>The configured service collection.</returns>
    public IServiceCollection Build()
    {
        EnsureBuilt();
        return _services;
    }

    private bool IsAgent365ExporterEnabled()
    {
        return Environment.GetEnvironmentVariable("EnableAgent365Exporter") == "true" 
            || Environment.GetEnvironmentVariable("EnableKairoExporter") == "true"; // Legacy support
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
            return;

        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
        
        // Configure OpenTelemetry with all processors in a single call
        _services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new ParentBasedSampler(
                        rootSampler: new AlwaysOnSampler(),
                        localParentNotSampled: new AlwaysOnSampler(),
                        remoteParentNotSampled: new AlwaysOnSampler()))
                    .AddSource(OpenTelemetryConstants.SourceName)
                    .AddProcessor(new ActivityProcessor());

                if (IsAgent365ExporterEnabled())
                {
                    tracing.AddAgent365Exporter();
                }
                else if (EnvironmentUtils.IsDevelopmentEnvironment())
                {
                    tracing.AddConsoleExporter();
                }

                // Add Azure Monitor exporter if connection string is provided
                if (!string.IsNullOrEmpty(_connectionString))
                {
                    tracing.AddAzureMonitorTraceExporter(options => { options.ConnectionString = _connectionString; });
                }

                // Add Sentinel processor if configured
                if (IsSentinelConfigured())
                {
                    var config = _sentinelConfiguration!;
                    var credential = new ClientSecretCredential(config.TenantId, config.ClientId, config.ClientSecret);
                    var ingestionClient = new LogsIngestionClient(new Uri(config.Endpoint), credential);

                    tracing.AddProcessor(new AzureAnalyticsWorkspaceProcessor(
                        ingestionClient,
                        config.RuleId,
                        config.StreamName));
                }
            });

        _isBuilt = true;
    }
}