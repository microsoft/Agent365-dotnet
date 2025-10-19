using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents a named data value for observability tracing.
    /// </summary>
    public sealed class NamedDataValue : IEquatable<NamedDataValue>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NamedDataValue"/> class.
        /// </summary>
        /// <param name="name">Identifier describing the value.</param>
        /// <param name="data">Telemetry payload associated with the name.</param>
        public NamedDataValue(string name, DataValue data)
        {
            Name = name;
            Data = data;
        }

        /// <summary>
        /// Gets the descriptive name for the data value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; }

        /// <summary>
        /// Gets the captured data value.
        /// </summary>
        [JsonPropertyName("data")]
        public DataValue Data { get; }

        /// <summary>
        /// Deconstructs this instance into name and value components.
        /// </summary>
        /// <param name="name">Receives the descriptive name.</param>
        /// <param name="data">Receives the associated data value.</param>
        public void Deconstruct(out string name, out DataValue data)
        {
            name = Name;
            data = Data;
        }

        /// <inheritdoc/>
        public bool Equals(NamedDataValue? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                   EqualityComparer<DataValue>.Default.Equals(Data, other.Data);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as NamedDataValue);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0);
                hash = (hash * 31) + EqualityComparer<DataValue>.Default.GetHashCode(Data);
                return hash;
            }
        }
    }
}