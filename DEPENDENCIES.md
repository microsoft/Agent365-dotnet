# Microsoft Agent 365 SDK .NET Package Dependencies

This diagram shows the internal dependencies between Microsoft Agent 365 SDK .NET packages.

```mermaid
graph LR
    Notifications["Microsoft.Agents.A365.Notifications"]
    Runtime["Microsoft.Agents.A365.Runtime"]
    Tooling["Microsoft.Agents.A365.Tooling"]
    ToolingExtensionsAgentFramework["Microsoft.Agents.A365.Tooling.Extensions.AgentFramework"]
    ToolingExtensionsAzureAIFoundry["Microsoft.Agents.A365.Tooling.Extensions.AzureAIFoundry"]
    ToolingExtensionsSemanticKernel["Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel"]

    Tooling --> Runtime
    ToolingExtensionsAgentFramework --> Tooling
    ToolingExtensionsAzureAIFoundry --> Tooling
    ToolingExtensionsSemanticKernel --> Tooling

    classDef Notifications fill:#ffcdd2,stroke:#c62828,color:#280505
    classDef Runtime fill:#bbdefb,stroke:#1565c0,color:#0d1a26
    classDef Tooling fill:#ffe0b2,stroke:#e65100,color:#331a00
    classDef ToolingExtensions fill:#fff3e0,stroke:#fb8c00,color:#4d2600

    class Notifications Notifications
    class Runtime Runtime
    class Tooling Tooling
    class ToolingExtensionsAgentFramework,ToolingExtensionsAzureAIFoundry,ToolingExtensionsSemanticKernel ToolingExtensions
```

## Package Types

- **Notifications** (Red): Notification and messaging extensions
- **Runtime** (Blue): Core runtime components
- **Tooling** (Orange): Agent tooling SDK core
- **Tooling Extensions** (Light Orange): Framework-specific tooling integrations
