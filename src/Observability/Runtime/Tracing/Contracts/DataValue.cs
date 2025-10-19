using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Describes the kind of telemetry value captured for data payloads.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValueType
    {
        /// <summary>
        /// Represents an empty value.
        /// </summary>
        Null,
        /// <summary>
        /// Represents an integral numeric value.
        /// </summary>
        Integer,
        /// <summary>
        /// Represents a floating-point numeric value.
        /// </summary>
        Float,
        /// <summary>
        /// Represents a boolean value.
        /// </summary>
        Boolean,
        /// <summary>
        /// Represents a string value.
        /// </summary>
        String,
        /// <summary>
        /// Represents an enumerable collection of values.
        /// </summary>
        Array,
        /// <summary>
        /// Represents a structured object value.
        /// </summary>
        Object
    }

    /// <summary>
    /// Encapsulates a telemetry data value along with its inferred type.
    /// </summary>
    public sealed class DataValue : IEquatable<DataValue>
    {
        /// <summary>
        /// Gets the captured value.
        /// </summary>
        [JsonPropertyName("value")]
        public object? Value { get; }

        /// <summary>
        /// Gets the classification for the captured value.
        /// </summary>
        [JsonPropertyName("type")]
        public ValueType Type { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataValue"/> class with an explicit type.
        /// </summary>
        /// <param name="value">The value to capture.</param>
        /// <param name="type">The type classification for the value.</param>
        public DataValue(object? value, ValueType type)
        {
            Value = value;
            Type = type;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataValue"/> class inferring the type from the supplied value.
        /// </summary>
        /// <param name="value">The value to capture.</param>
        public DataValue(object? value)
            : this(value, MapToValueType(value))
        {
        }

        /// <summary>
        /// Deconstructs this instance into its value and inferred type.
        /// </summary>
        /// <param name="value">Receives the stored value.</param>
        /// <param name="type">Receives the stored type.</param>
        public void Deconstruct(out object? value, out ValueType type)
        {
            value = Value;
            type = Type;
        }

        /// <inheritdoc/>
        public bool Equals(DataValue? other)
        {
            if (other is null)
            {
                return false;
            }

            return EqualityComparer<object?>.Default.Equals(Value, other.Value) && Type == other.Type;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as DataValue);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + EqualityComparer<object?>.Default.GetHashCode(Value);
                hash = (hash * 31) + Type.GetHashCode();
                return hash;
            }
        }

        private static ValueType MapToValueType(object? value)
        {
            if (value == null)
            {
                return ValueType.Null;
            }

            var type = value.GetType();

            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            {
                return ValueType.Integer;
            }

            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                return ValueType.Float;
            }

            if (type == typeof(bool))
            {
                return ValueType.Boolean;
            }

            if (type == typeof(string) || type == typeof(char))
            {
                return ValueType.String;
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                return ValueType.Array;
            }

            return ValueType.Object;
        }
    }
}
