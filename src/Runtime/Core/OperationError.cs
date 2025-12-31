// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Runtime;

/// <summary>
/// Encapsulates an error from an operation.
/// </summary>
public sealed class OperationError
{
    /// <summary>
    /// Gets the exception associated with the error.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the message associated with the error.
    /// </summary>
    public string Message => Exception.Message;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationError"/> class.
    /// </summary>
    /// <param name="exception">The exception associated with the error.</param>
    public OperationError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }

    /// <summary>
    /// Returns a string representation of the error.
    /// </summary>
    /// <returns>A string representation of the error.</returns>
    public override string ToString() => Exception.ToString();
}
