// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Contracts.Details
{
    /// <summary>
    /// Constants for tool type identifiers used in observability tracing.
    /// </summary>
    public sealed class ToolType
    {
        /// <summary>
        /// Represents a function tool type.
        /// </summary>
        public static readonly string Function = "function";
        
        /// <summary>
        /// Represents an extension tool type.
        /// </summary>
        public static readonly string Extension = "extension";
        
        /// <summary>
        /// Represents a datastore tool type.
        /// </summary>
        public static readonly string Datastore = "datastore";
    }
}