# Microsoft Agent 365 Settings SDK

This package provides functionality to manage Agent 365 settings templates and agent instance settings.

## Overview

The Settings SDK enables developers to:

- **Get or Set Agent Setting Templates by Agent Type**: Configure default settings templates that apply to specific types of agents.
- **Get or Set Agent Settings by Agent Instance**: Configure settings for individual agent instances.

## Installation

```bash
dotnet add package Microsoft.Agents.A365.Settings
```

## Usage

### Service Registration

Register the `AgentSettingsService` in your dependency injection container:

```csharp
services.AddHttpClient<IAgentSettingsService, AgentSettingsService>();
```

### Getting Settings Template by Agent Type

```csharp
public class MyAgentConfigService
{
    private readonly IAgentSettingsService _settingsService;

    public MyAgentConfigService(IAgentSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<AgentSettingsTemplate?> GetTemplateAsync(string agentType, string authToken)
    {
        return await _settingsService.GetSettingsTemplateByAgentTypeAsync(agentType, authToken);
    }
}
```

### Setting a Settings Template

```csharp
var template = new AgentSettingsTemplate
{
    AgentType = "custom-agent",
    Name = "Custom Agent Template",
    Properties = new List<AgentSettingProperty>
    {
        new AgentSettingProperty
        {
            Name = "maxRetries",
            Value = "3",
            Type = "integer",
            Required = true,
            Description = "Maximum number of retry attempts"
        }
    }
};

await _settingsService.SetSettingsTemplateByAgentTypeAsync("custom-agent", template, authToken);
```

### Getting Settings by Agent Instance

```csharp
var settings = await _settingsService.GetSettingsByAgentInstanceAsync(agentInstanceId, authToken);
if (settings != null)
{
    foreach (var property in settings.Properties)
    {
        Console.WriteLine($"{property.Name}: {property.Value}");
    }
}
```

### Setting Agent Instance Settings

```csharp
var settings = new AgentSettings
{
    AgentInstanceId = "my-agent-instance-id",
    AgentType = "custom-agent",
    Properties = new List<AgentSettingProperty>
    {
        new AgentSettingProperty
        {
            Name = "apiEndpoint",
            Value = "https://api.example.com",
            Type = "string"
        }
    }
};

await _settingsService.SetSettingsByAgentInstanceAsync(agentInstanceId, settings, authToken);
```

## Configuration

The service uses the following configuration options:

| Key | Description | Default |
|-----|-------------|---------|
| `MCP_PLATFORM_ENDPOINT` | Override the base URL for the Agent 365 platform | `https://agent365.svc.cloud.microsoft` |
| `MCP_PLATFORM_AUTHENTICATION_SCOPE` | The authentication scope for the platform | `ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default` |

## Models

### AgentSettingsTemplate

Represents a settings template for a specific agent type.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier of the template |
| `AgentType` | string | The agent type this template applies to |
| `Name` | string | Display name of the template |
| `Description` | string? | Optional description |
| `Version` | string | Template version (default: "1.0") |
| `Properties` | List\<AgentSettingProperty\> | Collection of setting properties |

### AgentSettings

Represents settings for a specific agent instance.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier of the settings |
| `AgentInstanceId` | string | The agent instance these settings belong to |
| `TemplateId` | string? | Optional reference to the template |
| `AgentType` | string | The agent type |
| `Properties` | List\<AgentSettingProperty\> | Collection of setting values |
| `CreatedAt` | DateTimeOffset | Creation timestamp |
| `ModifiedAt` | DateTimeOffset | Last modification timestamp |

### AgentSettingProperty

Represents a single setting property.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | string | Name of the setting |
| `Value` | string | Current value |
| `Type` | string | Value type (default: "string") |
| `Required` | bool | Whether the setting is required |
| `Description` | string? | Optional description |
| `DefaultValue` | string? | Optional default value |

## License

This project is licensed under the MIT License.
