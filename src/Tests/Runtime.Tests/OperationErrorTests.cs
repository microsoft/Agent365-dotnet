// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for OperationError class.
    /// Tests error construction, exception handling, and string representation.
    /// </summary>
    public class OperationErrorTests
    {
        [Fact]
        public void Constructor_WithValidException_CreatesOperationError()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var error = new OperationError(exception);

            // Assert
            error.Should().NotBeNull();
            error.Exception.Should().BeSameAs(exception);
            error.Message.Should().Be("Test error");
        }

        [Fact]
        public void Constructor_WithNullException_ThrowsArgumentNullException()
        {
            // Act
            Action act = () => new OperationError(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("exception");
        }

        [Fact]
        public void Message_ReturnsExceptionMessage()
        {
            // Arrange
            var exception = new ArgumentException("Invalid argument provided");
            var error = new OperationError(exception);

            // Act
            var message = error.Message;

            // Assert
            message.Should().Be("Invalid argument provided");
        }

        [Fact]
        public void ToString_ReturnsExceptionToString()
        {
            // Arrange
            var exception = new InvalidOperationException("Operation failed");
            var error = new OperationError(exception);

            // Act
            var result = error.ToString();

            // Assert
            result.Should().Contain("System.InvalidOperationException");
            result.Should().Contain("Operation failed");
        }

        [Fact]
        public void Exception_PreservesExceptionType()
        {
            // Arrange
            var httpException = new HttpRequestException("Network error");
            var error = new OperationError(httpException);

            // Act & Assert
            error.Exception.Should().BeOfType<HttpRequestException>();
            error.Exception.Message.Should().Be("Network error");
        }

        [Fact]
        public void Exception_PreservesInnerException()
        {
            // Arrange
            var innerException = new InvalidOperationException("Inner error");
            var outerException = new ApplicationException("Outer error", innerException);
            var error = new OperationError(outerException);

            // Act & Assert
            error.Exception.InnerException.Should().BeSameAs(innerException);
            error.Exception.InnerException!.Message.Should().Be("Inner error");
        }

        [Fact]
        public void Message_WithExceptionWithEmptyMessage_ReturnsEmptyString()
        {
            // Arrange
            var exception = new Exception("");
            var error = new OperationError(exception);

            // Act
            var message = error.Message;

            // Assert
            message.Should().BeEmpty();
        }
    }
}
