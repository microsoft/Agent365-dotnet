// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Azure;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs.Builders
{
    [TestClass]
    public class SpanStatusBuilderTests
    {
        [TestMethod]
        public void FromError_NullError_ReturnsUnset_AndDoesNotTouchAttributes()
        {
            var attributes = new Dictionary<string, object?>();

            var status = SpanStatusBuilder.FromError(null, attributes);

            status.Code.Should().Be(SpanStatusCode.Unset);
            status.Message.Should().BeNull();
            attributes.Should().NotContainKey(OpenTelemetryConstants.ErrorTypeKey);
        }

        [TestMethod]
        public void FromError_WithException_ReturnsError_WithMessage()
        {
            var error = new InvalidOperationException("boom");

            var status = SpanStatusBuilder.FromError(error);

            status.Code.Should().Be(SpanStatusCode.Error);
            status.Message.Should().Be("boom");
        }

        [TestMethod]
        public void FromError_WithException_WritesErrorTypeAttribute_AsFullTypeName()
        {
            var attributes = new Dictionary<string, object?>();
            var error = new TimeoutException("timed out");

            SpanStatusBuilder.FromError(error, attributes);

            attributes.Should().ContainKey(OpenTelemetryConstants.ErrorTypeKey);
            attributes[OpenTelemetryConstants.ErrorTypeKey].Should().Be(typeof(TimeoutException).FullName);
        }

        [TestMethod]
        public void FromError_RequestFailedException_UsesHttpStatusAsErrorType()
        {
            var attributes = new Dictionary<string, object?>();
            var error = new RequestFailedException(404, "not found");

            var status = SpanStatusBuilder.FromError(error, attributes);

            status.Code.Should().Be(SpanStatusCode.Error);
            attributes[OpenTelemetryConstants.ErrorTypeKey].Should().Be("404");
        }

        [TestMethod]
        public void FromError_NullAttributes_DoesNotThrow()
        {
            var act = () => SpanStatusBuilder.FromError(new Exception("x"), null);

            act.Should().NotThrow();
        }
    }
}
