using System.ComponentModel;
using System.Text;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace AzureIntegrationMcp.Tools;

[McpServerToolType]
public class ServiceBusTool
{
    private readonly IConfiguration _config;

    public ServiceBusTool(IConfiguration config)
    {
        _config = config;
    }

    private ServiceBusAdministrationClient CreateAdminClient()
    {
        var ns = _config["Azure:ServiceBus:FullyQualifiedNamespace"] ?? string.Empty;
        if (string.IsNullOrEmpty(ns))
            throw new InvalidOperationException("Azure:ServiceBus:FullyQualifiedNamespace är inte konfigurerat i appsettings.json.");

        var credential = new Azure.Identity.DefaultAzureCredential();
        return new ServiceBusAdministrationClient(ns, credential);
    }

    [McpServerTool]
    [Description("Hämtar status för en specifik Service Bus-kö: antal aktiva meddelanden, dead-letter-meddelanden och schemalagda meddelanden.")]
    public async Task<string> GetQueueStatus(
        [Description("Namn på Service Bus-kön.")] string queueName)
    {
        var adminClient = CreateAdminClient();
        var ns = _config["Azure:ServiceBus:FullyQualifiedNamespace"];

        var properties = await adminClient.GetQueueRuntimePropertiesAsync(queueName);
        var q = properties.Value;

        var sb = new StringBuilder();
        sb.AppendLine($"## Service Bus-kö: {queueName}");
        sb.AppendLine();
        sb.AppendLine("| Egenskap | Värde |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine($"| Namespace | {ns} |");
        sb.AppendLine($"| Aktiva meddelanden | {q.ActiveMessageCount} |");
        sb.AppendLine($"| Dead-letter-meddelanden | {q.DeadLetterMessageCount} |");
        sb.AppendLine($"| Schemalagda meddelanden | {q.ScheduledMessageCount} |");
        sb.AppendLine($"| Totalt antal meddelanden | {q.TotalMessageCount} |");
        sb.AppendLine($"| Skapades | {q.CreatedAt:yyyy-MM-dd HH:mm} |");
        sb.AppendLine($"| Uppdaterades | {q.UpdatedAt:yyyy-MM-dd HH:mm} |");
        sb.AppendLine($"| Senaste åtkomst | {q.AccessedAt:yyyy-MM-dd HH:mm} |");

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Listar alla Service Bus-köer som har dead-letter-meddelanden. Returnerar en Markdown-tabell med könamn och antal dead-letter-meddelanden.")]
    public async Task<string> ListQueuesWithDeadLetters()
    {
        var adminClient = CreateAdminClient();
        var ns = _config["Azure:ServiceBus:FullyQualifiedNamespace"];

        var sb = new StringBuilder();
        sb.AppendLine("## Service Bus-köer med dead-letter-meddelanden");
        sb.AppendLine();
        sb.AppendLine("| Kö | Dead-letter | Aktiva | Schemalagda |");
        sb.AppendLine("| --- | --- | --- | --- |");

        int found = 0;
        await foreach (var queue in adminClient.GetQueuesAsync())
        {
            var props = await adminClient.GetQueueRuntimePropertiesAsync(queue.Name);
            var q = props.Value;
            if (q.DeadLetterMessageCount > 0)
            {
                sb.AppendLine($"| {q.Name} | **{q.DeadLetterMessageCount}** | {q.ActiveMessageCount} | {q.ScheduledMessageCount} |");
                found++;
            }
        }

        if (found == 0)
        {
            sb.AppendLine("_Inga köer med dead-letter-meddelanden hittades._");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"**Totalt {found} kö(er) med dead-letter-meddelanden.**");
        }

        return sb.ToString();
    }
}
