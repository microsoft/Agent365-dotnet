# MCP URL Examples for Agent Framework

This file contains examples of MCP (Model Context Protocol) server URLs and configurations that can be used with the Agent Framework integration.

## Example MCP Server URLs

### Development/Local Servers

```
http://localhost:3000/mcp
http://localhost:8080/api/mcp
```

### Production Servers

```
https://api.example.com/mcp
https://tools.mycompany.com/mcp/v1
```

### Azure-hosted MCP Servers

```
https://myapp.azurewebsites.net/mcp
https://myfunction.azurewebsites.net/api/mcp
```

## Configuration Examples

### Basic Configuration

```json
{
  "mcpServers": [
    {
      "name": "ExampleServer",
      "url": "https://api.example.com/mcp",
      "authType": "bearer",
      "authToken": "your-token-here"
    }
  ]
}
```

### Advanced Configuration

```json
{
  "mcpServers": [
    {
      "name": "ProductionServer",
      "url": "https://tools.mycompany.com/mcp/v1",
      "authType": "oauth",
      "scopes": ["tools.read", "tools.execute"],
      "timeout": 30000,
      "retryAttempts": 3
    }
  ]
}
```

## Notes

- Ensure MCP servers are accessible from your application environment
- Use HTTPS in production environments
- Configure appropriate authentication for your MCP servers
- Test connectivity before deploying to production