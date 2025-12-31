// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

namespace Microsoft.Agents.A365.Runtime;

/// <summary>
/// Represents the result of an operation.
/// </summary>
public class OperationResult
{
    private static readonly OperationResult _success = new OperationResult { Succeeded = true };
    private List<OperationError>? _errors;

    /// <summary>
    /// Gets a flag indicating whether the operation succeeded.
    /// </summary>
    /// <value>True if the operation succeeded, otherwise false.</value>
    public bool Succeeded { get; protected init; }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> of <see cref="OperationError"/> instances indicating errors that occurred during the operation.
    /// </summary>
    /// <value>An <see cref="IEnumerable{T}"/> of <see cref="OperationError"/> instances.</value>
    public IEnumerable<OperationError> Errors => _errors ?? Enumerable.Empty<OperationError>();

    /// <summary>
    /// Returns an <see cref="OperationResult"/> indicating a successful operation.
    /// </summary>
    /// <returns>An <see cref="OperationResult"/> indicating a successful operation.</returns>
    public static OperationResult Success => _success;

    /// <summary>
    /// Creates an <see cref="OperationResult"/> indicating a failed operation, with a list of <paramref name="errors"/> if applicable.
    /// </summary>
    /// <param name="errors">An optional array of <see cref="OperationError"/> which caused the operation to fail.</param>
    /// <returns>An <see cref="OperationResult"/> indicating a failed operation, with a list of <paramref name="errors"/> if applicable.</returns>
    public static OperationResult Failed(params OperationError[] errors)
    {
        var result = new OperationResult { Succeeded = false };
        if (errors != null && errors.Length > 0)
        {
            result._errors = new List<OperationError>(errors);
        }
        return result;
    }

    /// <summary>
    /// Converts the value of the current <see cref="OperationResult"/> object to its equivalent string representation.
    /// </summary>
    /// <returns>A string representation of the current <see cref="OperationResult"/> object.</returns>
    /// <remarks>
    /// If the operation was successful the ToString() will return "Succeeded" otherwise it will return
    /// "Failed : " followed by a comma delimited list of error messages from its <see cref="Errors"/> collection, if any.
    /// </remarks>
    public override string ToString()
    {
        return Succeeded
            ? "Succeeded"
            : string.Format("{0} : {1}", "Failed", string.Join(", ", Errors.Select(x => x.Message)));
    }
}
