# OpenAI Analyzer Demo Application# Semantic Kernel multi-turn with Agent 365 sample



This demo application is a modified copy of the `openai-multiturn` sample that **deliberately contains analyzer violations** for testing OpenAI governance rules.This is a sample of an Agent 365 agent that is hosted on an Asp.net core web service. This Agent is configured to accept a request and will attempt to use configured Agent 365 to respond. This agent will handle multiple "turns" to get the required information from the user.



## ⚠️ Important NoticeThe sample is a modified verison of the [semantic-kernel-multiturn sample for Microsoft 365 Agents SDK](https://github.com/microsoft/Agents/tree/main/samples/dotnet/semantic-kernel-multiturn).



**DO NOT use the patterns shown in this application in production code.** All code in this demo contains intentional analyzer violations for educational and testing purposes.This Agent Sample is intended to introduce you the basics of integrating Agent 365 and Semantic Kernel with the Microsoft 365 Agents SDK in order to build powerful Agents. It can also be used as a the base for a custom Agent that you choose to develop.



## Purpose***Note:*** This sample requires JSON output from the model which works best from newer versions of the model such as gpt-4o-mini.



This application demonstrates violations of OpenAI governance analyzer rules:## Prerequisites



- **A365OAI0001**: Prohibit direct `ChatClient` registration/access- [.Net](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) version 8.0

- **A365OAI0002**: Prohibit direct `OpenAIClient` registration/access  - [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows)

