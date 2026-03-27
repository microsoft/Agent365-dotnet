// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Etw;
using System.Diagnostics.Tracing;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Etw
{
    [TestClass]
    public class EtwEventSourceConfigureTests
    {
        [TestInitialize]
        public void Setup()
        {
            EtwEventSource.ResetForTesting();
        }

        [TestCleanup]
        public void Cleanup()
        {
            EtwEventSource.ResetForTesting();
        }

        [TestMethod]
        public void Initialize_Default_CreatesInstanceWithThrowOnErrors()
        {
            // Arrange & Act
            EtwEventSource.Initialize();
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsTrue(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Initialize_WithSuppressTrue_CreatesInstanceWithoutThrowOnErrors()
        {
            // Arrange & Act
            EtwEventSource.Initialize(suppressThrowOnEventWriteErrors: true);
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsFalse(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Log_WithoutInitialize_CreatesDefaultInstanceWithThrowOnErrors()
        {
            // Act
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsTrue(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Initialize_AfterLogAccess_ThrowsInvalidOperationException()
        {
            // Arrange - force singleton creation
            _ = EtwEventSource.Log;

            // Act & Assert
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                EtwEventSource.Initialize());

            StringAssert.Contains(ex.Message, "Initialize()");
            StringAssert.Contains(ex.Message, "before the first access");
        }

        [TestMethod]
        public void Initialize_CalledTwice_ThrowsInvalidOperationException()
        {
            // Arrange
            EtwEventSource.Initialize();

            // Act & Assert
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                EtwEventSource.Initialize());

            StringAssert.Contains(ex.Message, "already been initialized");
        }
    }
}
