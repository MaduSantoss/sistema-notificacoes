using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;

const string FilaPrincipal = "notificacoes";
const string FilaRetry = "notificacoes.retry";
const string FilaFalhas = "notificacoes.falhas";
const int MaxTentativas = 3;
const int EsperaRetryMs = 10_000;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole(o =>
    {
        o.TimestampFormat = "HH:mm:ss ";
        o.SingleLine = true;
    }));

var logger = loggerFactory.CreateLogger("Consumidor");

logger.LogInformation("Consumidor iniciando...");

var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "localhost";

var factory = new ConnectionFactory { HostName = rabbitHost };

await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(FilaPrincipal, durable: true, exclusive: false, autoDelete: false);

await channel.QueueDeclareAsync(FilaFalhas, durable: true, exclusive: false, autoDelete: false);

await channel.QueueDeclareAsync(
    FilaRetry,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: new Dictionary<string, object?>
    {
        ["x-message-ttl"] = EsperaRetryMs,
        ["x-dead-letter-exchange"] = "",
        ["x-dead-letter-routing-key"] = FilaPrincipal
    });

var opcoes = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var processados = new HashSet<string>();
var consumer = new AsyncEventingBasicConsumer(channel);

consumer.ReceivedAsync += async (sender, ea) =>
{
    var json = Encoding.UTF8.GetString(ea.Body.ToArray());

    EventoNotificacao? evento;
    try
    {
        evento = JsonSerializer.Deserialize<EventoNotificacao>(json, opcoes);
    }
    catch (JsonException)
    {
        evento = null;
    }

    if (evento is null || evento.Data is null || string.IsNullOrWhiteSpace(evento.Data.Email))
    {
        logger.LogWarning("Mensagem inválida, descartada: {Json}", json);
        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        return;
    }

    var email = TemplateEmail.Montar(evento);
    if (email is null)
    {
        logger.LogWarning("Tipo de evento desconhecido, descartado: {Tipo}", evento.Type);
        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        return;
    }

    if (!string.IsNullOrEmpty(evento.EventId) && processados.Contains(evento.EventId))
    {
        logger.LogInformation("Evento {EventId} já processado, ignorado.", evento.EventId);
        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        return;
    }

    var tentativas = LerTentativas(ea.BasicProperties);
    logger.LogInformation("Evento {Tipo} para {Email} (tentativa {Tentativa})", evento.Type, evento.Data.Email, tentativas + 1);

    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await EnviarEmailAsync(smtpHost, evento.Data.Name, evento.Data.Email, email, cts.Token);
        logger.LogInformation("E-mail enviado para {Email}", evento.Data.Email);

        if (!string.IsNullOrEmpty(evento.EventId))
            processados.Add(evento.EventId);

        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
    }
    catch (Exception ex)
    {
        logger.LogWarning("Falha ao enviar para {Email}: {Erro}", evento.Data.Email, ex.Message);

        var proximaTentativa = tentativas + 1;
        var destino = proximaTentativa >= MaxTentativas ? FilaFalhas : FilaRetry;

        var props = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object?> { ["x-tentativas"] = proximaTentativa }
        };

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: destino,
            mandatory: false,
            basicProperties: props,
            body: ea.Body);

        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

        if (destino == FilaFalhas)
            logger.LogError("Desistindo de {Email} após {Tentativas} tentativas. Enviada para '{Fila}'.", evento.Data.Email, proximaTentativa, FilaFalhas);
        else
            logger.LogInformation("Nova tentativa em {Segundos}s (tentativa {Tentativa} de {Max}).", EsperaRetryMs / 1000, proximaTentativa, MaxTentativas);
    }
};

await channel.BasicConsumeAsync(FilaPrincipal, autoAck: false, consumer: consumer);

logger.LogInformation("Aguardando mensagens...");
await Task.Delay(Timeout.Infinite);

static int LerTentativas(IReadOnlyBasicProperties props)
{
    if (props.Headers is not null &&
        props.Headers.TryGetValue("x-tentativas", out var valor) &&
        valor is not null)
    {
        return Convert.ToInt32(valor);
    }
    return 0;
}

static async Task EnviarEmailAsync(string smtpHost, string nome, string destino, EmailPronto conteudo, CancellationToken ct)
{
    var mensagem = new MimeMessage();
    mensagem.From.Add(new MailboxAddress("Sistema de Notificações", "noreply@exemplo.com"));
    mensagem.To.Add(new MailboxAddress(nome, destino));
    mensagem.Subject = conteudo.Assunto;
    mensagem.Body = new TextPart("plain") { Text = conteudo.Corpo };

    using var smtp = new SmtpClient();
    await smtp.ConnectAsync(smtpHost, 1025, MailKit.Security.SecureSocketOptions.None, ct);
    await smtp.SendAsync(mensagem, ct);
    await smtp.DisconnectAsync(true, ct);
}