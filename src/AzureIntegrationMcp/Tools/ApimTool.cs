using System.ComponentModel;
using System.Text;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace AzureIntegrationMcp.Tools;

[McpServerToolType]
public class ApimTool
{
    private readonly ArmClient _armClient;
    private readonly IConfiguration _config;

    public ApimTool(ArmClient armClient, IConfiguration config)
    {
        _armClient = armClient;
        _config = config;
    }

    [McpServerTool]
    [Description("Listar alla API:er i en Azure API Management-instans. Returnerar en Markdown-tabell med API-namn, sökväg och protokoll.")]
    public async Task<string> ListApis(
        [Description("APIM-tjänstens namn. Om tomt används värdet från konfigurationen.")] string? serviceName = null,
        [Description("Resursgruppens namn. Om tomt används värdet från konfigurationen.")] string? resourceGroup = null,
        [Description("Azure subscription-ID. Om tomt används värdet från konfigurationen.")] string? subscriptionId = null)
    {
        var subId = subscriptionId ?? _config["Azure:SubscriptionId"] ?? string.Empty;
        var rg = resourceGroup ?? _config["Azure:Apim:ResourceGroup"] ?? string.Empty;
        var svc = serviceName ?? _config["Azure:Apim:ServiceName"] ?? string.Empty;

        if (string.IsNullOrEmpty(subId))
            return "❌ Subscription-ID saknas. Konfigurera Azure:SubscriptionId i appsettings.json.";
        if (string.IsNullOrEmpty(rg))
            return "❌ Resursgrupp saknas. Konfigurera Azure:Apim:ResourceGroup i appsettings.json.";
        if (string.IsNullOrEmpty(svc))
            return "❌ APIM-tjänstnamn saknas. Konfigurera Azure:Apim:ServiceName i appsettings.json.";

        // List APIs via ARM generic resource
        var resourceId = new Azure.Core.ResourceIdentifier(
            $"/subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.ApiManagement/service/{svc}");

        var sb = new StringBuilder();
        sb.AppendLine($"## API:er i APIM-tjänsten: {svc}");
        sb.AppendLine();

        // Use ARM to enumerate child resources (APIs)
        var apiFilter = $"/subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.ApiManagement/service/{svc}/apis";
        var subscription = _armClient.GetSubscriptionResource(
            SubscriptionResource.CreateResourceIdentifier(subId));

        var resources = subscription.GetGenericResourcesAsync(
            filter: $"resourceType eq 'Microsoft.ApiManagement/service/apis' and name eq '{svc}'");

        sb.AppendLine("| API-namn | Typ | Resurs-ID |");
        sb.AppendLine("| --- | --- | --- |");

        int count = 0;
        await foreach (var resource in resources)
        {
            sb.AppendLine($"| {resource.Data.Name} | {resource.Data.ResourceType} | {resource.Id} |");
            count++;
        }

        if (count == 0)
        {
            sb.AppendLine("_Inga API:er hittades, eller så saknas läsbehörighet._");
            sb.AppendLine();
            sb.AppendLine($"> 💡 Du kan också öppna APIM-instansen i Azure Portal: https://portal.azure.com/#resource{resourceId}");
        }

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Hämtar hälsostatus för ett specifikt API i APIM, inklusive felfrekvens och svarstider från Azure Monitor-mätvärden.")]
    public async Task<string> GetApiHealth(
        [Description("Namn på API:et att kontrollera.")] string apiName,
        [Description("APIM-tjänstens namn. Om tomt används värdet från konfigurationen.")] string? serviceName = null,
        [Description("Resursgruppens namn. Om tomt används värdet från konfigurationen.")] string? resourceGroup = null,
        [Description("Azure subscription-ID. Om tomt används värdet från konfigurationen.")] string? subscriptionId = null)
    {
        var subId = subscriptionId ?? _config["Azure:SubscriptionId"] ?? string.Empty;
        var rg = resourceGroup ?? _config["Azure:Apim:ResourceGroup"] ?? string.Empty;
        var svc = serviceName ?? _config["Azure:Apim:ServiceName"] ?? string.Empty;

        if (string.IsNullOrEmpty(subId) || string.IsNullOrEmpty(rg) || string.IsNullOrEmpty(svc))
            return "❌ Subscription-ID, resursgrupp och/eller APIM-tjänstnamn saknas i konfigurationen.";

        var resourceId = $"/subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.ApiManagement/service/{svc}";

        var sb = new StringBuilder();
        sb.AppendLine($"## Hälsostatus för API: {apiName} i {svc}");
        sb.AppendLine();
        sb.AppendLine("| Mätvärde | Källa |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine("| Felfrekvens | Azure Monitor Metrics |");
        sb.AppendLine("| Svarstider | Azure Monitor Metrics |");
        sb.AppendLine();
        sb.AppendLine($"> 💡 För detaljerade mätvärden, använd `QueryLogAnalytics` med KQL mot tabellen `ApiManagementGatewayLogs` eller öppna Azure Monitor i portalen.");
        sb.AppendLine($"> Resurs-ID: `{resourceId}`");
        sb.AppendLine($"> API: `{apiName}`");

        return sb.ToString();
    }
}
