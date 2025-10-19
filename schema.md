[[_TOC_]]

# Generic properties

Common to all operations.

| Property          | Type           | Description                                   | Examples                             |
|-------------------|----------------|-----------------------------------------------|--------------------------------------|
| timestamp         | DateTime (UTC) | Timestamp when the operation started          | 2025-07-21T10:32:53.6482311Z         |
| id                | string         | Id of the trace line                          | ad8e60573927c2aa                     |
| operationId       | string         | Id of the operation                           | ca8b5315adf0385c4eced3b83ee20d5e     |
| operationParentId | string         | Id of the parent trace line                   | 54e5e17086a16c9c                     |
| conversationId    | string         | Id of the conversation                        | conv_5j66UpCpwteGg4YSxUnt7lPY        |
| taskId            | string         | Id of the task                                | ca8b5315adf0385c4eced3b83ee20d5e     |
| operationName     | string         | Name of the operation                         | invoke_agent, execute_tool           |
| success           | boolean        | Whether the traced operation was successful   | True, False                          |
| duration          | double         | duration in ms                                | 2197.8986                            |
| agentId           | string         | Kairo Id of the agent performing the operation | 30ed5699-b157-4e87-bb45-9b0cfb13b8e5 |
|                   |                |                                               |                                      |

# 🤖 Invoke Agent

| Property      | Type   | Description                        | Examples                                |
|---------------|--------|------------------------------------|-----------------------------------------|
| targetAgentId | string | Kairo Id of the agent being invoked | 30ed5699-b157-4e87-bb45-9b0cfb13b8e5    |
| agentName     | string | Name of the target agent           | GraphAgent, ScrumAssistant              |
| agentEndpoint | string | Endpoint of the target agent       | app-web-aj5i4udhgrqgm.azurewebsites.net |
| response      | string | Response from the agent call       | Here is the org structure: ...          |

# 🛠 Execute Tool

| Property                | Type   | Description                      | Examples                                            |
|-------------------------|--------|----------------------------------|-----------------------------------------------------|
| toolCallId              | string | Id of the tool call              | call_xmS8WyT3sBrSVbX1uedGw2CA                       |
| gen_ai.tool.name        | string | Name of the tool                 | get-current-user-email                              |
| gen_ai.tool.description | string | Description of the tool          | get current user email address                      |
| gen_ai.tool.arguments   | string | Arguments of the tool call       | {'name':'input',data:{'value':'x','type':'string'}} |
| gen_ai.event.content    | string | Response from the tool execution | {'value':'user@somedomain.com','type':'string'}     |
| gen_ai.tool.type        | string | Type of the tool utilized        | function; extension; datastore                      |
| error.type              | string | Class of error the tool call ended with | java.net.UnknownHostException                |
| error.message           | string | Message containing human-readable detail about an error | Unexpected input type: string|