- **A365OAI0003**: Prohibit direct `IOpenAIFunctionManager` registration/access- [Microsoft 365 Agents Toolkit](https://github.com/OfficeDev/microsoft-365-agents-toolkit)

- **A365OAI0004**: Prohibit direct access to tenant_id/worker_id claims and headers

- You will need an Azure OpenAI or OpenAI resource using `gpt-40-mini`

## What Makes This a "Bad" Example 

- Configure OpenAI in appsettings

### 1. Direct Client Registration (Program.cs)

```csharp  ```json

// ❌ VIOLATION A365OAI0002: Direct OpenAIClient registration  "AIServices": {

builder.Services.AddSingleton<OpenAIClient>(provider => ...);    "AzureOpenAI": {

      "DeploymentName": "", // This is the Deployment (as opposed to model) Name of the Azure OpenAI model

// ❌ VIOLATION A365OAI0001: Direct ChatClient registration      "Endpoint": "", // This is the Endpoint of the Azure OpenAI model deployment

builder.Services.AddSingleton<ChatClient>(provider => ...);      "ApiKey": "" // This is the API Key of the Azure OpenAI model deployment

```    },

    "OpenAI": {

### 2. Local Services Instead of Runtime.OpenAI      "ModelId": "", // This is the Model ID of the OpenAI model

```csharp      "ApiKey": "" // This is the API Key of the OpenAI model

// ❌ VIOLATION: Using local services instead of Runtime.OpenAI    },

using OpenAIMultiturn.Services;      "UseAzureOpenAI": true // This is a flag to determine whether to use the Azure OpenAI model or the OpenAI model  

```  }

  ```

### 3. Direct Tenant/Worker Access (Routes)

```csharp## QuickestStart using Agent Toolkit

// ❌ VIOLATION A365OAI0004: Direct access patterns1. If you haven't done so already, install the Agents Playground

var tenantId = request.HttpContext.User.FindFirst("tenant_id")?.Value; 

var workerId = request.Headers["X-Worker-Id"].FirstOrDefault();   ```

var openAIClient = request.HttpContext.RequestServices.GetRequiredService<OpenAIClient>();   winget install agentsplayground

```   ```

1. Start the Agent in VS or VS Code in debug

## Comparison with Correct Implementation1. Start Agents Playground.  At a command prompt: `agentsplayground`

   - The tool will open a web browser showing the Microsoft 365 Agents Playgroun, ready to send messages to your agent. 

The **correct** implementation is in `C:\Users\sellak\source\repos\Agent365\dotnet\samples\openai-multiturn`, which:1. Interact with the Agent via the browser



✅ Uses `Microsoft.Agents.A365.Runtime.OpenAI` namespace  ## QuickStart using WebChat or Teams

✅ Uses `IOpenAIClientProvider` with proper configuration  

✅ Follows governance patterns  - Overview of running and testing an Agent

✅ No direct client access    - Provision an Azure Bot in your Azure Subscription

  - Configure your Agent settings to use to desired authentication type

## How to Use This Demo  - Running an instance of the Agent app (either locally or deployed to Azure)

  - Test in a client

### Building

```bash1. Create an Azure Bot with one of these authentication types

# Navigate to demo directory   - [SingleTenant, Client Secret](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-single-secret)

cd C:\Users\sellak\source\repos\Agent365\dotnet\sdk\DevTools\Analyzer\OpenAI\Tests\AnalyzerDemoApp   - [SingleTenant, Federated Credentials](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-federated-credentials) 

   - [User Assigned Managed Identity](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/azure-bot-create-managed-identity)

# Build (should show analyzer violations when analyzer is active)    

dotnet build   > Be sure to follow the **Next Steps** at the end of these docs to configure your agent settings.

```

   > **IMPORTANT:** If you want to run your agent locally via devtunnels, the only support auth type is ClientSecrets and Certificates

### Expected Analyzer Output (When Active)

```1. Running the Agent

Program.cs(24,1): error A365OAI0002: Direct OpenAIClient registration detected   1. Running the Agent locally

Program.cs(31,1): error A365OAI0001: Direct ChatClient registration detected      - Requires a tunneling tool to allow for local development and debugging should you wish to do local development whilst connected to a external client such as Microsoft Teams.

Program.cs(67,5): error A365OAI0004: Direct access to tenant_id claim detected      - **For ClientSecret or Certificate authentication types only.**  Federated Credentials and Managed Identity will not work via a tunnel to a local agent and must be deployed to an App Service or container.

Program.cs(70,5): error A365OAI0004: Direct access to worker_id header detected      

Program.cs(76,5): error A365OAI0002: Direct OpenAIClient access detected      1. Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

Program.cs(79,5): error A365OAI0001: Direct ChatClient access detected

```         ```bash

         devtunnel host -p 3978 --allow-anonymous

## Files Structure         ```



- **Program.cs**: Contains startup violations (direct registrations, tenant access)      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `{tunnel-url}/api/messages`

- **MyAgent.cs**: Uses local services (violation of governance)  

- **Services/**: Local service implementations (should use Runtime.OpenAI instead)      1. Start the Agent in Visual Studio

- **AnalyzerDemoApp.csproj**: Project with analyzer package reference (when available)

   1. Deploy Agent code to Azure

## Enabling Analyzer      1. VS Publish works well for this.  But any tools used to deploy a web application will also work.

      1. On the Azure Bot, select **Settings**, then **Configuration**, and update the **Messaging endpoint** to `https://{{appServiceDomain}}/api/messages`

Uncomment in `AnalyzerDemoApp.csproj`:

```xml## Testing this agent with WebChat

<PackageReference Include="Microsoft.Agents.A365.DevTools.Analyzer.OpenAI" Version="1.0.0">

  <PrivateAssets>all</PrivateAssets>   1. Select **Test in WebChat** on the Azure Bot

</PackageReference>

```## Testing this Agent in Teams or M365



## Related Files1. Update the manifest.json

   - Edit the `manifest.json` contained in the `/appManifest` folder

- **Correct Implementation**: `../../samples/openai-multiturn/`      - Replace with your AppId (that was created above) *everywhere* you see the place holder string `<<AAD_APP_CLIENT_ID>>`

- **Runtime Services**: `../../sdk/Runtime/OpenAI/`     - Replace `<<BOT_DOMAIN>>` with your Agent url.  For example, the tunnel host name.

- **Analyzer Rules**: `../../sdk/DevTools/Analyzer/OpenAI/`   - Zip up the contents of the `/appManifest` folder to create a `manifest.zip`
     - `manifest.json`
     - `outline.png`
     - `color.png`

1. Your Azure Bot should have the **Microsoft Teams** channel added under **Channels**.

1. Navigate to the Microsoft Admin Portal (MAC). Under **Settings** and **Integrated Apps,** select **Upload Custom App**.

1. Select the `manifest.zip` created in the previous step. 

1. After a short period of time, the agent shows up in Microsoft Teams and Microsoft 365 Copilot.

## Enabling JWT token validation
1. By default, the AspNet token validation is disabled in order to support local debugging.
1. Enable by updating appsettings
   ```json
   "TokenValidation": {
     "Enabled": true,
     "Audiences": [
       "{{ClientId}}" // this is the Client ID used for the Azure Bot
     ],
     "TenantId": "{{TenantId}}"
   },
   ```

## Troubleshooting - Known/Common Issues

### Missing OpenAI key in appSettings.json

#### Error when project is run through Visual Studio

When the project is run through Visual Studio, an error is seen:

  System.ArgumentException: 'The value cannot be an empty string or composed entirely of whitespace. (Parameter 'endpoint')'

The exception has call stack:
```
>	System.Private.CoreLib.dll!System.ArgumentException.ThrowNullOrWhiteSpaceException(string argument, string paramName) Line 113	C#
 	System.Private.CoreLib.dll!System.ArgumentException.ThrowIfNullOrWhiteSpace(string argument, string paramName) Line 98	C#
 	Microsoft.SemanticKernel.Connectors.OpenAI.dll!Microsoft.SemanticKernel.Verify.NotNullOrWhiteSpace(string str, string paramName) Line 38	C#
 	Microsoft.SemanticKernel.Connectors.AzureOpenAI.dll!Microsoft.SemanticKernel.AzureOpenAIServiceCollectionExtensions.AddAzureOpenAIChatCompletion(Microsoft.Extensions.DependencyInjection.IServiceCollection services, string deploymentName, string endpoint, string apiKey, string serviceId, string modelId, string apiVersion, System.Net.Http.HttpClient httpClient) Line 30	C#
 	SemanticKernelMultiturn.dll!Program.<Main>$(string[] args) Line 33	C#
```

#### Error when project is run through command line
When the project is run through command line:
```
> dotnet run SemanticKernelMultiturn.csproj
```
An error is seen:
```
C:\Agent365\dotnet\samples\semantic-kernel-multiturn\MyAgent.cs(145,48): warning CS8602: Dereference of a possibly null reference.
Unhandled exception. System.ArgumentException: The value cannot be an empty string or composed entirely of whitespace. (Parameter 'endpoint')
   at System.ArgumentException.ThrowNullOrWhiteSpaceException(String argument, String paramName)
   at System.ArgumentException.ThrowIfNullOrWhiteSpace(String argument, String paramName)
   at Microsoft.SemanticKernel.AzureOpenAIServiceCollectionExtensions.AddAzureOpenAIChatCompletion(IServiceCollection services, String deploymentName, String endpoint, String apiKey, String serviceId, String modelId, String apiVersion, HttpClient httpClient)
   at Program.<Main>$(String[] args) in C:\Agent365\dotnet\samples\semantic-kernel-multiturn\Program.cs:line 33
```


#### Solution
Follow the instructions in `appSettings.json` for how to set the correct OpenAI or Azure OpenAI key.



## Further reading
To learn more about building Agents, see [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).
