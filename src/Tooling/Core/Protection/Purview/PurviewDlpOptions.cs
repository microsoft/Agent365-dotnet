// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Purview
{
    using System;
    using System.Globalization;
    using System.Linq;

    /// <summary>How the agent's reply (<see cref="PurviewDlpActivity.DownloadText"/>) is sent to Purview.</summary>
    public enum PurviewDlpResponseMode
    {
        /// <summary>
        /// The reply is sent to Purview for audit without waiting, and Purview never blocks it. Purview DLP policies for
        /// custom AI apps restrict prompts (<c>uploadText</c>), not replies.
        /// </summary>
        Audit,

        /// <summary>The reply waits for Purview's verdict, which decides like the prompt's.</summary>
        Enforce,
    }

    /// <summary>
    /// Configuration for Microsoft Purview data loss prevention (DLP) and audit through the Microsoft Graph
    /// <c>processContent</c> API (<c>POST .../dataSecurityAndGovernance/processContent</c>).
    /// </summary>
    public sealed class PurviewDlpOptions
    {
        /// <summary>The default Microsoft Graph base URL.</summary>
        public const string DefaultGraphBaseUrl = "https://graph.microsoft.com/v1.0";

        /// <summary>The default token scope: Microsoft Graph.</summary>
        public const string DefaultAuthenticationScope = "https://graph.microsoft.com/.default";

        private static readonly string[] EnabledValues = { "true", "1", "yes", "on" };
        private static readonly string[] DisabledValues = { "false", "0", "no", "off" };

        /// <summary>
        /// Whether Purview DLP is enabled (<c>ENABLE_A365_PURVIEW_DLP</c>: <c>true</c> or <c>false</c>, or
        /// <c>1</c>/<c>0</c>, <c>yes</c>/<c>no</c>, <c>on</c>/<c>off</c>; unset means disabled).
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The Microsoft Graph base URL (<c>A365_PURVIEW_DLP_GRAPH_BASE_URL</c>), an absolute HTTPS URL without a query
        /// or fragment; defaults to <see cref="DefaultGraphBaseUrl"/>.
        /// </summary>
        public Uri GraphBaseUrl { get; set; } = new Uri(DefaultGraphBaseUrl);

        /// <summary>
        /// The token scope (<c>A365_PURVIEW_DLP_AUTHENTICATION_SCOPE</c>); defaults to
        /// <see cref="DefaultAuthenticationScope"/>. The token needs the Microsoft Graph permission
        /// <c>Content.Process.User</c> (delegated, for the agentic user) or <c>Content.Process.All</c>.
        /// </summary>
        public string AuthenticationScope { get; set; } = DefaultAuthenticationScope;

        /// <summary>
        /// The deadline for one evaluation, shared by token acquisition and the request
        /// (<c>A365_PURVIEW_DLP_TIMEOUT_MILLISECONDS</c>). When it passes, the result follows <see cref="FailClosed"/>.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// When true, an evaluation that returns no verdict blocks (<c>A365_PURVIEW_DLP_FAIL_MODE=closed</c>);
        /// otherwise (<c>open</c>, the default) it is allowed and reported as not evaluated.
        /// </summary>
        public bool FailClosed { get; set; }

        /// <summary>
        /// Maximum characters of text sent to Purview in one evaluation
        /// (<c>A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS</c>, default 100000). Longer text is cut and sent with
        /// <c>isTruncated</c> set: Purview's block of the cut text stands, but its allow does not cover what it did not
        /// see, so the result follows <see cref="FailClosed"/>.
        /// </summary>
        public int MaxContentCharacters { get; set; } = 100000;

        /// <summary>
        /// How replies are sent to Purview (<c>A365_PURVIEW_DLP_RESPONSE_MODE</c>: <c>audit</c>, the default, or
        /// <c>enforce</c>). Used by agent-hooks interceptors; the client evaluates whatever it is given.
        /// </summary>
        public PurviewDlpResponseMode ResponseMode { get; set; } = PurviewDlpResponseMode.Audit;

        /// <summary>Reads the options from the process environment.</summary>
        /// <returns>The configured options.</returns>
        /// <exception cref="InvalidOperationException">A variable is set to a value it does not accept.</exception>
        public static PurviewDlpOptions FromEnvironment() => FromEnvironment(Environment.GetEnvironmentVariable);

        /// <summary>
        /// Reads the options from the given variable lookup. A value a variable does not accept fails rather than
        /// falling back to a default, so a typo cannot quietly weaken protection: in particular,
        /// <c>ENABLE_A365_PURVIEW_DLP</c> must say true or false, <c>A365_PURVIEW_DLP_FAIL_MODE</c> must be
        /// <c>open</c> or <c>closed</c>, and <c>A365_PURVIEW_DLP_RESPONSE_MODE</c> must be <c>audit</c> or
        /// <c>enforce</c>.
        /// </summary>
        /// <param name="getVariable">Returns the value of an environment variable, or null.</param>
        /// <returns>The configured options.</returns>
        /// <exception cref="InvalidOperationException">A variable is set to a value it does not accept.</exception>
        public static PurviewDlpOptions FromEnvironment(Func<string, string?> getVariable)
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

            var options = new PurviewDlpOptions
            {
                Enabled = ParseEnabled(Read("ENABLE_A365_PURVIEW_DLP")),
                FailClosed = ParseFailMode(Read("A365_PURVIEW_DLP_FAIL_MODE")),
                ResponseMode = ParseResponseMode(Read("A365_PURVIEW_DLP_RESPONSE_MODE")),
            };

            if (Read("A365_PURVIEW_DLP_GRAPH_BASE_URL") is { } baseUrl)
            {
                options.GraphBaseUrl = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && IsHttpsBaseUrl(uri)
                    ? uri
                    : throw new InvalidOperationException(
                        "A365_PURVIEW_DLP_GRAPH_BASE_URL must be an absolute HTTPS URL without a query or fragment.");
            }

            if (Read("A365_PURVIEW_DLP_AUTHENTICATION_SCOPE") is { } scope)
            {
                options.AuthenticationScope = scope;
            }

            if (Read("A365_PURVIEW_DLP_TIMEOUT_MILLISECONDS") is { } timeout)
            {
                options.Timeout = TimeSpan.FromMilliseconds(ParsePositive(timeout, "A365_PURVIEW_DLP_TIMEOUT_MILLISECONDS"));
            }

            if (Read("A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS") is { } maximum)
            {
                options.MaxContentCharacters = ParsePositive(maximum, "A365_PURVIEW_DLP_MAX_CONTENT_CHARACTERS");
            }

            return options;
        }

        /// <summary>Throws when the options cannot be used by a client.</summary>
        internal void Validate()
        {
            if (!IsHttpsBaseUrl(GraphBaseUrl))
            {
                throw new InvalidOperationException(
                    "The Purview DLP Graph base URL must be an absolute HTTPS URL without a query or fragment "
                    + "(A365_PURVIEW_DLP_GRAPH_BASE_URL or PurviewDlpOptions.GraphBaseUrl).");
            }

            if (Timeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("PurviewDlpOptions.Timeout must be positive.");
            }

            if (MaxContentCharacters <= 0)
            {
                throw new InvalidOperationException("PurviewDlpOptions.MaxContentCharacters must be positive.");
            }

            if (string.IsNullOrWhiteSpace(AuthenticationScope))
            {
                throw new InvalidOperationException("PurviewDlpOptions.AuthenticationScope is required.");
            }

            if (ResponseMode is not (PurviewDlpResponseMode.Audit or PurviewDlpResponseMode.Enforce))
            {
                throw new InvalidOperationException("PurviewDlpOptions.ResponseMode must be Audit or Enforce.");
            }
        }

        /// <summary>
        /// Whether the URL can be the Graph base: absolute and HTTPS, so tokens and content never travel in clear
        /// text, with no query or fragment, so the API path appends to it.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <returns>True for an absolute <c>https</c> URL without a query or fragment.</returns>
        internal static bool IsHttpsBaseUrl(Uri? url) =>
            url is { IsAbsoluteUri: true }
            && string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && url.Query.Length == 0
            && url.Fragment.Length == 0;

        private static int ParsePositive(string value, string name) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : throw new InvalidOperationException($"{name} must be a positive integer.");

        /// <summary>
        /// Whether Purview DLP is enabled. Unset means disabled; a value that is not a recognized way of saying true
        /// or false is rejected, so a typo cannot quietly turn protection off.
        /// </summary>
        private static bool ParseEnabled(string? value)
        {
            if (value == null || DisabledValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (EnabledValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            throw new InvalidOperationException("ENABLE_A365_PURVIEW_DLP must be true or false.");
        }

        /// <summary>Whether the fail mode is closed. Unset means open; any value but open or closed is rejected.</summary>
        private static bool ParseFailMode(string? value)
        {
            if (value == null || value.Equals("open", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (value.Equals("closed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            throw new InvalidOperationException("A365_PURVIEW_DLP_FAIL_MODE must be \"open\" or \"closed\".");
        }

        /// <summary>The response mode. Unset means audit; any value but audit or enforce is rejected.</summary>
        private static PurviewDlpResponseMode ParseResponseMode(string? value)
        {
            if (value == null || value.Equals("audit", StringComparison.OrdinalIgnoreCase))
            {
                return PurviewDlpResponseMode.Audit;
            }

            if (value.Equals("enforce", StringComparison.OrdinalIgnoreCase))
            {
                return PurviewDlpResponseMode.Enforce;
            }

            throw new InvalidOperationException("A365_PURVIEW_DLP_RESPONSE_MODE must be \"audit\" or \"enforce\".");
        }
    }
}
