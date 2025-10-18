// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OpenAI.Chat;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using OpenAIMultiturn;

namespace OpenAIMultiturn.Functions;

/// <summary>
/// Terms and Conditions management functions for OpenAI function calling when not accepted
/// </summary>
public static class TermsAndConditionsNotAcceptedFunctions
{
    /// <summary>
    /// OpenAI Function Definition for accepting terms and conditions
    /// </summary>
    public static ChatTool AcceptTermsAndConditionsFunction => ChatTool.CreateFunctionTool(
        functionName: "accept_terms_and_conditions",
        functionDescription: "Accept the terms and conditions on behalf of the user. Use when the user states they accept the terms and conditions.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """)
    );

    /// <summary>
    /// OpenAI Function Definition for informing user about terms and conditions requirement
    /// </summary>
    public static ChatTool TermsAndConditionsNotAcceptedFunction => ChatTool.CreateFunctionTool(
        functionName: "terms_and_conditions_not_accepted",
        functionDescription: "Inform the user that they must accept the terms and conditions to proceed. Use when the user tries to perform any action before accepting the terms and conditions.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """)
    );

    /// <summary>
    /// Execute the accept terms and conditions function
    /// </summary>
    public static Task<string> ExecuteAcceptTermsAndConditionsAsync()
    {
        MyAgent.TermsAndConditionsAccepted = true;
        return Task.FromResult("Terms and conditions accepted. Thank you.");
    }

    /// <summary>
    /// Execute the terms and conditions not accepted notification function
    /// </summary>
    public static Task<string> ExecuteTermsAndConditionsNotAcceptedAsync()
    {
        return Task.FromResult("You must accept the terms and conditions to proceed.");
    }
}