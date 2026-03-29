// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Etw;
using System.Diagnostics.Tracing;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Etw
{
    [TestClass]
    public class EtwEventSourceConfigureTests
    {
        [TestMethod]
        public void Log_HasThrowOnEventWriteErrors()
        {
            Assert.IsTrue(EtwEventSource.Log.Settings.HasFlag(EventSourceSettings.ThrowOnEventWriteErrors));
        }

        [TestMethod]
        public void Log_ReturnsSameInstance()
        {
            var a = EtwEventSource.Log;
            var b = EtwEventSource.Log;

            Assert.AreSame(a, b);
        }
    }
}
