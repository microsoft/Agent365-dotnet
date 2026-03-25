# A365 Input/Output Message Schema Examples

> **A365 Schema Version:** 1.0.0 | **Based on OTel Semconv:** v1.40.0

---

## Input Message Examples (`gen_ai.input.messages`)

### Simple — Text Chat

```json
[
  {
    "role": "system",
    "parts": [
      { "type": "text", "content": "You are a helpful assistant." }
    ]
  },
  {
    "role": "user",
    "parts": [
      { "type": "text", "content": "What meetings do I have today?" }
    ]
  }
]
```

### Complex — All Input Part Types

A multi-turn conversation demonstrating every input part type: text, tool_call (from prior assistant turn), tool_call_response, server_tool_call, server_tool_call_response, blob, file, uri, reasoning, and a custom generic part.

```json
[
  {
    "role": "system",
    "parts": [
      { "type": "text", "content": "You are a project assistant with access to tools, files, and images." }
    ],
    "name": "OrchestratorAgent"
  },
  {
    "role": "user",
    "parts": [
      { "type": "text", "content": "Summarize the attached document, check the weather in Seattle, and describe the whiteboard photo." },
      {
        "type": "file",
        "modality": "image",
        "mime_type": "application/pdf",
        "file_id": "file-report-q1-2026"
      },
      {
        "type": "uri",
        "modality": "image",
        "mime_type": "image/png",
        "uri": "https://sharepoint.com/sites/team/whiteboard-photo.png"
      },
      {
        "type": "blob",
        "modality": "audio",
        "mime_type": "audio/wav",
        "content": "/9j/4AAQSkZJRg..."
      }
    ],
    "name": "UserJohnDoe"
  },
  {
    "role": "assistant",
    "parts": [
      { "type": "text", "content": "Let me look up the weather and analyze your files." },
      {
        "type": "tool_call",
        "id": "call_weather_001",
        "name": "get_weather",
        "arguments": { "city": "Seattle", "units": "fahrenheit" }
      },
      {
        "type": "reasoning",
        "content": "User attached a PDF, an image URI, and an audio blob. I should process the weather tool call first, then analyze the attachments."
      }
    ]
  },
  {
    "role": "tool",
    "parts": [
      {
        "type": "tool_call_response",
        "id": "call_weather_001",
        "response": { "temperature": 57, "condition": "rainy", "humidity": 82 }
      }
    ]
  },
  {
    "role": "assistant",
    "parts": [
      {
        "type": "server_tool_call",
        "id": "stc_code_001",
        "name": "code_interpreter",
        "server_tool_call": {
          "type": "code_interpreter",
          "code": "import pandas as pd\ndf = pd.read_csv('sales.csv')\nprint(df.describe())"
        }
      },
      {
        "type": "server_tool_call_response",
        "id": "stc_code_001",
        "server_tool_call_response": {
          "type": "code_interpreter",
          "output": "count: 1200, mean: 45000, max: 120000"
        }
      }
    ]
  },
  {
    "role": "user",
    "parts": [
      {
        "type": "microsoft.a365.adaptive_card",
        "card_id": "ac-approval-001",
        "action": "approved"
      }
    ],
    "name": "UserJohnDoe"
  }
]
```

**Part types demonstrated:**

| Part Type | Where Used | Description |
|---|---|---|
| `text` | system, user, assistant | Plain text messages |
| `tool_call` | assistant (prior turn) | Model requested a client-side tool |
| `tool_call_response` | tool | Client-side tool result sent back |
| `server_tool_call` | assistant (prior turn) | Server-side tool invocation |
| `server_tool_call_response` | assistant (prior turn) | Server-side tool result |
| `blob` | user | Inline binary data (base64) |
| `file` | user | Pre-uploaded file reference |
| `uri` | user | External file by URL |
| `reasoning` | assistant (prior turn) | Chain-of-thought from model |
| `microsoft.a365.adaptive_card` | user | Custom A365 GenericPart |

---

## Output Message Examples (`gen_ai.output.messages`)

### Simple — Text Response

