using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using FCG.NotificationsForwarder.Consumers;
using FCG.NotificationsForwarder.Services;
using FCG.NotificationsForwarder.Settings;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);
var httpClientHandler = new HttpClientHandler();

httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
{
    return true; 
};
var configuration = builder.Configuration;

// ── Configuração tipada ────────────────────────────────────
builder.Services.Configure<SqsSettings>(configuration.GetSection(SqsSettings.SectionName));

// ── Cliente SQS ─────────────────────────────────────────────
builder.Services.AddTransient<IAmazonSQS>(_ =>
{
    var region = configuration["Sqs:Region"] ?? "us-east-1";
    
    var credentials = new SessionAWSCredentials(configuration["Sqs:AccessKeyId"] ?? "test", configuration["Sqs:SecretAccessKey"] ?? "test", configuration["Sqs:SessionToken"] ?? "test");

    var config = new AmazonSQSConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(region)
    };
    var queueUrl = configuration["Sqs:UserCreatedQueueUrl"];
    if (!string.IsNullOrEmpty(queueUrl))
    {
        var uri = new Uri(queueUrl);
        config.ServiceURL = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
    }

    return new AmazonSQSClient(credentials, config);
});

builder.Services.AddTransient<ISqsForwarderService, SqsForwarderService>();

// ── MassTransit / RabbitMQ ─────────────────────────────────
var rabbitHost = configuration["RabbitMQ:Host"] ?? "rabbitmq";
var rabbitUser = configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserCreatedForwardConsumer>();
    x.AddConsumer<PaymentProcessedForwardConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // Fila de usuários criados (welcome padrão + welcome admin)
        cfg.ReceiveEndpoint(
            configuration["RabbitMQ:Queues:UserCreated"] ?? "fcg.user-created",
            e =>
            {
                // 3 tentativas com 5s de intervalo antes de desistir.
                // Após esgotar, o MassTransit move a mensagem
                // automaticamente para "fcg.user-created_error" (DLQ).
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                e.ConfigureConsumer<UserCreatedForwardConsumer>(ctx);
            });

        // Fila de pagamentos processados
        cfg.ReceiveEndpoint(
            configuration["RabbitMQ:Queues:PaymentProcessed"] ?? "payment.processed",
            e =>
            {
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                e.ConfigureConsumer<PaymentProcessedForwardConsumer>(ctx);
            });
    });
});

var host = builder.Build();
await host.RunAsync();
