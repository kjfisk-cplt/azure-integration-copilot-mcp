# Copilot Instructions – Azure Integration MCP Server

This is a **C# .NET MCP (Model Context Protocol) server** that connects GitHub Copilot Chat to Azure integration services.

## Purpose
Enable natural-language queries against Azure resources used in integration scenarios, such as:
- "Hur många fakturor har vi skickat idag?"
- "Finns det några fel i mina integrationer idag?"
- "Visa körhistorik för min Logic App X"
- "Hur många dead-letter meddelanden finns i kön Y?"

## Architecture
- The project is located in `src/AzureIntegrationMcp/`
- MCP tools are in `src/AzureIntegrationMcp/Tools/`
- The server uses `stdio` transport (standard for MCP)
- Authentication uses `Azure.Identity.DefaultAzureCredential` – never hardcode credentials

## Coding Conventions
- All MCP tools must be decorated with `[McpServerTool]` and `[Description]` attributes
- Descriptions should be clear and descriptive (Swedish or English) so Copilot knows when to invoke them
- KQL queries target Log Analytics custom tables ending in `_CL`
- Return formatted Markdown tables when returning tabular data
- Use `DefaultAzureCredential` for all Azure SDK clients – never use connection strings with secrets or API keys
- Configuration is read from `appsettings.json` and environment variables via `IConfiguration`

## Azure Services Covered
| Tool file              | Azure service                        |
|------------------------|--------------------------------------|
| `LogAnalyticsTool.cs`  | Azure Monitor Log Analytics (KQL)    |
| `LogicAppsTool.cs`     | Logic Apps Standard                  |
| `ServiceBusTool.cs`    | Azure Service Bus                    |
| `ApimTool.cs`          | Azure API Management                 |
| `FunctionsTool.cs`     | Azure Functions + Log Analytics      |

## Useful Links
- [MCP SDK for .NET](https://github.com/modelcontextprotocol/csharp-sdk)
- [GitHub Copilot MCP docs](https://docs.github.com/en/copilot/using-github-copilot/using-extensions/using-model-context-protocol-with-copilot)
- [Azure.Monitor.Query docs](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.query-readme)
- [DefaultAzureCredential docs](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
