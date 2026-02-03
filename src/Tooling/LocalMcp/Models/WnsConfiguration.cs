// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Models;

/// <summary>
/// Configuration options for WNS (Windows Notification Service).
/// </summary>
public class WnsConfiguration
{
    /// <summary>
    /// Gets or sets the Azure AD tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD application (client) ID for WNS.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure AD client secret for WNS.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}
