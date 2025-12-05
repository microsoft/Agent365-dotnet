// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Agents.A365.Runtime;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Agents.A365.Runtime.Tests
{
    /// <summary>
    /// Unit tests for TenantContextHelper class.
    /// Tests the extraction of tenant and worker IDs from various sources in HttpContext.
    /// </summary>
    public class TenantContextHelperTests
    {
        #region Constants Tests

        [Fact]
        public void Constants_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal("tenant_id", TenantContextHelper.TenantClaimName);
            Assert.Equal("worker_id", TenantContextHelper.WorkerClaimName);
            Assert.Equal("X-Tenant-Id", TenantContextHelper.TenantHeaderName);
            Assert.Equal("X-Worker-Id", TenantContextHelper.WorkerHeaderName);
            Assert.Equal("TenantId", TenantContextHelper.TenantItemKey);
            Assert.Equal("WorkerId", TenantContextHelper.WorkerItemKey);
        }

        #endregion

        #region GetTenantId Tests

        [Fact]
        public void GetTenantId_WhenContextIsNull_ReturnsNull()
        {
            // Arrange
            HttpContext? context = null;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("tenant-123", "tenant-123")]
        [InlineData("tenant-456", "tenant-456")]
        [InlineData("", "")] // Empty claim should return null (fallback behavior)
        [InlineData("   ", "")] // Whitespace claim should return null (fallback behavior)
        [InlineData("special-tenant-789", "special-tenant-789")]
        public void GetTenantId_WhenTenantInClaims_ReturnsExpectedResult(string claimValue, string? expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            var claims = new List<Claim>
            {
                new(TenantContextHelper.TenantClaimName, claimValue)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            if (string.IsNullOrWhiteSpace(expectedResult))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(expectedResult, result);
            }
        }

        [Theory]
        [InlineData("header-tenant-123", "header-tenant-123")]
        [InlineData("header-tenant-456", "header-tenant-456")]
        [InlineData("", "")] // Empty header should return null
        [InlineData("special-header-789", "special-header-789")]
        public void GetTenantId_WhenTenantInHeaders_ReturnsExpectedResult(string headerValue, string? expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            if (!string.IsNullOrEmpty(headerValue))
            {
                context.Request.Headers[TenantContextHelper.TenantHeaderName] = headerValue;
            }

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            if (string.IsNullOrWhiteSpace(expectedResult))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(expectedResult, result);
            }
        }

        [Theory]
        [InlineData("item-tenant-123", "item-tenant-123")]
        [InlineData("item-tenant-456", "item-tenant-456")]
        [InlineData(12345, "12345")] // Non-string values should be converted to string
        [InlineData("", "")] // Empty string should return null
        public void GetTenantId_WhenTenantInItems_ReturnsExpectedResult(object? itemValue, string? expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.TenantItemKey] = itemValue;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            if (string.IsNullOrWhiteSpace(expectedResult))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(expectedResult, result);
            }
        }

        [Theory]
        [InlineData("claims-tenant", "headers-tenant", "items-tenant", "claims-tenant")] // Claims takes priority
        [InlineData("", "headers-tenant", "items-tenant", "headers-tenant")] // Empty claims, headers take priority
        [InlineData("   ", "headers-tenant", "items-tenant", "headers-tenant")] // Whitespace claims, headers take priority
        public void GetTenantId_WithMultipleSources_ReturnsBasedOnPriority(
            string? claimValue, string? headerValue, string? itemValue, string expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();

            // Set up claims
            if (claimValue != null)
            {
                var claims = new List<Claim> { new(TenantContextHelper.TenantClaimName, claimValue) };
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
                context.User = principal;
            }

            // Set up headers
            if (headerValue != null)
            {
                context.Request.Headers[TenantContextHelper.TenantHeaderName] = headerValue;
            }

            // Set up items
            if (itemValue != null)
            {
                context.Items[TenantContextHelper.TenantItemKey] = itemValue;
            }

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        #endregion

        #region GetWorkerId Tests

        [Fact]
        public void GetWorkerId_WhenContextIsNull_ReturnsNull()
        {
            // Arrange
            HttpContext? context = null;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("worker-123", "worker-123")]
        [InlineData("worker-456", "worker-456")]
        [InlineData("", "")] // Empty claim should return null (fallback behavior)
        [InlineData("   ", "")] // Whitespace claim should return null (fallback behavior)
        [InlineData("special-worker-789", "special-worker-789")]
        public void GetWorkerId_WhenWorkerInClaims_ReturnsExpectedResult(string claimValue, string? expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            var claims = new List<Claim>
            {
                new(TenantContextHelper.WorkerClaimName, claimValue)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            if (string.IsNullOrWhiteSpace(expectedResult))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(expectedResult, result);
            }
        }

        [Theory]
        [InlineData("header-worker-123", "header-worker-123")]
        [InlineData("header-worker-456", "header-worker-456")]
        [InlineData("", "")] // Empty header should return null
        [InlineData("special-header-worker-789", "special-header-worker-789")]
        public void GetWorkerId_WhenWorkerInHeaders_ReturnsExpectedResult(string headerValue, string? expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            if (!string.IsNullOrEmpty(headerValue))
            {
                context.Request.Headers[TenantContextHelper.WorkerHeaderName] = headerValue;
            }

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            if (string.IsNullOrWhiteSpace(expectedResult))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(expectedResult, result);
            }
        }

        [Theory]
        [InlineData("item-worker-123", "item-worker-123")]
        [InlineData("item-worker-456", "item-worker-456")]
        [InlineData(12345, "12345")] // Non-string values should be converted to string
        [InlineData(67890, "67890")] // Another non-string test
        [InlineData("", "")] // Empty string should return null
        public void GetWorkerId_WhenWorkerInItems_ReturnsExpectedResult(object? itemValue, string? expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.WorkerItemKey] = itemValue;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            if (string.IsNullOrWhiteSpace(expectedResult))
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(expectedResult, result);
            }
        }

        [Theory]
        [InlineData("claims-worker", "headers-worker", "items-worker", "claims-worker")] // Claims takes priority
        [InlineData("", "headers-worker", "items-worker", "headers-worker")] // Empty claims, headers take priority
        [InlineData("   ", "headers-worker", "items-worker", "headers-worker")] // Whitespace claims, headers take priority
        [InlineData("SKIP", "headers-worker", "items-worker", "headers-worker")] // Skip claims, headers take priority
        [InlineData("SKIP", "", "items-worker", "items-worker")] // No claims or headers, items used
        public void GetWorkerId_WithMultipleSources_ReturnsBasedOnPriority(
            string? claimValue, string? headerValue, string? itemValue, string expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();

            // Set up claims
            if (claimValue != null && claimValue != "SKIP")
            {
                var claims = new List<Claim> { new(TenantContextHelper.WorkerClaimName, claimValue) };
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
                context.User = principal;
            }

            // Set up headers
            if (!string.IsNullOrEmpty(headerValue))
            {
                context.Request.Headers[TenantContextHelper.WorkerHeaderName] = headerValue;
            }

            // Set up items
            if (itemValue != null)
            {
                context.Items[TenantContextHelper.WorkerItemKey] = itemValue;
            }

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(12345, "12345")] // Both tenant and worker as integers
        [InlineData("string-value", "string-value")] // Both tenant and worker as strings
        public void GetTenantIdAndWorkerId_WhenItemsAreVariousTypes_ReturnExpectedResults(object itemValue, string expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.TenantItemKey] = itemValue;
            context.Items[TenantContextHelper.WorkerItemKey] = itemValue;

            // Act
            var tenantResult = TenantContextHelper.GetTenantId(context);
            var workerResult = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(expectedResult, tenantResult);
            Assert.Equal(expectedResult, workerResult);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a mock HttpContext with basic setup for testing.
        /// </summary>
        /// <returns>A configured HttpContext instance.</returns>
        private static HttpContext CreateMockHttpContext()
        {
            var context = new DefaultHttpContext();
            return context;
        }

        #endregion
    }
}