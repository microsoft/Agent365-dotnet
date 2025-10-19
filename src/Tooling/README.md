# Microsoft Kairo Tooling SDK for .NET

## 🚀 Features

- **Listing tool servers**: Ability to list the tool servers that are available to the agent.
- **Add tool server to orchestrator**: An easy way to add all selected tool servers directly to the most popular orchestrators.

## 🚀 Quick Start

TODO: This is currently only for Semantic Kernel. We need to add a sample for List tool servers and then provide a link to this Semantic Kernel-specific code.

1. Register required types:
2. Create a class with a constructor such as this:
    ```csharp
    public ToolingAgent(Kernel kernel, IServiceProvider service, IMcpToolRegistrationService mcpToolRegistrationService, UserAuthorization userAuthorization, ITurnContext turnContext)
    ```
3. Call `IMcpToolRegistrationService.AddToolServersToAgent(...)`:
    ```csharp
    // To use agentic authentation:
    mcpToolRegistrationService.AddToolServersToAgent(kernel, agentUserId, environmentId, userAuthorization, turnContext);
    
    // To use an auth token you specify yourself:
    mcpToolRegistrationService.AddToolServersToAgent(kernel, agentUserId, environmentId, userAuthorization, turnContext, authToken);
    ```
4. Define the agent:
    ```csharp
        // Define the agent
        this._agent =
            new()
            {
                Instructions = AgentInstructions(),
                Name = AgentName,
                Kernel = this._kernel,
                Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
                {
    #pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true }),
    #pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    ResponseFormat = "json_object", 
                }),
            };
    ```
    
> [!IMPORTANT]
> This line is important, make sure not to omit it: `FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true }),`



## 🛠️ Sample Applications

- **Semantic Kernel Multiturn**: [`/dotnet/samples/semantic-kernel-multiturn/`](./dotnet/samples/semantic-kernel-multiturn/) - C# Semantic Kernel sample.
