// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Models;

/// <summary>
/// Represents the Intune management status of a Windows device.
/// </summary>
public class IntuneStatusResult
{
    /// <summary>
    /// Gets or sets the request ID for correlating the Intune status check.
    /// </summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the device is managed by Intune.
    /// </summary>
    [JsonPropertyName("isIntuneManaged")]
    public bool IsIntuneManaged { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device is Azure AD joined.
    /// </summary>
    [JsonPropertyName("isAzureAdJoined")]
    public bool IsAzureAdJoined { get; set; }

    /// <summary>
    /// Gets or sets the MDM enrollment URL.
    /// </summary>
    [JsonPropertyName("mdmUrl")]
    public string? MdmUrl { get; set; }

    /// <summary>
    /// Gets or sets the UPN of the enrolled user.
    /// </summary>
    [JsonPropertyName("enrolledUserPrincipalName")]
    public string? EnrolledUserPrincipalName { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD tenant ID.
    /// </summary>
    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD device ID.
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the machine name of the device.
    /// </summary>
    [JsonPropertyName("machineName")]
    public string? MachineName { get; set; }

    /// <summary>
    /// Gets or sets when the Intune status was checked.
    /// </summary>
    [JsonPropertyName("checkedAt")]
    public DateTime CheckedAt { get; set; }

    /// <summary>
    /// Gets or sets the status of the request (pending, completed, error).
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Gets or sets an error message if the check failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets when the result was received.
    /// </summary>
    [JsonPropertyName("receivedAt")]
    public DateTime ReceivedAt { get; set; }
}
