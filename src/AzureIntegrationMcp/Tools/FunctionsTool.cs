using System.ComponentModel;
using System.Text;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace AzureIntegrationMcp.Tools;

[McpServerToolType]
public class FunctionsTool
{
    private readonly ArmClient _armClient;
    private readonly LogsQueryClient _logsClient;
    private readonly IConfiguration _config;

    public FunctionsTool(ArmClient armClient, LogsQueryClient logsClient, IConfiguration config)
    {
        _armClient = armClient;
        _logsClient = logsClient;
        _config = config;
    }

    [McpServerTool]
    [Description("Listar Azure Function Apps i en resursgrupp eller i hela prenumerationen. Returnerar en Markdown-tabell med namn, runtime, plats och status.")]
    public async Task<string> ListFunctionApps(
        [Description("Azure subscription-ID. Om tomt används värdet från konfigurationen.")] string? subscriptionId = null,
        [Description("Resursgruppens namn. Lämna tomt för att lista i hela prenumerationen.")] string? resourceGroup = null)
    {
        var subId = subscriptionId ?? _config["Azure:SubscriptionId"] ?? string.Empty;
        if (string.IsNullOrEmpty(subId))
            return "❌ Subscription-ID saknas. Konfigurera Azure:SubscriptionId i appsettings.json.";

        var subscription = _armClient.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subId));

        var sb = new StringBuilder();
        sb.AppendLine("## Azure Function Apps");
        sb.AppendLine();
        sb.AppendLine("| Namn | Typ | Resursgrupp | Plats |");
        sb.AppendLine("| --- | --- | --- | --- |");

        if (!string.IsNullOrEmpty(resourceGroup))
        {
            var rg = await subscription.GetResourceGroupAsync(resourceGroup);
            var resources = rg.Value.GetGenericResourcesAsync(
                filter: "resourceType eq 'Microsoft.Web/sites'");

            await foreach (var resource in resources)
            {
                var kind = resource.Data.Kind ?? "";
                if (!kind.Contains("functionapp", StringComparison.OrdinalIgnoreCase))
                    continue;

                sb.AppendLine($"| {resource.Data.Name} | {kind} | {resourceGroup} | {resource.Data.Location} |");
            }
        }
        else
        {
            var resources = subscription.GetGenericResourcesAsync(
                filter: "resourceType eq 'Microsoft.Web/sites'");

            await foreach (var resource in resources)
            {
                var kind = resource.Data.Kind ?? "";
                if (!kind.Contains("functionapp", StringComparison.OrdinalIgnoreCase))
                    continue;

                var rgName = resource.Id.ResourceGroupName ?? "";
                sb.AppendLine($"| {resource.Data.Name} | {kind} | {rgName} | {resource.Data.Location} |");
            }
        }

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Hämtar senaste fel och undantag för en Azure Function App från Log Analytics. Returnerar en Markdown-tabell med feldetaljer.")]
    public async Task<string> GetFunctionErrors(
        [Description("Namn på Function App.")] string functionAppName,
        [Description("Log Analytics workspace-ID. Om tomt används värdet från konfigurationen.")] string? workspaceId = null,
        [Description("Antal timmar bakåt att söka efter fel (standard: 24).")] int hours = 24)
    {
        var wsId = workspaceId ?? _config["Azure:LogAnalytics:WorkspaceId"] ?? string.Empty;
        if (string.IsNullOrEmpty(wsId))
            return "❌ Workspace-ID saknas. Konfigurera Azure:LogAnalytics:WorkspaceId i appsettings.json.";

        var kql = $@"union exceptions, traces
| where timestamp >= ago({hours}h)
| where cloud_RoleName =~ ""{functionAppName}""
| where severityLevel >= 3 or itemType == ""exception""
| project timestamp, severityLevel, message, outerMessage = tostring(customDimensions[""prop__outerMessage""]), operation_Name, cloud_RoleName
| order by timestamp desc
| take 50";

        var response = await _logsClient.QueryWorkspaceAsync(wsId, kql, QueryTimeRange.All);
        var table = response.Value.Table;

        if (table.Rows.Count == 0)
            return $"✅ Inga fel hittades för Function App `{functionAppName}` de senaste {hours} timmarna.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Fel i Function App: {functionAppName} (senaste {hours}h)");
        sb.AppendLine();
        sb.AppendLine(FormatTable(table));
        return sb.ToString();
    }

    private static string FormatTable(Azure.Monitor.Query.Models.LogsTable table)
    {
        if (table.Columns.Count == 0 || table.Rows.Count == 0)
            return "_Inga resultat._";

        var sb = new StringBuilder();

        sb.Append("| ");
        sb.Append(string.Join(" | ", table.Columns.Select(c => c.Name)));
        sb.AppendLine(" |");

        sb.Append("| ");
        sb.Append(string.Join(" | ", table.Columns.Select(_ => "---")));
        sb.AppendLine(" |");

        foreach (var row in table.Rows)
        {
            sb.Append("| ");
            sb.Append(string.Join(" | ", table.Columns.Select(c => row[c.Name]?.ToString()?.Replace("|", "\\|") ?? "")));
            sb.AppendLine(" |");
        }

        return sb.ToString();
    }
}
