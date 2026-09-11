using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace FCG.NotificationsForwarder.Services;

public class SqsForwarderService : ISqsForwarderService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsForwarderService> _logger;

    public SqsForwarderService(IAmazonSQS sqsClient, ILogger<SqsForwarderService> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
    }

    public async Task PublishAsync(
        string queueUrl, string eventType, string messageBodyJson, CancellationToken ct = default)
    {
        var request = new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBodyJson,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["EventType"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = eventType
                }
            }
        };

        // Deixa a exceção propagar em caso de falha — quem chama decide
        // se reprocessa (retry) ou encaminha para a fila de erro (DLQ).
        var response = await _sqsClient.SendMessageAsync(request, ct);

        _logger.LogInformation(
            "[SQS] Mensagem publicada | EventType={EventType} | MessageId={MessageId} | Fila={QueueUrl}",
            eventType, response.MessageId, queueUrl);
    }
}
