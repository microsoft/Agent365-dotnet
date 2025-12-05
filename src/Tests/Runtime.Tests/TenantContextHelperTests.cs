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

        [Fact]
        public void GetTenantId_WhenTenantInClaims_ReturnsTenantFromClaims()
        {
            // Arrange
            const string expectedTenantId = "tenant-123";
            var context = CreateMockHttpContext();
            var claims = new List<Claim>
            {
                new(TenantContextHelper.TenantClaimName, expectedTenantId)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(expectedTenantId, result);
        }

        [Fact]
        public void GetTenantId_WhenTenantInHeaders_ReturnsTenantFromHeaders()
        {
            // Arrange
            const string expectedTenantId = "tenant-456";
            var context = CreateMockHttpContext();
            context.Request.Headers[TenantContextHelper.TenantHeaderName] = expectedTenantId;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(expectedTenantId, result);
        }

        [Fact]
        public void GetTenantId_WhenTenantInItems_ReturnsTenantFromItems()
        {
            // Arrange
            const string expectedTenantId = "tenant-789";
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.TenantItemKey] = expectedTenantId;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(expectedTenantId, result);
        }

        [Fact]
        public void GetTenantId_WhenTenantInMultipleSources_PrioritizesClaims()
        {
            // Arrange
            const string tenantFromClaims = "tenant-claims";
            const string tenantFromHeaders = "tenant-headers";
            const string tenantFromItems = "tenant-items";

            var context = CreateMockHttpContext();
            
            // Set up claims
            var claims = new List<Claim>
            {
                new(TenantContextHelper.TenantClaimName, tenantFromClaims)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Set up headers
            context.Request.Headers[TenantContextHelper.TenantHeaderName] = tenantFromHeaders;

            // Set up items
            context.Items[TenantContextHelper.TenantItemKey] = tenantFromItems;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(tenantFromClaims, result);
        }

        [Fact]
        public void GetTenantId_WhenTenantInHeadersAndItems_PrioritizesHeaders()
        {
            // Arrange
            const string tenantFromHeaders = "tenant-headers";
            const string tenantFromItems = "tenant-items";

            var context = CreateMockHttpContext();
            
            // Set up headers
            context.Request.Headers[TenantContextHelper.TenantHeaderName] = tenantFromHeaders;

            // Set up items
            context.Items[TenantContextHelper.TenantItemKey] = tenantFromItems;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(tenantFromHeaders, result);
        }

        [Fact]
        public void GetTenantId_WhenTenantClaimIsEmpty_FallsBackToHeaders()
        {
            // Arrange
            const string tenantFromHeaders = "tenant-headers";
            var context = CreateMockHttpContext();

            // Set up empty claim
            var claims = new List<Claim>
            {
                new(TenantContextHelper.TenantClaimName, "")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Set up headers
            context.Request.Headers[TenantContextHelper.TenantHeaderName] = tenantFromHeaders;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(tenantFromHeaders, result);
        }

        [Fact]
        public void GetTenantId_WhenTenantClaimIsWhitespace_FallsBackToHeaders()
        {
            // Arrange
            const string tenantFromHeaders = "tenant-headers";
            var context = CreateMockHttpContext();

            // Set up whitespace claim
            var claims = new List<Claim>
            {
                new(TenantContextHelper.TenantClaimName, "   ")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Set up headers
            context.Request.Headers[TenantContextHelper.TenantHeaderName] = tenantFromHeaders;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(tenantFromHeaders, result);
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

        [Fact]
        public void GetTenantId_WhenUserIsNull_FallsBackToHeaders()
        {
            // Arrange
            const string tenantFromHeaders = "tenant-headers";
            var context = CreateMockHttpContext();
            context.User = new ClaimsPrincipal();
            context.Request.Headers[TenantContextHelper.TenantHeaderName] = tenantFromHeaders;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal(tenantFromHeaders, result);
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

        [Fact]
        public void GetWorkerId_WhenWorkerInClaims_ReturnsWorkerFromClaims()
        {
            // Arrange
            const string expectedWorkerId = "worker-123";
            var context = CreateMockHttpContext();
            var claims = new List<Claim>
            {
                new(TenantContextHelper.WorkerClaimName, expectedWorkerId)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(expectedWorkerId, result);
        }

        [Fact]
        public void GetWorkerId_WhenWorkerInHeaders_ReturnsWorkerFromHeaders()
        {
            // Arrange
            const string expectedWorkerId = "worker-456";
            var context = CreateMockHttpContext();
            context.Request.Headers[TenantContextHelper.WorkerHeaderName] = expectedWorkerId;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(expectedWorkerId, result);
        }

        [Fact]
        public void GetWorkerId_WhenWorkerInItems_ReturnsWorkerFromItems()
        {
            // Arrange
            const string expectedWorkerId = "worker-789";
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.WorkerItemKey] = expectedWorkerId;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(expectedWorkerId, result);
        }

        [Fact]
        public void GetWorkerId_WhenWorkerInMultipleSources_PrioritizesClaims()
        {
            // Arrange
            const string workerFromClaims = "worker-claims";
            const string workerFromHeaders = "worker-headers";
            const string workerFromItems = "worker-items";

            var context = CreateMockHttpContext();
            
            // Set up claims
            var claims = new List<Claim>
            {
                new(TenantContextHelper.WorkerClaimName, workerFromClaims)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Set up headers
            context.Request.Headers[TenantContextHelper.WorkerHeaderName] = workerFromHeaders;

            // Set up items
            context.Items[TenantContextHelper.WorkerItemKey] = workerFromItems;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(workerFromClaims, result);
        }

        [Fact]
        public void GetWorkerId_WhenWorkerInHeadersAndItems_PrioritizesHeaders()
        {
            // Arrange
            const string workerFromHeaders = "worker-headers";
            const string workerFromItems = "worker-items";

            var context = CreateMockHttpContext();
            
            // Set up headers
            context.Request.Headers[TenantContextHelper.WorkerHeaderName] = workerFromHeaders;

            // Set up items
            context.Items[TenantContextHelper.WorkerItemKey] = workerFromItems;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(workerFromHeaders, result);
        }

        [Fact]
        public void GetWorkerId_WhenWorkerClaimIsEmpty_FallsBackToHeaders()
        {
            // Arrange
            const string workerFromHeaders = "worker-headers";
            var context = CreateMockHttpContext();

            // Set up empty claim
            var claims = new List<Claim>
            {
                new(TenantContextHelper.WorkerClaimName, "")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Set up headers
            context.Request.Headers[TenantContextHelper.WorkerHeaderName] = workerFromHeaders;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(workerFromHeaders, result);
        }

        [Fact]
        public void GetWorkerId_WhenWorkerClaimIsWhitespace_FallsBackToHeaders()
        {
            // Arrange
            const string workerFromHeaders = "worker-headers";
            var context = CreateMockHttpContext();

            // Set up whitespace claim
            var claims = new List<Claim>
            {
                new(TenantContextHelper.WorkerClaimName, "   ")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            context.User = principal;

            // Set up headers
            context.Request.Headers[TenantContextHelper.WorkerHeaderName] = workerFromHeaders;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(workerFromHeaders, result);
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

        [Fact]
        public void GetWorkerId_WhenUserIsNull_FallsBackToHeaders()
        {
            // Arrange
            const string workerFromHeaders = "worker-headers";
            var context = CreateMockHttpContext();
            context.User = new ClaimsPrincipal();
            context.Request.Headers[TenantContextHelper.WorkerHeaderName] = workerFromHeaders;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal(workerFromHeaders, result);
        }

        [Fact]
        public void GetWorkerId_WhenItemValueIsNonString_ReturnsStringRepresentation()
        {
            // Arrange
            const int workerIdAsInt = 12345;
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.WorkerItemKey] = workerIdAsInt;

            // Act
            var result = TenantContextHelper.GetWorkerId(context);

            // Assert
            Assert.Equal("12345", result);
        }

        [Fact]
        public void GetTenantId_WhenItemValueIsNonString_ReturnsStringRepresentation()
        {
            // Arrange
            const int tenantIdAsInt = 67890;
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.TenantItemKey] = tenantIdAsInt;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Equal("67890", result);
        }

        [Fact]
        public void GetTenantId_WhenItemValueIsNull_ReturnsNull()
        {
            // Arrange
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.TenantItemKey] = null;

            // Act
            var result = TenantContextHelper.GetTenantId(context);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetWorkerId_WhenItemValueIsNull_ReturnsNull()
        {
            // Arrange
            var context = CreateMockHttpContext();
            context.Items[TenantContextHelper.WorkerItemKey] = null;

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