```json
[
  {
    "role": "assistant",
    "finish_reason": "stop",
    "parts": [
      { "type": "text", "content": "You have 3 meetings today: standup at 9 AM, design review at 11 AM, and a 1:1 at 3 PM." }
    ]
  }
]
```

### Complex — All Output Part Types

A response demonstrating every output part type: reasoning, text, tool_call, server_tool_call with response, and a custom generic part. Also shows multiple finish reasons.

```json
[
  {
    "role": "assistant",
    "finish_reason": "stop",
    "parts": [
      {
        "type": "reasoning",
        "content": "The user asked for a document summary, weather, and whiteboard analysis. I have the weather data from the tool call. The code_interpreter analyzed the sales data. I should compile all results into a structured response."
      },
      {
        "type": "text",
        "content": "Here's your summary:\n\n**Weather:** Seattle is currently rainy at 57°F with 82% humidity.\n\n**Q1 Report:** Revenue averaged $45K per deal across 1,200 transactions, with a max deal of $120K.\n\n**Whiteboard:** The whiteboard shows a system architecture diagram with three microservices connected via an API gateway."
      },
      {
        "type": "server_tool_call",
        "id": "stc_search_001",
        "name": "web_search",
        "server_tool_call": {
          "type": "web_search",
          "query": "Seattle weather forecast March 2026"
        }
      },
      {
        "type": "server_tool_call_response",
        "id": "stc_search_001",
        "server_tool_call_response": {
          "type": "web_search",
          "results": [
            { "title": "Seattle Weather - March 2026", "url": "https://weather.com/seattle", "snippet": "Expect rain through the week..." }
          ]
        }
      },
      {
        "type": "blob",
        "modality": "image",
        "mime_type": "image/png",
        "content": "iVBORw0KGgoAAAANSUhEUg..."
      },
      {
        "type": "uri",
        "modality": "image",
        "mime_type": "image/png",
        "uri": "https://ai-generated.example.com/chart-q1-revenue.png"
      }
    ],
    "name": "ProjectAssistant"
  }
]
```

**Additional output examples by finish reason:**

#### Tool Call Request

```json
[
  {
    "role": "assistant",
    "finish_reason": "tool_call",
    "parts": [
      { "type": "text", "content": "Let me check that for you." },
      {
        "type": "tool_call",
        "id": "call_sales_q1",
        "name": "query_sales_data",
        "arguments": { "quarter": "Q1", "year": 2026 }
      },
      {
        "type": "tool_call",
        "id": "call_calendar",
        "name": "get_calendar",
        "arguments": { "date": "2026-03-25" }
      }
    ]
  }
]
```

#### Content Filter

```json
[
  {
    "role": "assistant",
    "finish_reason": "content_filter",
    "parts": [
      { "type": "text", "content": "I'm unable to provide that information due to content policy restrictions." }
    ]
  }
]
```

#### Token Limit

```json
[
  {
    "role": "assistant",
    "finish_reason": "length",
    "parts": [
      { "type": "text", "content": "Here is the beginning of the report: Q1 revenue increased by 15% year-over-year driven by strong adoption of..." }
    ]
  }
]
```

#### Error

```json
[
  {
    "role": "assistant",
    "finish_reason": "error",
    "parts": [
      { "type": "text", "content": "An error occurred while processing your request. Please try again." }
    ]
  }
]
```

**Part types demonstrated in output:**

| Part Type | Description |
|---|---|
| `text` | Direct text response from model |
| `reasoning` | Chain-of-thought / thinking content |
| `tool_call` | Model requesting client-side tool execution |
| `server_tool_call` | Server-side tool invocation details |
| `server_tool_call_response` | Server-side tool result |
| `blob` | Generated binary content (e.g., chart image) |
| `uri` | Reference to generated content |

**Finish reasons demonstrated:**

| Finish Reason | Meaning |
|---|---|
| `stop` | Model completed naturally |
| `tool_call` | Model is requesting tool execution |
| `content_filter` | Blocked by content policy |
| `length` | Token limit exceeded |
| `error` | Generation encountered an error |
