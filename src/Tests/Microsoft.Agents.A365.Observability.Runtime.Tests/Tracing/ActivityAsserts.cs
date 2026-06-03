// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing;

using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Primitives;

public static class ActivityAsserts
{
    public static AndConstraint<StringAssertions> ShouldHaveTag(this Activity activity, string key, string value)
    {
        return activity.Tags.Should().ContainKey(key, $"Activity should have tag '{key}'")
            .WhoseValue.Should().Be(value, $"Activity tag '{key}' should be '{value}'");
    }

    public static AndConstraint<StringAssertions> ShouldHaveTagContaining(this Activity activity, string key, string substring)
    {
        return activity.Tags.Should().ContainKey(key, $"Activity should have tag '{key}'")
            .WhoseValue.Should().Contain(substring, $"Activity tag '{key}' should contain '{substring}'");
    }

    public static void ShouldBeError(this Activity activity, string expectedMessage)
    {
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be(expectedMessage);
    }
}