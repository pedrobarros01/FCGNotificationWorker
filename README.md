# FCG.NotificationsForwarder

Worker que faz a ponte entre o **RabbitMQ** (mensageria interna do sistema) e o **Amazon SQS** (trigger da Lambda de notificações).

---

## Clonando o repositório

Este repositório utiliza **git submodules** (o projeto `FCG.Shared` em `src/submodule/FCG.Shared`). Para clonar com tudo incluído, use o argumento `--recurse-submodules`:

```bash
git clone --recurse-submodules https://github.com/pedrobarros01/FCGNotificationWorker.git
```

Se você já clonou sem o submodule, inicialize-o manualmente:

```bash
git submodule update --init --recursive
```

---

## Por que esse serviço existe

A `NotificationsAPI` foi migrada para **AWS Lambda**, e Lambda não tem trigger nativo para RabbitMQ — só para SQS, Kinesis, DynamoDB Streams, etc.

Esse worker resolve a ponte: fica sempre rodando (é o único componente do fluxo de notificações que continua como container), escuta as filas do RabbitMQ e replica cada mensagem para a fila SQS correspondente, que então dispara a Lambda.

```
UsersAPI ──────► RabbitMQ (fcg.user-created)  ──► Forwarder ──► SQS user-created     ──► Lambda
PaymentsAPI ───► RabbitMQ (payment.processed) ──► Forwarder ──► SQS payment-processed ──► Lambda
```

---

## Filas consumidas

| Fila RabbitMQ | Evento | Fila SQS destino |
|---|---|---|
| `fcg.user-created` | `UserCreatedEvent` (cobre boas-vindas padrão **e** admin, via flag `IsAdmin`) | `SQS_URL_USER_CREATED` |
| `payment.processed` | `PaymentProcessedEvent` | `SQS_URL_PAYMENT_PROCESSED` |

O worker encaminha **todas** as mensagens dessas filas — o filtro de "só envia e-mail se `Status == Approved`" continua sendo responsabilidade da Lambda, não do forwarder.

---

## Garantias de entrega

- **Ack manual implícito**: o MassTransit só confirma (ack) a mensagem no RabbitMQ depois que o método `Consume` termina sem exceção — ou seja, só depois que o `SendMessageAsync` do SQS retorna com sucesso.
- **Retry**: em caso de falha ao publicar no SQS, a mensagem é retentada automaticamente **3 vezes com intervalo de 5 segundos**.
- **DLQ**: se todas as tentativas falharem, o MassTransit move a mensagem automaticamente para uma fila de erro (`<nome-da-fila>_error`), que funciona como Dead Letter Queue. Nada é perdido — o time pode inspecionar e reprocessar manualmente depois.

---

## Variáveis de ambiente

| Variável | Descrição | Padrão |
|---|---|---|
| `RabbitMQ__Host` | Host do RabbitMQ | `rabbitmq` |
| `RabbitMQ__Username` | Usuário RabbitMQ | `fcg_user` |
| `RabbitMQ__Password` | Senha RabbitMQ | `fcg_password` |
| `RabbitMQ__Queues__UserCreated` | Nome da fila de usuários | `fcg.user-created` |
| `RabbitMQ__Queues__PaymentProcessed` | Nome da fila de pagamentos | `payment.processed.sqs` |
| `Sqs__Region` | Região AWS | `us-east-1` |
| `Sqs__UrlService` | URL base do serviço SQS | `https://localhost.localstack.cloud:4566` |
| `Sqs__AccessKeyId` | Access Key ID AWS | `AKIAIOSFODNN7EXAMPLE` |
| `Sqs__SecretAccessKey` | Secret Access Key AWS | `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY` |
| `Sqs__SessionToken` | Session Token AWS | `AKIAIOSFODNN7EXAMPLE` |
| `Sqs__UserCreatedQueueUrl` | URL da fila SQS de usuários | — |
| `Sqs__PaymentProcessedQueueUrl` | URL da fila SQS de pagamentos | — |
| `AWS_ACCESS_KEY_ID` | Credencial AWS | — |
| `AWS_SECRET_ACCESS_KEY` | Credencial AWS | — |

> As credenciais AWS podem vir de variáveis de ambiente padrão do SDK (`AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY`), de um profile local, ou de uma IAM Role se rodando em ECS/EC2 — o `AmazonSQSClient` resolve isso automaticamente.

---

## Rodando localmente

```bash
docker build -t fcg-notifications-forwarder .

docker run --rm \
  -e RabbitMQ__Host=localhost \
  -e RabbitMQ__Username=fcg_user \
  -e RabbitMQ__Password=fcg_password \
  -e Sqs__Region=us-east-1 \
  -e Sqs__UserCreatedQueueUrl=https://sqs.us-east-1.amazonaws.com/<account-id>/fcg-user-created \
  -e Sqs__PaymentProcessedQueueUrl=https://sqs.us-east-1.amazonaws.com/<account-id>/fcg-payment-processed \
  -e AWS_ACCESS_KEY_ID=xxx \
  -e AWS_SECRET_ACCESS_KEY=yyy \
  --network host \
  fcg-notifications-forwarder
```

---

## Testando o fluxo completo

1. Suba o `docker-compose.yml` do FCG.Infra (RabbitMQ + serviços) com este worker incluso
2. Cadastre um usuário novo via UsersAPI
3. Verifique nos logs do forwarder: `[UserCreatedEvent] Encaminhado ao SQS com sucesso`
4. Verifique nos logs da Lambda (`aws logs tail /aws/lambda/FCG-NotificationsAPI --follow`): `[EMAIL ENVIADO]`
5. Confira a caixa de entrada do e-mail cadastrado
