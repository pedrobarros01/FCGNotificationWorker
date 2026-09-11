namespace FCG.NotificationsForwarder.Settings;

public class SqsSettings
{
    public const string SectionName = "Sqs";

    public string Region { get; set; } = "us-east-1";
    public string UrlService { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>URL completa da fila SQS que recebe UserCreatedEvent.</summary>
    public string UserCreatedQueueUrl { get; set; } = string.Empty;

    /// <summary>URL completa da fila SQS que recebe PaymentProcessedEvent.</summary>
    public string PaymentProcessedQueueUrl { get; set; } = string.Empty;
}
