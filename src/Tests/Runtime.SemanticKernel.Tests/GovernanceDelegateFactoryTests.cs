using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel;

namespace Microsoft.Agents.A365.Runtime.SemanticKernel.Tests
{
    /// <summary>
    /// Unit tests for GovernanceDelegateFactory.
    /// </summary>
    public class GovernanceDelegateFactoryTests
    {
        [Fact]
        public void Constructor_WithValidServiceProvider_ShouldNotThrow()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            // Act & Assert
            var factory = new GovernanceDelegateFactory(serviceProvider);
            Assert.NotNull(factory);
        }

        [Fact]
        public void CreateGovernanceDelegate_ShouldReturnValidDelegate()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var factory = new GovernanceDelegateFactory(serviceProvider);

            // Act
            var governanceDelegate = factory.CreateGovernanceDelegate();

            // Assert
            Assert.NotNull(governanceDelegate);
        }

        [Fact]
        public async Task GovernanceDelegate_ShouldAddInvocationFilter()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var factory = new GovernanceDelegateFactory(serviceProvider);
            var kernel = Kernel.CreateBuilder().Build();
            var initialFilterCount = kernel.FunctionInvocationFilters.Count;

            // Act
            var governanceDelegate = factory.CreateGovernanceDelegate();
            await governanceDelegate(kernel);

            // Assert
            Assert.True(kernel.FunctionInvocationFilters.Count > initialFilterCount);
            Assert.Contains(kernel.FunctionInvocationFilters, 
                filter => filter.GetType().Name == "FunctionInvocationFilter");
        }

        [Fact]
        public async Task GovernanceDelegate_WithLogger_ShouldLogInformation()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var factory = new GovernanceDelegateFactory(serviceProvider);
            var kernel = Kernel.CreateBuilder().Build();

            // Act
            var governanceDelegate = factory.CreateGovernanceDelegate(mockLogger.Object);
            await governanceDelegate(kernel);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("APPLYING KERNEL FUNCTION GOVERNANCE")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GovernanceDelegate_WithKernelContainingPlugins_ShouldLogPluginCount()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var factory = new GovernanceDelegateFactory(serviceProvider);
            var kernel = Kernel.CreateBuilder().Build();
            
            // Add a plugin to test logging
            var testFunction = KernelFunctionFactory.CreateFromMethod(() => "test");
            kernel.Plugins.AddFromFunctions("TestPlugin", [testFunction]);

            // Act
            var governanceDelegate = factory.CreateGovernanceDelegate(mockLogger.Object);
            await governanceDelegate(kernel);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Post-Governance Plugin Count")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GovernanceDelegate_WhenExceptionThrown_ShouldLogErrorAndRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var factory = new GovernanceDelegateFactory(serviceProvider);
            
            // Create a kernel that will cause an exception (simulate by passing null)
            Kernel? nullKernel = null;

            // Act & Assert
            var governanceDelegate = factory.CreateGovernanceDelegate(mockLogger.Object);
            
            await Assert.ThrowsAsync<NullReferenceException>(
                () => governanceDelegate(nullKernel!));

            // Verify error was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to apply kernel function governance")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}