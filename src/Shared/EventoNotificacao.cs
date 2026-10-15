namespace Shared;

public record DadosEvento(
    string Name,
    string Email,
    string? OrderId = null,
    decimal? Total = null);

public record EventoNotificacao(string Type, DadosEvento Data, string? EventId = null);