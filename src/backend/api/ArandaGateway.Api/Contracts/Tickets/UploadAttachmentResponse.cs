namespace ArandaGateway.Api.Contracts.Tickets;

public sealed record RespuestaAdjuntarArchivo(
    string FileName,
    bool Uploaded);
