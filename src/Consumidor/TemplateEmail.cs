using System.Globalization;
using Shared;

public record EmailPronto(string Assunto, string Corpo);

public static class TemplateEmail
{
    public static EmailPronto? Montar(EventoNotificacao evento)
    {
        var d = evento.Data;

        return evento.Type switch
        {
            "user.registered" => new EmailPronto(
                "Bem-vinda(o) ao nosso sistema!",
                $"Olá, {d.Name}!\n\n" +
                "Seu cadastro foi realizado com sucesso.\n" +
                "Estamos felizes em ter você por aqui."),

            "order.confirmed" => new EmailPronto(
                $"Pedido {d.OrderId} confirmado",
                $"Olá, {d.Name}!\n\n" +
                $"Recebemos o seu pedido {d.OrderId}.\n" +
                $"Valor total: {FormatarValor(d.Total)}\n\n" +
                "Avisaremos quando ele for enviado."),

            _ => null
        };
    }

    private static string FormatarValor(decimal? valor) =>
        valor is null
            ? "não informado"
            : valor.Value.ToString("C", new CultureInfo("pt-BR"));
}