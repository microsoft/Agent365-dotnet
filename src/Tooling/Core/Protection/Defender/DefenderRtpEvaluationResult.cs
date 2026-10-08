// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Defender
{
    using System;
    using System.Collections.Generic;

    /// <summary>A warning attached to a Defender verdict.</summary>
    /// <param name="Reason">Machine-readable reason, for example <c>prevention_annotated</c>.</param>
    /// <param name="Message">Human-readable message.</param>
    public sealed record DefenderRtpWarning(string? Reason, string? Message);

    /// <summary>The agent-hooks verdict returned by the Defender prevention endpoint.</summary>
    public sealed class DefenderRtpVerdict
    {
        /// <summary><c>allow</c>, <c>deny</c>, or <c>transform</c>.</summary>
        public string Decision { get; init; } = "allow";

        /// <summary>Defender's reason, for example <c>prevention_blocked</c>.</summary>
        public string? Reason { get; init; }

        /// <summary>Defender's message for the block.</summary>
        public string? Message { get; init; }

        /// <summary>Warnings, for example an annotation or a point that is not evaluated.</summary>
        public IReadOnlyList<DefenderRtpWarning> Warnings { get; init; } = Array.Empty<DefenderRtpWarning>();

        /// <summary>Threat labels, for example <c>PromptInjection</c>.</summary>
        public IReadOnlyList<string> ResultLabels { get; init; } = Array.Empty<string>();

        /// <summary>For <c>transform</c>: the JSON pointer of the content to rewrite.</summary>
        public string? TransformPath { get; init; }
    }

    /// <summary>
    /// The outcome of one Defender evaluation. <see cref="Evaluated"/> is false when no verdict was
    /// obtained; <see cref="Allowed"/> then follows <see cref="DefenderRtpOptions.FailClosed"/>, as it does when
    /// Defender allowed a <see cref="Truncated"/> copy.
    /// </summary>
    public sealed class DefenderRtpEvaluationResult
    {
        /// <summary>Whether the action may proceed.</summary>
        public bool Allowed { get; init; }

        /// <summary>Whether Defender returned a verdict.</summary>
        public bool Evaluated { get; init; }

        /// <summary>
        /// Whether the content under decision was longer than <see cref="DefenderRtpOptions.MaxContentCharacters"/>,
        /// so Defender evaluated a truncated copy. Its deny stands; an allow does not cover the content that was cut,
        /// so <see cref="Allowed"/> follows <see cref="DefenderRtpOptions.FailClosed"/> and <see cref="Error"/> is
        /// <see cref="DefenderRtpClient.TruncatedContentError"/>.
        /// </summary>
        public bool Truncated { get; init; }

        /// <summary>The agent-hooks interception point that was evaluated.</summary>
        public string InterceptionPoint { get; init; } = string.Empty;

        /// <summary>The <c>x-ms-correlation-id</c> sent with the call; Defender logs the evaluation under it.</summary>
        public string CorrelationId { get; init; } = string.Empty;

        /// <summary>The agent-hooks <c>session.id</c>.</summary>
        public string? SessionId { get; init; }

        /// <summary>Defender's verdict, when one was returned.</summary>
        public DefenderRtpVerdict? Verdict { get; init; }

        /// <summary>The HTTP status, when a response was received.</summary>
        public int? HttpStatus { get; init; }

        /// <summary>Why no verdict was obtained, for example <c>http 403: ...</c>.</summary>
        public string? Error { get; init; }

        /// <summary>Time spent on the evaluation call.</summary>
        public TimeSpan Latency { get; init; }

        /// <summary>A user-facing reason when the action is blocked.</summary>
        public string? BlockReason { get; init; }
    }
}
