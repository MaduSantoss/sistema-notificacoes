using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
var factory = new ConnectionFactory { HostName = rabbitHost };
var connection = await factory.CreateConnectionAsync();
var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(
    queue: "notificacoes",
    durable: true,
    exclusive: false,
    autoDelete: false);

var publishLock = new SemaphoreSlim(1, 1);

app.MapPost("/events", async (EventoNotificacao evento) =>
{
    var erros = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(evento.Type))
        erros["type"] = ["O campo 'type' é obrigatório."];

    if (evento.Data is null)
        erros["data"] = ["O campo 'data' é obrigatório."];
    else
    {
        if (string.IsNullOrWhiteSpace(evento.Data.Name))
            erros["data.name"] = ["O campo 'name' é obrigatório."];

        if (string.IsNullOrWhiteSpace(evento.Data.Email) || !evento.Data.Email.Contains('@'))
            erros["data.email"] = ["Informe um e-mail válido."];
    }

    if (erros.Count > 0)
        return Results.ValidationProblem(erros);

    var eventoComId = evento with { EventId = evento.EventId ?? Guid.NewGuid().ToString() };
    var json = JsonSerializer.Serialize(eventoComId);
    var body = Encoding.UTF8.GetBytes(json);

    var props = new BasicProperties { Persistent = true };

    await publishLock.WaitAsync();
    try
    {
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "notificacoes",
            mandatory: false,
            basicProperties: props,
            body: body);
    }
    finally
    {
        publishLock.Release();
    }

    return Results.Accepted(value: new { mensagem = "Evento recebido e enviado para a fila." });
});

app.Run();