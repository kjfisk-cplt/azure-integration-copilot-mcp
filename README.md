# azure-integration-copilot-mcp

> **MCP-server i C# / .NET 9 som kopplar GitHub Copilot Chat mot dina Azure-integrationstjänster.**

Ställ naturliga frågor i Copilot Chat och få svar direkt från dina Azure-resurser:

- *"Hur många fakturor har vi skickat idag?"*
- *"Finns det några fel i mina integrationer idag?"*
- *"Visa körhistorik för min Logic App X"*
- *"Hur många dead-letter meddelanden finns i kön Y?"*

---

## Innehåll

- [Vad gör det här projektet?](#vad-gör-det-här-projektet)
- [Förutsättningar](#förutsättningar)
- [Konfiguration](#konfiguration)
- [Köra lokalt som MCP-server](#köra-lokalt-som-mcp-server)
- [Konfigurera i VS Code / Codespaces](#konfigurera-i-vs-code--codespaces)
- [Exempelfrågor i Copilot Chat](#exempelfrågor-i-copilot-chat)
- [Projektstruktur](#projektstruktur)
- [Länkar](#länkar)

---

## Vad gör det här projektet?

Det här är en **MCP-server (Model Context Protocol)** byggd i C# / .NET 9 som exponerar ett antal verktyg (*tools*) mot GitHub Copilot Chat. När du ställer en fråga i Copilot Chat som rör dina Azure-resurser anropar Copilot automatiskt rätt verktyg och returnerar ett svar baserat på riktig data.

### Azure-tjänster som stöds

| Verktyg | Azure-tjänst |
|---|---|
| `LogAnalyticsTool` | Azure Monitor Log Analytics (KQL-frågor, anpassade tabeller `_CL`) |
| `LogicAppsTool` | Logic Apps Standard (lista arbetsflöden, körhistorik) |
| `ServiceBusTool` | Azure Service Bus (köstatus, dead-letter-meddelanden) |
| `ApimTool` | Azure API Management (lista API:er, hälsostatus) |
| `FunctionsTool` | Azure Functions (lista appar, hämta fel från Log Analytics) |

---

## Förutsättningar

- **Azure-prenumeration** med tillgång till de resurser du vill fråga mot
- **Behörigheter**: Minst `Reader`-roll på prenumeration/resursgrupper, samt `Log Analytics Reader` på Log Analytics-workspace
- **.NET 9 SDK** – [ladda ner här](https://dotnet.microsoft.com/download/dotnet/9)
- **Azure CLI** – för att logga in med `az login` (används av `DefaultAzureCredential`)
- **GitHub Copilot**-licens med stöd för MCP

---

## Konfiguration

Redigera `src/AzureIntegrationMcp/appsettings.json` och fyll i dina Azure-resurser:

```json
{
  "Azure": {
    "SubscriptionId": "<ditt-subscription-id>",
    "TenantId": "<ditt-tenant-id>",
    "LogAnalytics": {
      "WorkspaceId": "<workspace-id-guid>",
      "InvoiceTable": "IntegrationLogs_CL",
      "InvoiceOperationField": "OperationType_s",
      "InvoiceOperationValue": "Invoice"
    },
    "ServiceBus": {
      "FullyQualifiedNamespace": "<namespace>.servicebus.windows.net"
    },
    "Apim": {
      "ResourceGroup": "<resursgrupp>",
      "ServiceName": "<apim-tjänstnamn>"
    }
  }
}
```

> ⚠️ **Commit aldrig `appsettings.*.json`** med riktiga värden. Filen `appsettings.json` med tomma värden är OK att committa (och är inkluderad). Lokala åsidosättningar görs i t.ex. `appsettings.Development.json` som ignoreras av `.gitignore`.

Du kan också ange värden via miljövariabler med dubbla understreck som separator:
```bash
export Azure__LogAnalytics__WorkspaceId="<guid>"
```

---

## Köra lokalt som MCP-server

```bash
# 1. Logga in på Azure
az login

# 2. Återställ paket
dotnet restore ./src/AzureIntegrationMcp/AzureIntegrationMcp.csproj

# 3. Bygg projektet
dotnet build ./src/AzureIntegrationMcp/AzureIntegrationMcp.csproj

# 4. Kör MCP-servern (stdio-transport)
dotnet run --project ./src/AzureIntegrationMcp/AzureIntegrationMcp.csproj
```

MCP-servern kommunicerar via `stdio` och startas automatiskt av VS Code / Copilot Chat när den är konfigurerad (se nedan).

---

## Konfigurera i VS Code / Codespaces

Skapa filen `.vscode/mcp.json` i ditt workspace:

```json
{
  "servers": {
    "azure-integration": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/AzureIntegrationMcp/AzureIntegrationMcp.csproj",
        "--no-build"
      ]
    }
  }
}
```

> 💡 Bygg projektet en gång med `dotnet build` innan du använder `--no-build` för snabbare start.

---

## Exempelfrågor i Copilot Chat

När MCP-servern är konfigurerad kan du ställa frågor som:

```
Hur många fakturor har vi skickat idag?
```
```
Finns det några fel i mina integrationer idag?
```
```
Visa körhistorik för min Logic App 'invoice-processor'
```
```
Hur många dead-letter meddelanden finns i kön 'invoices'?
```
```
Lista alla mina Function Apps i resursgruppen 'integration-rg'
```
```
Kör den här KQL-frågan mot min Log Analytics: IntegrationLogs_CL | take 10
```

---

## Projektstruktur

```
azure-integration-copilot-mcp/
├── .devcontainer/
│   └── devcontainer.json          # Codespaces-konfiguration
├── .github/
│   └── copilot-instructions.md   # Instruktioner för Copilot i detta repo
├── src/
│   └── AzureIntegrationMcp/
│       ├── AzureIntegrationMcp.csproj
│       ├── Program.cs             # MCP-serverinit, DI-registrering
│       ├── Tools/
│       │   ├── LogAnalyticsTool.cs
│       │   ├── LogicAppsTool.cs
│       │   ├── ServiceBusTool.cs
│       │   ├── ApimTool.cs
│       │   └── FunctionsTool.cs
│       └── appsettings.json
├── .gitignore
└── README.md
```

---

## Länkar

- [MCP SDK for .NET (GitHub)](https://github.com/modelcontextprotocol/csharp-sdk)
- [GitHub Copilot MCP-dokumentation](https://docs.github.com/en/copilot/using-github-copilot/using-extensions/using-model-context-protocol-with-copilot)
- [Azure.Monitor.Query (NuGet)](https://www.nuget.org/packages/Azure.Monitor.Query)
- [DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Model Context Protocol (spec)](https://modelcontextprotocol.io)
