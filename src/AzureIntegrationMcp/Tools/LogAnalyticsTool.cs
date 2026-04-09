using System.ComponentModel;
using System.Text;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace AzureIntegrationMcp.Tools;

[McpServerToolType]
public class LogAnalyticsTool
{
    private readonly LogsQueryClient _client;
    private readonly IConfiguration _config;

    public LogAnalyticsTool(LogsQueryClient client, IConfiguration config)
    {
        _client = client;
        _config = config;
    }

    [McpServerTool]
    [Description("Kör en KQL-fråga mot Azure Monitor Log Analytics och returnerar resultatet som en Markdown-tabell. Ange workspace-ID och KQL-frågan.")]
    public async Task<string> QueryLogAnalytics(
        [Description("Log Analytics workspace-ID (GUID)")] string workspaceId,
        [Description("KQL-fråga att köra")] string kqlQuery)
    {
        var response = await _client.QueryWorkspaceAsync(workspaceId, kqlQuery, QueryTimeRange.All);
        return FormatTable(response.Value.Table);
    }

    [McpServerTool]
    [Description("Hämtar antal fakturor som skickats idag från Log Analytics. Returnerar en sammanfattning med antal och status. Svara på frågor som 'Hur många fakturor har vi skickat idag?'")]
    public async Task<string> GetInvoicesSentToday(
        [Description("Log Analytics workspace-ID (GUID). Om tomt används värdet från konfigurationen.")] string? workspaceId = null)
    {
        var wsId = workspaceId ?? _config["Azure:LogAnalytics:WorkspaceId"] ?? string.Empty;
        if (string.IsNullOrEmpty(wsId))
            return "❌ Workspace-ID saknas. Ange det som parameter eller konfigurera Azure:LogAnalytics:WorkspaceId i appsettings.json.";

        var table = _config["Azure:LogAnalytics:InvoiceTable"] ?? "IntegrationLogs_CL";
        var opField = _config["Azure:LogAnalytics:InvoiceOperationField"] ?? "OperationType_s";
        var opValue = _config["Azure:LogAnalytics:InvoiceOperationValue"] ?? "Invoice";
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var kql = $@"{table}
| where TimeGenerated >= datetime({today})
| where {opField} == ""{opValue}""
| summarize Total = count(), Succeeded = countif(tolower(Status_s) == ""succeeded""), Failed = countif(tolower(Status_s) == ""failed"") by bin(TimeGenerated, 1h)
| order by TimeGenerated asc";

        var response = await _client.QueryWorkspaceAsync(wsId, kql, QueryTimeRange.All);
        var table2 = response.Value.Table;

        if (table2.Rows.Count == 0)
            return $"📋 Inga fakturor hittades i tabellen `{table}` för idag ({today}).";

        long total = 0;
        foreach (var row in table2.Rows)
        {
            if (long.TryParse(row["Total"]?.ToString(), out var t))
                total += t;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Fakturor skickade idag ({today})");
        sb.AppendLine($"**Totalt: {total} fakturor**");
        sb.AppendLine();
        sb.AppendLine(FormatTable(table2));
        return sb.ToString();
    }

    [McpServerTool]
    [Description("Hämtar integrations-fel från Log Analytics för idag. Returnerar en Markdown-tabell med feldetaljer. Svara på frågor som 'Finns det några fel idag?'")]
    public async Task<string> GetIntegrationErrorsToday(
        [Description("Log Analytics workspace-ID (GUID). Om tomt används värdet från konfigurationen.")] string? workspaceId = null)
    {
        var wsId = workspaceId ?? _config["Azure:LogAnalytics:WorkspaceId"] ?? string.Empty;
        if (string.IsNullOrEmpty(wsId))
            return "❌ Workspace-ID saknas. Ange det som parameter eller konfigurera Azure:LogAnalytics:WorkspaceId i appsettings.json.";

        var table = _config["Azure:LogAnalytics:InvoiceTable"] ?? "IntegrationLogs_CL";
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var kql = $@"{table}
| where TimeGenerated >= datetime({today})
| where tolower(Status_s) in (""failed"", ""error"", ""faulted"")
| project TimeGenerated, OperationType_s, Status_s, ErrorMessage_s, CorrelationId_s
| order by TimeGenerated desc
| take 50";

        var response = await _client.QueryWorkspaceAsync(wsId, kql, QueryTimeRange.All);
        var resultTable = response.Value.Table;

        if (resultTable.Rows.Count == 0)
            return $"✅ Inga fel hittades i tabellen `{table}` för idag ({today}).";

        var sb = new StringBuilder();
        sb.AppendLine($"## Integrations-fel idag ({today}) – {resultTable.Rows.Count} fel");
        sb.AppendLine();
        sb.AppendLine(FormatTable(resultTable));
        return sb.ToString();
    }

    private static string FormatTable(LogsTable table)
    {
        if (table.Columns.Count == 0 || table.Rows.Count == 0)
            return "_Inga resultat._";

        var sb = new StringBuilder();

        // Header
        sb.Append("| ");
        sb.Append(string.Join(" | ", table.Columns.Select(c => c.Name)));
        sb.AppendLine(" |");

        // Separator
        sb.Append("| ");
        sb.Append(string.Join(" | ", table.Columns.Select(_ => "---")));
        sb.AppendLine(" |");

        // Rows
        foreach (var row in table.Rows)
        {
            sb.Append("| ");
            sb.Append(string.Join(" | ", table.Columns.Select(c => row[c.Name]?.ToString()?.Replace("|", "\\|") ?? "")));
            sb.AppendLine(" |");
        }

        return sb.ToString();
    }
}
