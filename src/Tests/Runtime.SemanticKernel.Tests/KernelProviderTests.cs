using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel;

namespace Microsoft.Agents.A365.Runtime.SemanticKernel.Tests
{
    /// <summary>
    /// Unit tests for KernelProvider focused on key functionality.
    /// </summary>
    public class KernelProviderTests : IDisposable
    {
        private readonly KernelProvider _kernelProvider;

        public KernelProviderTests()
        {
            _kernelProvider = new KernelProvider(builder => { });
        }

        [Fact]
        public void GetKernel_WithValidTenantAndWorker_ReturnsKernel()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string workerId = "worker1";

            // Act
            var kernel = _kernelProvider.GetKernel(tenantId, workerId);

            // Assert
            Assert.NotNull(kernel);
        }

        [Fact]
        public void GetKernel_SameTenantAndWorker_ReturnsCachedKernel()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string workerId = "worker1";

            // Act
            var kernel1 = _kernelProvider.GetKernel(tenantId, workerId);
            var kernel2 = _kernelProvider.GetKernel(tenantId, workerId);

            // Assert
            Assert.Same(kernel1, kernel2);
        }

        [Fact]
        public void GetKernel_DifferentTenants_ReturnsDifferentKernels()
        {
            // Arrange
            const string tenant1 = "tenant1";
            const string tenant2 = "tenant2";
            const string workerId = "worker1";

            // Act
            var kernel1 = _kernelProvider.GetKernel(tenant1, workerId);
            var kernel2 = _kernelProvider.GetKernel(tenant2, workerId);

            // Assert
            Assert.NotSame(kernel1, kernel2);
        }

        [Fact]
        public void GetKernel_DifferentWorkers_ReturnsDifferentKernels()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string worker1 = "worker1";
            const string worker2 = "worker2";

            // Act
            var kernel1 = _kernelProvider.GetKernel(tenantId, worker1);
            var kernel2 = _kernelProvider.GetKernel(tenantId, worker2);

            // Assert
            Assert.NotSame(kernel1, kernel2);
        }

        [Theory]
        [InlineData(null, "worker1")]
        [InlineData("tenant1", null)]
        public void GetKernel_WithNullParameters_ThrowsArgumentNullException(string? tenantId, string? workerId)
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _kernelProvider.GetKernel(tenantId!, workerId!));
        }

        [Theory]
        [InlineData("", "worker1")]
        [InlineData("tenant1", "")]
        [InlineData("   ", "worker1")]
        [InlineData("tenant1", "   ")]
        public void GetKernel_WithEmptyOrWhitespaceParameters_ThrowsArgumentException(string tenantId, string workerId)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _kernelProvider.GetKernel(tenantId, workerId));
        }

        [Fact]
        public void GetKernel_WithGovernanceDelegate_CallsOnCacheMiss()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string workerId = "worker1";
            var governanceCalled = false;
            
            Task GovernanceDelegate(Kernel kernel)
            {
                governanceCalled = true;
                return Task.CompletedTask;
            }

            // Act
            _kernelProvider.GetKernel(tenantId, workerId, GovernanceDelegate);

            // Assert
            Assert.True(governanceCalled);
        }

        [Fact]
        public void GetKernel_CachedKernelWithGovernance_DoesNotCallGovernanceAgain()
        {
            // Arrange
            const string tenantId = "tenant1";
            const string workerId = "worker1";
            var governanceCallCount = 0;
            
            Task GovernanceDelegate(Kernel kernel)
            {
                governanceCallCount++;
                return Task.CompletedTask;
            }

            // Act
            _kernelProvider.GetKernel(tenantId, workerId, GovernanceDelegate);
            _kernelProvider.GetKernel(tenantId, workerId, GovernanceDelegate);

            // Assert
            Assert.Equal(1, governanceCallCount);
        }

        [Fact]
        public void SetKernelBuilder_ChangesKernelConfiguration()
        {
            // Arrange
            var configCalled = false;
            
            void ConfigureKernel(IKernelBuilder builder)
            {
                configCalled = true;
            }

            // Act
            _kernelProvider.SetKernelBuilder(ConfigureKernel);
            _kernelProvider.GetKernel("tenant1", "worker1");

            // Assert
            Assert.True(configCalled);
        }

        [Fact]
        public void SetTemplateKernel_CopiesPluginsToNewKernels()
        {
            // Arrange
            var templateKernel = Kernel.CreateBuilder().Build();
            var testFunction = KernelFunctionFactory.CreateFromMethod(() => "test");
            templateKernel.Plugins.AddFromFunctions("TestPlugin", [testFunction]);

            // Act
            _kernelProvider.SetTemplateKernel(templateKernel);
            var newKernel = _kernelProvider.GetKernel("tenant1", "worker1");

            // Assert
            Assert.True(newKernel.Plugins.Count > 0);
            Assert.Contains(newKernel.Plugins, p => p.Name == "TestPlugin");
        }

        [Fact]
        public void Constructor_WithServiceProvider_IntegratesWithGovernanceFactory()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockFactory = new Mock<IGovernanceDelegateFactory>();
            var mockLogger = new Mock<ILogger<KernelProvider>>();
            
            mockFactory.Setup(f => f.CreateGovernanceDelegate(It.IsAny<ILogger>()))
                      .Returns((Kernel k) => Task.CompletedTask);
            
            services.AddSingleton(mockFactory.Object);
            services.AddSingleton(mockLogger.Object);
            
            var serviceProvider = services.BuildServiceProvider();

            // Act
            using var provider = new KernelProvider(builder => { }, serviceProvider);
            provider.GetKernel("tenant1", "worker1");

            // Assert
            mockFactory.Verify(f => f.CreateGovernanceDelegate(It.IsAny<ILogger>()), Times.Once);
        }

        public void Dispose()
        {
            _kernelProvider?.Dispose();
        }
    }
}