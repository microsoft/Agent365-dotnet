// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Hosting.Middleware;
using Microsoft.Agents.Builder;
using Moq;

namespace Microsoft.Agents.A365.Observability.Hosting.Tests.Middleware;

[TestClass]
public class ObservabilityMiddlewareExtensionsTests
{
    [TestMethod]
    public void UseObservabilityMiddleware_RegistersBothMiddlewares_ByDefault()
    {
        // Arrange
        var mockAdapter = new Mock<IChannelAdapter>();
        mockAdapter.Setup(a => a.Use(It.IsAny<IMiddleware>())).Returns(mockAdapter.Object);

        // Act
        mockAdapter.Object.UseObservabilityMiddleware();

        // Assert
        mockAdapter.Verify(a => a.Use(It.IsAny<BaggageTurnMiddleware>()), Times.Once);
        mockAdapter.Verify(a => a.Use(It.IsAny<OutputLoggingMiddleware>()), Times.Once);
    }

    [TestMethod]
    public void UseObservabilityMiddleware_RegistersBaggageOnly_WhenOutputLoggingDisabled()
    {
        // Arrange
        var mockAdapter = new Mock<IChannelAdapter>();
        mockAdapter.Setup(a => a.Use(It.IsAny<IMiddleware>())).Returns(mockAdapter.Object);

        // Act
        mockAdapter.Object.UseObservabilityMiddleware(enableOutputLogging: false);

        // Assert
        mockAdapter.Verify(a => a.Use(It.IsAny<BaggageTurnMiddleware>()), Times.Once);
        mockAdapter.Verify(a => a.Use(It.IsAny<OutputLoggingMiddleware>()), Times.Never);
    }

    [TestMethod]
    public void UseObservabilityMiddleware_RegistersOutputLoggingOnly_WhenBaggageDisabled()
    {
        // Arrange
        var mockAdapter = new Mock<IChannelAdapter>();
        mockAdapter.Setup(a => a.Use(It.IsAny<IMiddleware>())).Returns(mockAdapter.Object);

        // Act
        mockAdapter.Object.UseObservabilityMiddleware(enableBaggage: false);

        // Assert
        mockAdapter.Verify(a => a.Use(It.IsAny<BaggageTurnMiddleware>()), Times.Never);
        mockAdapter.Verify(a => a.Use(It.IsAny<OutputLoggingMiddleware>()), Times.Once);
    }

    [TestMethod]
    public void UseObservabilityMiddleware_RegistersNothing_WhenBothDisabled()
    {
        // Arrange
        var mockAdapter = new Mock<IChannelAdapter>();
        mockAdapter.Setup(a => a.Use(It.IsAny<IMiddleware>())).Returns(mockAdapter.Object);

        // Act
        mockAdapter.Object.UseObservabilityMiddleware(enableBaggage: false, enableOutputLogging: false);

        // Assert
        mockAdapter.Verify(a => a.Use(It.IsAny<IMiddleware>()), Times.Never);
    }

    [TestMethod]
    public void UseObservabilityMiddleware_ThrowsOnNullAdapter()
    {
        // Arrange
        IChannelAdapter adapter = null!;

        // Act & Assert
        var act = () => adapter.UseObservabilityMiddleware();
        act.Should().Throw<System.ArgumentNullException>()
           .WithParameterName("adapter");
    }

    [TestMethod]
    public void UseObservabilityMiddleware_ReturnsAdapter_ForChaining()
    {
        // Arrange
        var mockAdapter = new Mock<IChannelAdapter>();
        mockAdapter.Setup(a => a.Use(It.IsAny<IMiddleware>())).Returns(mockAdapter.Object);

        // Act
        var result = mockAdapter.Object.UseObservabilityMiddleware();

        // Assert
        result.Should().BeSameAs(mockAdapter.Object);
    }
}
