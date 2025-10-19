// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using OpenAI.Chat;

namespace OpenAIMultiturn.Functions;

public static class WeatherFunctions
{
    public static ChatTool GetWeatherFunction => ChatTool.CreateFunctionTool(
        functionName: "get_weather",
        functionDescription: "Get current weather information for a specified location",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "location": {
                    "type": "string",
                    "description": "The city and state, e.g. San Francisco, CA"
                },
                "unit": {
                    "type": "string",
                    "enum": ["celsius", "fahrenheit"],
                    "description": "The temperature unit to use"
                }
            },
            "required": ["location"]
        }
        """)
    );

    public static ChatTool GetForecastFunction => ChatTool.CreateFunctionTool(
        functionName: "get_forecast",
        functionDescription: "Get weather forecast for the next 3 days for a specified location",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "location": {
                    "type": "string",
                    "description": "The city and state, e.g. San Francisco, CA"
                },
                "days": {
                    "type": "number",
                    "description": "Number of days for forecast (1-7)",
                    "minimum": 1,
                    "maximum": 7
                }
            },
            "required": ["location"]
        }
        """)
    );
}