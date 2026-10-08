// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Defender
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Configuration for Microsoft Defender for AI real-time protection (the prevention endpoint
    /// <c>POST .../v1/protection/evaluate</c>, agent-hooks/0.1 contract).
    /// </summary>
    public sealed class DefenderRtpOptions
    {
        /// <summary>Application id of the Defender API that grants <c>RealtimeProtection.Evaluate.All</c>.</summary>
        public const string DefenderApiAppId = "86a21212-634e-4553-b3d6-e477e4c9d9ec";

        /// <summary>Default token scope: the Defender API.</summary>
        public const string DefaultAuthenticationScope = "api://" + DefenderApiAppId + "/.default";

        /// <summary>Whether Defender real-time protection is enabled (<c>ENABLE_A365_DEFENDER_RTP</c>).</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The prevention endpoint (<c>A365_DEFENDER_RTP_ENDPOINT</c>), an absolute HTTPS URL. Required when enabled.
        /// </summary>
        public Uri? Endpoint { get; set; }

        /// <summary>The token scope (<c>A365_DEFENDER_RTP_AUTHENTICATION_SCOPE</c>); defaults to the Defender API.</summary>
        public string AuthenticationScope { get; set; } = DefaultAuthenticationScope;

        /// <summary>
        /// The deadline for one evaluation, shared by token acquisition and the request
        /// (<c>A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS</c>). When it passes, the result follows <see cref="FailClosed"/>.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// When true, an evaluation that returns no verdict blocks (<c>A365_DEFENDER_RTP_FAIL_MODE=closed</c>);
        /// otherwise it is allowed and reported as not evaluated.
        /// </summary>
        public bool FailClosed { get; set; }

        /// <summary>
        /// Maximum characters per content string sent to Defender (<c>A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS</c>):
        /// input and output content, tool arguments and results, message content, tool descriptions and
        /// schemas, extensions, and any member a host adds. Envelope fields such as ids, names and roles are
        /// not truncated.
        /// </summary>
        public int MaxContentCharacters { get; set; } = 20000;

        /// <summary>Reads the options from the process environment.</summary>
        /// <returns>The configured options.</returns>
        public static DefenderRtpOptions FromEnvironment() => FromEnvironment(Environment.GetEnvironmentVariable);

        /// <summary>Reads the options from the given variable lookup.</summary>
        /// <param name="getVariable">Returns the value of an environment variable, or null.</param>
        /// <returns>The configured options.</returns>
        public static DefenderRtpOptions FromEnvironment(Func<string, string?> getVariable)
        {
            if (getVariable == null)
            {
                throw new ArgumentNullException(nameof(getVariable));
            }

            string? Read(string name)
            {
                var value = getVariable(name)?.Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }

            var options = new DefenderRtpOptions
            {
                Enabled = Read("ENABLE_A365_DEFENDER_RTP") is { } enabled
                    && (enabled.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || enabled == "1"
                        || enabled.Equals("yes", StringComparison.OrdinalIgnoreCase)),
                FailClosed = string.Equals(Read("A365_DEFENDER_RTP_FAIL_MODE"), "closed", StringComparison.OrdinalIgnoreCase),
            };

            if (Read("A365_DEFENDER_RTP_ENDPOINT") is { } endpoint)
            {
                options.Endpoint = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && IsHttpsUrl(uri)
                    ? uri
                    : throw new InvalidOperationException("A365_DEFENDER_RTP_ENDPOINT must be an absolute HTTPS URL.");
            }

            if (Read("A365_DEFENDER_RTP_AUTHENTICATION_SCOPE") is { } scope)
            {
                options.AuthenticationScope = scope;
            }

            if (Read("A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS") is { } timeout)
            {
                options.Timeout = TimeSpan.FromMilliseconds(ParsePositive(timeout, "A365_DEFENDER_RTP_TIMEOUT_MILLISECONDS"));
            }

            if (Read("A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS") is { } maximum)
            {
                options.MaxContentCharacters = ParsePositive(maximum, "A365_DEFENDER_RTP_MAX_CONTENT_CHARACTERS");
            }

            return options;
        }

        /// <summary>Throws when the options cannot be used for an enabled client.</summary>
        internal void Validate()
        {
            if (Enabled && Endpoint == null)
            {
                throw new InvalidOperationException(
                    "Defender RTP is enabled but no endpoint is configured. Set A365_DEFENDER_RTP_ENDPOINT or DefenderRtpOptions.Endpoint.");
            }

            if (Endpoint != null && !IsHttpsUrl(Endpoint))
            {
                throw new InvalidOperationException(
                    "The Defender RTP endpoint must be an absolute HTTPS URL (A365_DEFENDER_RTP_ENDPOINT or DefenderRtpOptions.Endpoint).");
            }

            if (Timeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("DefenderRtpOptions.Timeout must be positive.");
            }

            if (MaxContentCharacters <= 0)
            {
                throw new InvalidOperationException("DefenderRtpOptions.MaxContentCharacters must be positive.");
            }

            if (string.IsNullOrWhiteSpace(AuthenticationScope))
            {
                throw new InvalidOperationException("DefenderRtpOptions.AuthenticationScope is required.");
            }
        }

        /// <summary>Whether the URL is absolute and uses HTTPS, so tokens and content never travel in clear text.</summary>
        /// <param name="url">The URL to check.</param>
        /// <returns>True for an absolute <c>https</c> URL.</returns>
        internal static bool IsHttpsUrl(Uri? url) =>
            url is { IsAbsoluteUri: true } && string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        private static int ParsePositive(string value, string name) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : throw new InvalidOperationException($"{name} must be a positive integer.");
    }
}
