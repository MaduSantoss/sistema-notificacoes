using Shared;
using Xunit;

public class TemplateEmailTests
{
    [Fact]
    public void UserRegistered_GeraEmailDeBoasVindas_ComONomeDaPessoa()
    {
        var evento = new EventoNotificacao(
            "user.registered",
            new DadosEvento("Ana", "ana@exemplo.com"));

        var email = TemplateEmail.Montar(evento);

        Assert.NotNull(email);
        Assert.Equal("Bem-vinda(o) ao nosso sistema!", email.Assunto);
        Assert.Contains("Olá, Ana!", email.Corpo);
    }

    [Fact]
    public void OrderConfirmed_IncluiNumeroDoPedido_EValorEmReais()
    {
        var evento = new EventoNotificacao(
            "order.confirmed",
            new DadosEvento("Ana", "ana@exemplo.com", OrderId: "PED-1001", Total: 249.90m));

        var email = TemplateEmail.Montar(evento);

        Assert.NotNull(email);
        Assert.Equal("Pedido PED-1001 confirmado", email.Assunto);
        Assert.Contains("PED-1001", email.Corpo);
        Assert.Contains("249,90", email.Corpo);
    }

    [Fact]
    public void EventoDesconhecido_RetornaNull()
    {
        var evento = new EventoNotificacao(
            "evento.inexistente",
            new DadosEvento("Ana", "ana@exemplo.com"));

        var email = TemplateEmail.Montar(evento);

        Assert.Null(email);
    }

    [Fact]
    public void OrderConfirmed_SemTotal_MostraNaoInformado()
    {
        var evento = new EventoNotificacao(
            "order.confirmed",
            new DadosEvento("Caio", "caio@exemplo.com", OrderId: "PED-2003"));

        var email = TemplateEmail.Montar(evento);

        Assert.NotNull(email);
        Assert.Contains("não informado", email.Corpo);
    }

    [Fact]
    public void OrderConfirmed_ValorComMilhar_FormataEmReais()
    {
        var evento = new EventoNotificacao(
            "order.confirmed",
            new DadosEvento("Bia", "bia@exemplo.com", OrderId: "PED-2002", Total: 1234.5m));

        var email = TemplateEmail.Montar(evento);

        Assert.NotNull(email);
        Assert.Contains("1.234,50", email.Corpo);
    }
}