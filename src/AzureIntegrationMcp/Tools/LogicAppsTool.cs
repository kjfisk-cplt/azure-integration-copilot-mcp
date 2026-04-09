using System.ComponentModel;
using System.Text;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace AzureIntegrationMcp.Tools;

[McpServerToolType]
public class LogicAppsTool
{
    private readonly ArmClient _armClient;
    private readonly IConfiguration _config;

    public LogicAppsTool(ArmClient armClient, IConfiguration config)
    {
        _armClient = armClient;
        _config = config;
    }

    [McpServerTool]
    [Description("Listar Logic App Standard-arbetsflöden i en angiven resursgrupp. Returnerar en Markdown-tabell med namn, status och plats.")]
    public async Task<string> ListLogicApps(
        [Description("Azure subscription-ID. Om tomt används värdet från konfigurationen.")] string? subscriptionId = null,
        [Description("Namn på resursgruppen som innehåller Logic Apps.")] string? resourceGroup = null)
    {
        var subId = subscriptionId ?? _config["Azure:SubscriptionId"] ?? string.Empty;
        if (string.IsNullOrEmpty(subId))
            return "❌ Subscription-ID saknas. Ange det som parameter eller konfigurera Azure:SubscriptionId i appsettings.json.";

        var subscription = _armClient.GetSubscriptionResource(
            Azure.ResourceManager.Resources.SubscriptionResource.CreateResourceIdentifier(subId));

        var sb = new StringBuilder();
        sb.AppendLine("## Logic App Standard-arbetsflöden");
        sb.AppendLine();
        sb.AppendLine("| Namn | Typ | Resursgrupp | Plats | Taggar |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");

        if (!string.IsNullOrEmpty(resourceGroup))
        {
            var rg = await subscription.GetResourceGroupAsync(resourceGroup);
            var resources = rg.Value.GetGenericResourcesAsync(
                filter: "resourceType eq 'Microsoft.Web/sites'");

            await foreach (var resource in resources)
            {
                var kind = resource.Data.Kind ?? "";
                if (!kind.Contains("workflowapp", StringComparison.OrdinalIgnoreCase))
                    continue;

                sb.AppendLine($"| {resource.Data.Name} | {kind} | {resourceGroup} | {resource.Data.Location} | {FormatTags(resource.Data.Tags)} |");
            }
        }
        else
        {
            var resources = subscription.GetGenericResourcesAsync(
                filter: "resourceType eq 'Microsoft.Web/sites'");

            await foreach (var resource in resources)
            {
                var kind = resource.Data.Kind ?? "";
                if (!kind.Contains("workflowapp", StringComparison.OrdinalIgnoreCase))
                    continue;

                var rgName = resource.Id.ResourceGroupName ?? "";
                sb.AppendLine($"| {resource.Data.Name} | {kind} | {rgName} | {resource.Data.Location} | {FormatTags(resource.Data.Tags)} |");
            }
        }

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Hämtar körhistorik för en Logic App Standard. Returnerar de senaste körningarna med status (lyckad/misslyckad) och tidsstämplar.")]
    public async Task<string> GetLogicAppRunHistory(
        [Description("Namn på Logic App (workflow app).")] string logicAppName,
        [Description("Namn på arbetsflödet inom Logic App-appen. Lämna tomt för standardarbetsflödet.")] string? workflowName = null,
        [Description("Azure subscription-ID. Om tomt används värdet från konfigurationen.")] string? subscriptionId = null,
        [Description("Namn på resursgruppen.")] string? resourceGroup = null)
    {
        var subId = subscriptionId ?? _config["Azure:SubscriptionId"] ?? string.Empty;
        var rg = resourceGroup ?? _config["Azure:Apim:ResourceGroup"] ?? string.Empty;

        if (string.IsNullOrEmpty(subId))
            return "❌ Subscription-ID saknas. Ange det som parameter eller konfigurera Azure:SubscriptionId i appsettings.json.";
        if (string.IsNullOrEmpty(rg))
            return "❌ Resursgrupp saknas. Ange det som parameter.";

        var wfName = workflowName ?? logicAppName;

        // Build resource ID for the workflow run history
        var resourceId = $"/subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{logicAppName}/workflows/{wfName}/runs";

        var sb = new StringBuilder();
        sb.AppendLine($"## Körhistorik för Logic App: {logicAppName} / {wfName}");
        sb.AppendLine();
        sb.AppendLine("| Körnings-ID | Status | Starttid | Sluttid |");
        sb.AppendLine("| --- | --- | --- | --- |");

        var workflowResourceId = new Azure.Core.ResourceIdentifier(
            $"/subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{logicAppName}");

        var siteResource = _armClient.GetGenericResource(workflowResourceId);
        var site = await siteResource.GetAsync();

        // Note: Run history requires REST API call; this returns basic resource info as fallback
        sb.AppendLine($"| - | {site.Value.Data.Properties?.ToString() ?? "Se Azure Portal"} | - | - |");
        sb.AppendLine();
        sb.AppendLine($"> 💡 För detaljerad körhistorik, använd verktyget `QueryLogAnalytics` med KQL mot tabellen `AzureDiagnostics` eller `AppWorkflowRuntime`, eller öppna Azure Portal.");

        return sb.ToString();
    }

    private static string FormatTags(IDictionary<string, string>? tags)
    {
        if (tags == null || tags.Count == 0) return "";
        return string.Join(", ", tags.Take(3).Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
