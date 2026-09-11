using System.Text.Json;
using FCG.NotificationsForwarder.Services;
using FCG.NotificationsForwarder.Settings;
using FCG.Shared.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FCG.NotificationsForwarder.Consumers;

/// <summary>
/// Consome UserCreatedEvent da fila "fcg.user-created" e encaminha para o SQS.
/// O ack no RabbitMQ só acontece se o Consume terminar sem exceção
/// (comportamento padrão do MassTransit) — ou seja, só após o SQS confirmar o publish.
/// </summary>
public class UserCreatedForwardConsumer : IConsumer<UserCreatedEvent>
{
    private readonly ISqsForwarderService _sqsForwarder;
    private readonly SqsSettings _sqsSettings;
    private readonly ILogger<UserCreatedForwardConsumer> _logger;

    public UserCreatedForwardConsumer(
        ISqsForwarderService sqsForwarder,
        IOptions<SqsSettings> sqsSettings,
        ILogger<UserCreatedForwardConsumer> logger)
    {
        _sqsForwarder = sqsForwarder;
        _sqsSettings = sqsSettings.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "[UserCreatedEvent] Recebido do RabbitMQ | UserId={UserId} | IsAdmin={IsAdmin}",
            evt.UserId, evt.IsAdmin);

        var json = JsonSerializer.Serialize(evt);

        // Se isso lançar exceção, o MassTransit NÃO faz ack —
        // a mensagem é retentada e, após esgotar as tentativas,
        // encaminhada automaticamente para a fila de erro (DLQ).
        await _sqsForwarder.PublishAsync(
            _sqsSettings.UserCreatedQueueUrl,
            eventType: "UserCreatedEvent",
            messageBodyJson: json,
            ct: context.CancellationToken);

        _logger.LogInformation(
            "[UserCreatedEvent] Encaminhado ao SQS com sucesso | UserId={UserId}", evt.UserId);
    }
}
