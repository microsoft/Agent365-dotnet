// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Tests;

/// <summary>
/// Contains constant values used across test files to ensure consistency and reduce confusion.
/// </summary>
public static class TestConstants
{
    // MCP Server Names
    public const string MailToolsServerName = "mailtools";
    public const string CalendarToolsServerName = "calendartools";
    public const string SharePointToolsServerName = "sharepointtools";

    // Server IDs
    public const string MailServerId = "mcp-mail-001";
    public const string CalendarServerId = "mcp-calendar-001";
    public const string SharePointServerId = "mcp-sharepoint-001";

    // URLs
    public const string MailToolsUrl = "https://mailtools.example.com/mcp";
    public const string CalendarToolsUrl = "https://calendartools.example.com/mcp";
    public const string SharePointToolsUrl = "https://sharepointtools.example.com/mcp";

    // Scopes
    public const string MailScope = "mail.read mail.send";
    public const string CalendarScope = "calendar.read calendar.write";
    public const string SharePointScope = "sites.read sites.write";

    // Audiences
    public const string MailAudience = "api://mailtools";
    public const string CalendarAudience = "api://calendartools";
    public const string SharePointAudience = "api://sharepointtools";

    // Publisher
    public const string Publisher = "Microsoft";

    // Test Environment Values
    public const string TestAgentInstanceId = "test-agent-123";
    public const string TestAuthToken = "test-auth-token";

    // Command and Args
    public const string NodeCommand = "node";
    public const string PythonCommand = "python";
    public const string DefaultArgs = "--version";
}
