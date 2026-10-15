using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared;

var factory = new ConnectionFactory { HostName = "localhost" };

await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await channel.QueueDeclareAsync(
    queue: "notificacoes",
    durable: true,
    exclusive: false,
    autoDelete: false);

var eventos = new[]
{
    new EventoNotificacao(
        "user.registered",
        new DadosEvento("Ana", "ana@exemplo.com")),

    new EventoNotificacao(
        "order.confirmed",
        new DadosEvento("Ana", "ana@exemplo.com", OrderId: "PED-1001", Total: 249.90m)),

    new EventoNotificacao(
        "evento.inexistente",
        new DadosEvento("Ana", "ana@exemplo.com"))
};

foreach (var evento in eventos)
{
    var json = JsonSerializer.Serialize(evento);
    var body = Encoding.UTF8.GetBytes(json);

    await channel.BasicPublishAsync(
        exchange: string.Empty,
        routingKey: "notificacoes",
        body: body);

    Console.WriteLine($"Evento enviado: {json}");
}