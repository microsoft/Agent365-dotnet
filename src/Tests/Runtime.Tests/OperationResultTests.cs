// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using FluentAssertions;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for OperationResult class.
    /// Tests success and failure scenarios, error collection handling, and string representation.
    /// </summary>
    public class OperationResultTests
    {
        [Fact]
        public void Success_ReturnsSuccessfulResult()
        {
            // Act
            var result = OperationResult.Success;

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Success_ReturnsSameInstance()
        {
            // Act
            var result1 = OperationResult.Success;
            var result2 = OperationResult.Success;

            // Assert
            result1.Should().BeSameAs(result2);
        }

        [Fact]
        public void Failed_WithNoErrors_ReturnsFailedResult()
        {
            // Act
            var result = OperationResult.Failed();

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Failed_WithSingleError_ReturnsFailedResultWithError()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");
            var error = new OperationError(exception);

            // Act
            var result = OperationResult.Failed(error);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            result.Errors.First().Should().BeSameAs(error);
            result.Errors.First().Exception.Should().BeSameAs(exception);
            result.Errors.First().Message.Should().Be("Test error");
        }

        [Fact]
        public void Failed_WithMultipleErrors_ReturnsFailedResultWithAllErrors()
        {
            // Arrange
            var exception1 = new InvalidOperationException("Error 1");
            var exception2 = new ArgumentException("Error 2");
            var error1 = new OperationError(exception1);
            var error2 = new OperationError(exception2);

            // Act
            var result = OperationResult.Failed(error1, error2);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().HaveCount(2);
            result.Errors.Should().Contain(error1);
            result.Errors.Should().Contain(error2);
        }

        [Fact]
        public void Failed_WithNullErrorsArray_ReturnsFailedResultWithEmptyErrors()
        {
            // Act
            var result = OperationResult.Failed(null!);

            // Assert
            result.Should().NotBeNull();
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ToString_ForSuccess_ReturnsSucceeded()
        {
            // Arrange
            var result = OperationResult.Success;

            // Act
            var stringResult = result.ToString();

            // Assert
            stringResult.Should().Be("Succeeded");
        }

        [Fact]
        public void ToString_ForFailureWithNoErrors_ReturnsFailed()
        {
            // Arrange
            var result = OperationResult.Failed();

            // Act
            var stringResult = result.ToString();

            // Assert
            stringResult.Should().Be("Failed : ");
        }

        [Fact]
        public void ToString_ForFailureWithSingleError_ReturnsFailedWithMessage()
        {
            // Arrange
            var exception = new InvalidOperationException("Something went wrong");
            var result = OperationResult.Failed(new OperationError(exception));

            // Act
            var stringResult = result.ToString();

            // Assert
            stringResult.Should().Be("Failed : Something went wrong");
        }

        [Fact]
        public void ToString_ForFailureWithMultipleErrors_ReturnsFailedWithCommaSeparatedMessages()
        {
            // Arrange
            var exception1 = new InvalidOperationException("Error 1");
            var exception2 = new ArgumentException("Error 2");
            var result = OperationResult.Failed(
                new OperationError(exception1),
                new OperationError(exception2));

            // Act
            var stringResult = result.ToString();

            // Assert
            stringResult.Should().Be("Failed : Error 1, Error 2");
        }

        [Fact]
        public void Errors_IsEnumerable()
        {
            // Arrange
            var exception1 = new InvalidOperationException("Error 1");
            var exception2 = new ArgumentException("Error 2");
            var result = OperationResult.Failed(
                new OperationError(exception1),
                new OperationError(exception2));

            // Act
            var errorMessages = result.Errors.Select(e => e.Message).ToList();

            // Assert
            errorMessages.Should().HaveCount(2);
            errorMessages.Should().Contain("Error 1");
            errorMessages.Should().Contain("Error 2");
        }

        [Fact]
        public void Failed_ReturnsNewInstanceEachTime()
        {
            // Arrange
            var exception = new InvalidOperationException("Test");
            var error = new OperationError(exception);

            // Act
            var result1 = OperationResult.Failed(error);
            var result2 = OperationResult.Failed(error);

            // Assert
            result1.Should().NotBeSameAs(result2);
        }

        [Fact]
        public void Errors_WhenNoErrors_ReturnsEmptyEnumerable()
        {
            // Arrange
            var successResult = OperationResult.Success;
            var failedResult = OperationResult.Failed();

            // Act & Assert
            successResult.Errors.Should().BeEmpty();
            successResult.Errors.Should().NotBeNull();
            failedResult.Errors.Should().BeEmpty();
            failedResult.Errors.Should().NotBeNull();
        }

        [Fact]
        public void Failed_WithHttpRequestException_StoresException()
        {
            // Arrange
            var httpException = new HttpRequestException("Request failed", null, HttpStatusCode.BadRequest);
            var error = new OperationError(httpException);

            // Act
            var result = OperationResult.Failed(error);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            var storedError = result.Errors.First();
            storedError.Exception.Should().BeOfType<HttpRequestException>();
            var storedHttpEx = (HttpRequestException)storedError.Exception;
            storedHttpEx.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
