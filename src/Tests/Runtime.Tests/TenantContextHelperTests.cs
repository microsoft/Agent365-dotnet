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
        [InlineData("tenant-claims", null, null, "tenant-claims")] // Claims only
        [InlineData(null, "tenant-headers", null, "tenant-headers")] // Headers only
        [InlineData(null, null, "tenant-items", "tenant-items")] // Items only
        [InlineData("tenant-claims", "tenant-headers", "tenant-items", "tenant-claims")] // Claims has priority
        [InlineData(null, "tenant-headers", "tenant-items", "tenant-headers")] // Headers has priority over items
        [InlineData("", "tenant-headers", null, "tenant-headers")] // Empty claim falls back to headers
        [InlineData("   ", "tenant-headers", null, "tenant-headers")] // Whitespace claim falls back to headers
        public void GetTenantId_WithVariousSources_ReturnsExpectedTenantId(
            string? claimValue,
            string? headerValue,
            string? itemValue,
            string? expectedTenantId)
        {
            // Arrange
            var context = CreateMockHttpContext();

            if (claimValue != null)
            {
                var claims = new List<Claim> { new(TenantContextHelper.TenantClaimName, claimValue) };
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            }

            if (headerValue != null)
            {
                context.Request.Headers[TenantContextHelper.TenantHeaderName] = headerValue;
            }

            if (itemValue != null)
            {
                context.Items[TenantContextHelper.TenantItemKey] = itemValue;
            }

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(expectedTenantId, result);
        }

        [Theory]
        [InlineData(12345, "12345")] // Integer to string
        [InlineData(null, null)] // Null item
        public void GetTenantId_WithVariousItemTypes_ReturnsExpectedResult(object? itemValue, string? expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.TenantItemKey] = itemValue;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void GetTenantId_WhenNoTenantFound_ReturnsNull()
        {
            // Arrange
            var context = CreateMockHttpContext();

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Null(result);
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
        [InlineData("worker-claims", null, null, "worker-claims")] // Claims only
        [InlineData(null, "worker-headers", null, "worker-headers")] // Headers only
        [InlineData(null, null, "worker-items", "worker-items")] // Items only
        [InlineData("worker-claims", "worker-headers", "worker-items", "worker-claims")] // Claims has priority
        [InlineData(null, "worker-headers", "worker-items", "worker-headers")] // Headers has priority over items
        [InlineData("", "worker-headers", null, "worker-headers")] // Empty claim falls back to headers
        [InlineData("   ", "worker-headers", null, "worker-headers")] // Whitespace claim falls back to headers
        public void GetWorkerId_WithVariousSources_ReturnsExpectedWorkerId(
            string? claimValue,
            string? headerValue,
            string? itemValue,
            string? expectedWorkerId)
        {
            // Arrange
            var context = CreateMockHttpContext();

            if (claimValue != null)
            {
                var claims = new List<Claim> { new(TenantContextHelper.WorkerClaimName, claimValue) };
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            }

            if (headerValue != null)
            {
                context.Request.Headers[TenantContextHelper.WorkerHeaderName] = headerValue;
            }

            if (itemValue != null)
            {
                context.Items[TenantContextHelper.WorkerItemKey] = itemValue;
            }

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(expectedWorkerId, result);
        }

        [Theory]
        [InlineData(12345, "12345")] // Integer to string
        [InlineData(null, null)] // Null item
        public void GetWorkerId_WithVariousItemTypes_ReturnsExpectedResult(object? itemValue, string? expectedResult)
        {
            // Arrange
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.WorkerItemKey] = itemValue;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void GetWorkerId_WhenNoWorkerFound_ReturnsNull()
        {
            // Arrange
            var context = CreateMockHttpContext();

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Null(result);
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