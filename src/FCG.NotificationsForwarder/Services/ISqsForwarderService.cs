namespace FCG.NotificationsForwarder.Services;

public interface ISqsForwarderService
{
    /// <summary>
    /// Publica a mensagem no SQS no formato esperado pela Lambda:
    /// corpo = JSON do evento, atributo "EventType" = nome do tipo do evento.
    /// Lança exceção em caso de falha — quem chama decide o que fazer (retry/DLQ).
    /// </summary>
    Task PublishAsync(string queueUrl, string eventType, string messageBodyJson, CancellationToken ct = default);
}
