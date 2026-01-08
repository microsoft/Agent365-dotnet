// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Tests.Services.McpToolRegistrationServiceTests;

/// <summary>
/// Base class for McpToolRegistrationService tests, providing common test setup.
/// </summary>
public abstract class McpToolRegistrationServiceTestBase
{
    protected readonly Mock<ILogger<IMcpToolRegistrationService>> LoggerMock;
    protected readonly Mock<IMcpToolServerConfigurationService> McpServerConfigurationServiceMock;
    protected readonly Mock<IConfiguration> ConfigurationMock;

    protected McpToolRegistrationServiceTestBase()
    {
        LoggerMock = new Mock<ILogger<IMcpToolRegistrationService>>();
        McpServerConfigurationServiceMock = new Mock<IMcpToolServerConfigurationService>();
        ConfigurationMock = new Mock<IConfiguration>();
    }

    /// <summary>
    /// Creates a new instance of McpToolRegistrationService with mocked dependencies.
    /// </summary>
    protected McpToolRegistrationService CreateService()
    {
        return new McpToolRegistrationService(
            LoggerMock.Object,
            McpServerConfigurationServiceMock.Object,
            ConfigurationMock.Object);
    }

    /// <summary>
    /// Helper method to check if a string is a valid GUID.
    /// </summary>
    protected static bool IsValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }
}
