// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Purview
{
    using System;

    /// <summary>The Purview activity an evaluation reports (<c>activityMetadata.activity</c>).</summary>
    public enum PurviewDlpActivity
    {
        /// <summary>Content entering the agent, such as the user's prompt (<c>uploadText</c>).</summary>
        UploadText,

        /// <summary>Content the agent returns, such as its reply (<c>downloadText</c>).</summary>
        DownloadText,
    }

    /// <summary>The policy actions Purview returned for an evaluation.</summary>
    public sealed class PurviewDlpDecision
    {
        /// <summary>
        /// Whether a policy action blocks: its <c>restrictionAction</c> is <c>block</c> or its <c>action</c> is
        /// <c>blockAccess</c>.
        /// </summary>
        public bool BlockAction { get; init; }

        /// <summary>The <c>restrictionAction</c> of the blocking action, when it has one.</summary>
        public string? RestrictionAction { get; init; }

        /// <summary>How many policy actions Purview returned, blocking or not (for example <c>audit</c> or <c>warn</c>).</summary>
        public int ActionCount { get; init; }
    }

    /// <summary>
    /// The outcome of one Purview evaluation. <see cref="Evaluated"/> is false when no verdict was obtained;
    /// <see cref="Allowed"/> then follows <see cref="PurviewDlpOptions.FailClosed"/>, as it does when Purview allowed a
    /// <see cref="Truncated"/> copy.
    /// </summary>
    public sealed class PurviewDlpEvaluationResult
    {
        /// <summary>Whether the content may proceed.</summary>
        public bool Allowed { get; init; }

        /// <summary>Whether Purview returned a verdict.</summary>
        public bool Evaluated { get; init; }

        /// <summary>
        /// Whether Purview evaluated a truncated copy: the text was longer than
        /// <see cref="PurviewDlpOptions.MaxContentCharacters"/>. Its block stands; an allow does not cover what Purview
        /// did not see, so <see cref="Allowed"/> follows <see cref="PurviewDlpOptions.FailClosed"/> and
        /// <see cref="Error"/> is <see cref="PurviewDlpClient.TruncatedContentError"/>.
        /// </summary>
        public bool Truncated { get; init; }

        /// <summary>The activity that was evaluated.</summary>
        public PurviewDlpActivity Activity { get; init; }

        /// <summary>
        /// The <c>client-request-id</c> of the evaluation, a GUID unique to it and sent with its Graph call; Graph logs
        /// the call under it.
        /// </summary>
        public string CorrelationId { get; init; } = string.Empty;

        /// <summary>The conversation or session the content belongs to (the content entry's <c>correlationId</c>).</summary>
        public string? SessionId { get; init; }

        /// <summary>The policy actions Purview returned; empty when it returned none or no verdict was obtained.</summary>
        public PurviewDlpDecision Decision { get; init; } = new PurviewDlpDecision();

        /// <summary>Purview's <c>protectionScopeState</c> (<c>modified</c> or <c>notModified</c>), when returned.</summary>
        public string? ProtectionScopeState { get; init; }

        /// <summary>The HTTP status, when a response was received.</summary>
        public int? HttpStatus { get; init; }

        /// <summary>Time spent on the evaluation, including token acquisition.</summary>
        public TimeSpan Latency { get; init; }

        /// <summary>
        /// Why no verdict was obtained, or why Purview's allow does not stand, for example <c>http 403</c>. It never
        /// carries a response body or a token; for an exception it names only the exception's type.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>A user-facing reason when the content is blocked.</summary>
        public string? BlockReason { get; init; }
    }
}
