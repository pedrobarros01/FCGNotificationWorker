using System.Text.Json;
using FCG.NotificationsForwarder.Services;
using FCG.NotificationsForwarder.Settings;
using FCG.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FCG.NotificationsForwarder.Consumers;

/// <summary>
/// Consome PaymentProcessedEvent da fila "payment.processed" e encaminha para o SQS.
/// Encaminha TODOS os status — a Lambda decide filtrar apenas "Approved".
/// </summary>
public class PaymentProcessedForwardConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly ISqsForwarderService _sqsForwarder;
    private readonly SqsSettings _sqsSettings;
    private readonly ILogger<PaymentProcessedForwardConsumer> _logger;

    public PaymentProcessedForwardConsumer(
        ISqsForwarderService sqsForwarder,
        IOptions<SqsSettings> sqsSettings,
        ILogger<PaymentProcessedForwardConsumer> logger)
    {
        _sqsForwarder = sqsForwarder;
        _sqsSettings = sqsSettings.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "[PaymentProcessedEvent] Recebido do RabbitMQ | OrderId={OrderId} | Status={Status}",
            evt.OrderId, evt.Status);

        var json = JsonSerializer.Serialize(evt);

        await _sqsForwarder.PublishAsync(
            _sqsSettings.PaymentProcessedQueueUrl,
            eventType: "PaymentProcessedEvent",
            messageBodyJson: json,
            ct: context.CancellationToken);

        _logger.LogInformation(
            "[PaymentProcessedEvent] Encaminhado ao SQS com sucesso | OrderId={OrderId}", evt.OrderId);
    }
}
