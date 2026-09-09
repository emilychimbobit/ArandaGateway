using ArandaGateway.Api.Contracts.Equipos;

namespace ArandaGateway.Api.Application.Equipos;

public interface IEquipoService
{
    Task<EquipoOperationResult<IReadOnlyList<EquipoResponse>>> ListAssignedEquiposAsync(
        CancellationToken cancellationToken = default);
}