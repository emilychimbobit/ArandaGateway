namespace ArandaGateway.Api.Contracts.Equipos;

/// <summary>
/// Representa la información visible de un equipo asignado.
/// Restringido estrictamente a: tipo, modelo, código y estado.
/// </summary>
public record EquipoResponse(
    string Tipo,
    string Modelo,
    string Codigo,
    string Estado
);
