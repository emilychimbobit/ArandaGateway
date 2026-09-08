namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record SolicitudAnularTicket(
    string Reason,
    bool Confirmed);
