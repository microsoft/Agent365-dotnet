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
        public void Configure_WithTrue_BeforeLogAccess_CreatesInstanceWithThrowOnErrors()
        {
            // Arrange & Act
            EtwEventSource.Configure(throwOnEventWriteErrors: true);
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsTrue(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Configure_WithFalse_BeforeLogAccess_CreatesInstanceWithoutThrowOnErrors()
        {
            // Arrange & Act
            EtwEventSource.Configure(throwOnEventWriteErrors: false);
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsFalse(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Log_WithoutConfigure_CreatesInstanceWithoutThrowOnErrors()
        {
            // Act
            var log = EtwEventSource.Log;

            // Assert
            Assert.IsFalse(log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Configure_AfterLogAccess_ThrowsInvalidOperationException()
        {
            // Arrange - force singleton creation
            _ = EtwEventSource.Log;

            // Act & Assert
            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                EtwEventSource.Configure(throwOnEventWriteErrors: true));

            StringAssert.Contains(ex.Message, "Configure()");
            StringAssert.Contains(ex.Message, "before the first access");
        }
    }
}
