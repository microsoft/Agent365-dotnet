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
        public void Initialize_WithTrue_CreatesInstanceWithThrowOnErrors()
        {
            // Arrange & Act
            EtwEventSource.Initialize(throwOnEventWriteErrors: true);
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsTrue(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Initialize_WithFalse_CreatesInstanceWithoutThrowOnErrors()
        {
            // Arrange & Act
            EtwEventSource.Initialize(throwOnEventWriteErrors: false);
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsFalse(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Log_WithoutInitialize_CreatesDefaultInstance()
        {
            // Act
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsFalse(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Initialize_AfterLogAccess_ThrowsInvalidOperationException()
        {
            // Arrange - force singleton creation
            _ = EtwEventSource.Log;

            // Act & Assert
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                EtwEventSource.Initialize(throwOnEventWriteErrors: true));

            StringAssert.Contains(ex.Message, "Initialize()");
            StringAssert.Contains(ex.Message, "before the first access");
        }

        [TestMethod]
        public void Initialize_CalledTwice_ThrowsInvalidOperationException()
        {
            // Arrange
            EtwEventSource.Initialize(throwOnEventWriteErrors: false);

            // Act & Assert
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                EtwEventSource.Initialize(throwOnEventWriteErrors: true));

            StringAssert.Contains(ex.Message, "already been initialized");
        }
    }
}
