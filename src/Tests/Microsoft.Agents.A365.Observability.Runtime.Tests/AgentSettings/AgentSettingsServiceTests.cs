// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Observability.Runtime.AgentSettings;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.AgentSettings.Tests
{
    [TestClass]
    public class AgentSettingsServiceTests
    {
        private const string TestTenantId = "e3064512-cc6d-4703-be71-a2ecaecaa98a";
        private const string TestAgentType = "test-agent";
        private const string TestAgentInstanceId = "test-instance-123";
        private const string TestToken = "test-token";

        [TestMethod]
        public void Constructor_WithValidParameters_Succeeds()
        {
            // Arrange
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");

            // Act
            var service = new AgentSettingsService(apiDiscovery, TestTenantId);

            // Assert
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Constructor_WithNullApiDiscovery_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new AgentSettingsService(null!, TestTenantId));
        }

        [TestMethod]
        public void Constructor_WithNullTenantId_ThrowsArgumentNullException()
        {
            // Arrange
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new AgentSettingsService(apiDiscovery, null!));
        }

        [TestMethod]
        public async Task GetAgentSettingTemplateAsync_WithValidResponse_ReturnsTemplate()
        {
            // Arrange
            var expectedTemplate = new AgentSettingTemplate
            {
                AgentType = TestAgentType,
                Settings = new Dictionary<string, object?> { ["key1"] = "value1" }
            };

            var responseContent = JsonSerializer.Serialize(expectedTemplate);
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId, httpClient);

            // Act
            var result = await service.GetAgentSettingTemplateAsync(TestAgentType, TestToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestAgentType, result.AgentType);
            Assert.IsTrue(result.Settings.ContainsKey("key1"));
        }

        [TestMethod]
        public async Task GetAgentSettingTemplateAsync_WithNotFoundResponse_ReturnsNull()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.NotFound, string.Empty);
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId, httpClient);

            // Act
            var result = await service.GetAgentSettingTemplateAsync(TestAgentType, TestToken);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetAgentSettingTemplateAsync_WithNullAgentType_ThrowsArgumentNullException()
        {
            // Arrange
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                service.GetAgentSettingTemplateAsync(null!, TestToken));
        }

        [TestMethod]
        public async Task GetAgentSettingTemplateAsync_WithNullToken_ThrowsArgumentNullException()
        {
            // Arrange
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                service.GetAgentSettingTemplateAsync(TestAgentType, null!));
        }

        [TestMethod]
        public async Task SetAgentSettingTemplateAsync_WithValidTemplate_Succeeds()
        {
            // Arrange
            var template = new AgentSettingTemplate
            {
                AgentType = TestAgentType,
                Settings = new Dictionary<string, object?> { ["key1"] = "value1" }
            };

            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, string.Empty);
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId, httpClient);

            // Act
            await service.SetAgentSettingTemplateAsync(template, TestToken);

            // Assert - no exception thrown
        }

        [TestMethod]
        public async Task SetAgentSettingTemplateAsync_WithNullTemplate_ThrowsArgumentNullException()
        {
            // Arrange
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                service.SetAgentSettingTemplateAsync(null!, TestToken));
        }

        [TestMethod]
        public async Task SetAgentSettingTemplateAsync_WithEmptyAgentType_ThrowsArgumentException()
        {
            // Arrange
            var template = new AgentSettingTemplate
            {
                AgentType = string.Empty,
                Settings = new Dictionary<string, object?> { ["key1"] = "value1" }
            };

            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                service.SetAgentSettingTemplateAsync(template, TestToken));
        }

        [TestMethod]
        public async Task GetAgentSettingsAsync_WithValidResponse_ReturnsSettings()
        {
            // Arrange
            var expectedSettings = new Runtime.AgentSettings.AgentSettings
            {
                AgentInstanceId = TestAgentInstanceId,
                AgentType = TestAgentType,
                Settings = new Dictionary<string, object?> { ["key1"] = "value1" }
            };

            var responseContent = JsonSerializer.Serialize(expectedSettings);
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, responseContent);
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId, httpClient);

            // Act
            var result = await service.GetAgentSettingsAsync(TestAgentInstanceId, TestToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(TestAgentInstanceId, result.AgentInstanceId);
            Assert.AreEqual(TestAgentType, result.AgentType);
            Assert.IsTrue(result.Settings.ContainsKey("key1"));
        }

        [TestMethod]
        public async Task GetAgentSettingsAsync_WithNotFoundResponse_ReturnsNull()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.NotFound, string.Empty);
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId, httpClient);

            // Act
            var result = await service.GetAgentSettingsAsync(TestAgentInstanceId, TestToken);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task SetAgentSettingsAsync_WithValidSettings_Succeeds()
        {
            // Arrange
            var settings = new Runtime.AgentSettings.AgentSettings
            {
                AgentInstanceId = TestAgentInstanceId,
                AgentType = TestAgentType,
                Settings = new Dictionary<string, object?> { ["key1"] = "value1" }
            };

            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, string.Empty);
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId, httpClient);

            // Act
            await service.SetAgentSettingsAsync(settings, TestToken);

            // Assert - no exception thrown
        }

        [TestMethod]
        public async Task SetAgentSettingsAsync_WithNullSettings_ThrowsArgumentNullException()
        {
            // Arrange
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                service.SetAgentSettingsAsync(null!, TestToken));
        }

        [TestMethod]
        public async Task SetAgentSettingsAsync_WithEmptyAgentInstanceId_ThrowsArgumentException()
        {
            // Arrange
            var settings = new Runtime.AgentSettings.AgentSettings
            {
                AgentInstanceId = string.Empty,
                AgentType = TestAgentType,
                Settings = new Dictionary<string, object?> { ["key1"] = "value1" }
            };

            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                service.SetAgentSettingsAsync(settings, TestToken));
        }

        [TestMethod]
        public async Task GetAgentSettingTemplateAsync_ConstructsCorrectUrl()
        {
            // Arrange
            HttpRequestMessage? capturedRequest = null;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new AgentSettingTemplate
                    {
                        AgentType = TestAgentType,
                        Settings = new Dictionary<string, object?>()
                    }))
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId, httpClient);

            // Act
            await service.GetAgentSettingTemplateAsync(TestAgentType, TestToken);

            // Assert
            Assert.IsNotNull(capturedRequest);
            var expectedEndpoint = apiDiscovery.GetTenantEndpoint(TestTenantId);
            var expectedUrl = $"https://{expectedEndpoint}/agents/v1.0/settings/templates/{TestAgentType}";
            Assert.AreEqual(expectedUrl, capturedRequest.RequestUri?.ToString());
            Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        }

        [TestMethod]
        public async Task GetAgentSettingsAsync_ConstructsCorrectUrl()
        {
            // Arrange
            HttpRequestMessage? capturedRequest = null;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new Runtime.AgentSettings.AgentSettings
                    {
                        AgentInstanceId = TestAgentInstanceId,
                        AgentType = TestAgentType,
                        Settings = new Dictionary<string, object?>()
                    }))
                });

            var httpClient = new HttpClient(mockHandler.Object);
            var apiDiscovery = new PowerPlatformApiDiscovery("prod");
            var service = new AgentSettingsService(apiDiscovery, TestTenantId, httpClient);

            // Act
            await service.GetAgentSettingsAsync(TestAgentInstanceId, TestToken);

            // Assert
            Assert.IsNotNull(capturedRequest);
            var expectedEndpoint = apiDiscovery.GetTenantEndpoint(TestTenantId);
            var expectedUrl = $"https://{expectedEndpoint}/agents/v1.0/settings/instances/{TestAgentInstanceId}";
            Assert.AreEqual(expectedUrl, capturedRequest.RequestUri?.ToString());
            Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        }

        private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });

            return new HttpClient(mockHandler.Object);
        }
    }
}
