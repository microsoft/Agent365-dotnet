// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.LocalMcp.Models;

namespace Microsoft.Agents.A365.Tooling.LocalMcp.Services;

/// <summary>
/// Interface for checking the Intune management status of a Windows device via WNS.
/// </summary>
public interface IIntuneStatusService
{
    /// <summary>
    /// Checks whether the device associated with the given client is Intune managed.
    /// This sends a WNS notification to the device and waits for the callback response.
    /// </summary>
    /// <param name="clientName">The registered client name.</param>
    /// <param name="timeoutSeconds">The timeout in seconds to wait for the response. Default is 30 seconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The Intune status result, or null if the check failed or timed out.</returns>
    Task<IntuneStatusResult?> CheckIntuneStatusAsync(string clientName, int timeoutSeconds = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the device associated with the given client is Intune managed.
    /// This is a simpler version that just returns true/false.
    /// </summary>
    /// <param name="clientName">The registered client name.</param>
    /// <param name="timeoutSeconds">The timeout in seconds to wait for the response. Default is 30 seconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the device is Intune managed, false otherwise.</returns>
    Task<bool> IsDeviceIntuneManagedAsync(string clientName, int timeoutSeconds = 30, CancellationToken cancellationToken = default);
}
