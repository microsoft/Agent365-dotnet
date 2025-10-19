// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OpenAI.Chat;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using OpenAIMultiturn;

namespace OpenAIMultiturn.Functions;

/// <summary>
/// Terms and Conditions management functions for OpenAI function calling
/// </summary>
public static class TermsAndConditionsAcceptedFunctions
{
    /// <summary>
    /// OpenAI Function Definition for rejecting terms and conditions
    /// </summary>
    public static ChatTool RejectTermsAndConditionsFunction => ChatTool.CreateFunctionTool(
        functionName: "reject_terms_and_conditions",
        functionDescription: "Reject the terms and conditions on behalf of the user. Use when the user indicates they do not accept the terms and conditions.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """)
    );

    /// <summary>
    /// Execute the reject terms and conditions function
    /// </summary>
    public static Task<string> ExecuteRejectTermsAndConditionsAsync()
    {
        MyAgent.TermsAndConditionsAccepted = false;
        return Task.FromResult("Terms and conditions rejected. You can accept later to proceed.");
    }
}