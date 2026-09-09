using System.Net;
using ArandaGateway.Api.Contracts.Equipos;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Integrations.Aranda.Models;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Application.Equipos;

public sealed class EquipoService(
    ICurrentCollaborator currentCollaborator,
    IArandaClient arandaClient,
    IOptions<ArandaOptions> options,
    ILogger<EquipoService> logger) : IEquipoService
{
    private readonly ArandaOptions arandaOptions = options.Value;

    public async Task<EquipoOperationResult<IReadOnlyList<EquipoResponse>>> ListAssignedEquiposAsync(
        CancellationToken cancellationToken = default)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return EquipoOperationResult<IReadOnlyList<EquipoResponse>>.MissingCollaborator();
        }

        logger.LogInformation("REQ_10: Consultando inventario CMDB para colaborador {Username}.", username);

        try
        {
            var user = await ResolveActiveUserAsync(username, cancellationToken);
            if (user is null)
            {
                return EquipoOperationResult<IReadOnlyList<EquipoResponse>>.MissingCollaborator();
            }

            var request = new ArandaCiRequest
            {
                Projects = [new ArandaProjectFilter(arandaOptions.ProjectId)],
                UserId = user.Id
            };

            var response = await arandaClient.GetCisByUserAndProjectsAsync(request, cancellationToken);

            if (response.Content is null || response.Content.Count == 0)
            {
                return EquipoOperationResult<IReadOnlyList<EquipoResponse>>.NoRecordsFound();
            }

            var equipos = response.Content.Select(ci => new EquipoResponse(
                ci.CiTypeName ?? "Dispositivo",
                ci.ModelName ?? ci.Name ?? "Sin modelo",
                ci.Code ?? ci.Id.ToString(),
                ci.StateName ?? "Asignado"
            )).ToArray();

            return EquipoOperationResult<IReadOnlyList<EquipoResponse>>.Success(equipos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "REQ_10: Error al consultar fuente de inventario CMDB.");
            return EquipoOperationResult<IReadOnlyList<EquipoResponse>>.SourceUnavailable(
                "La fuente de inventario/CMDB no está disponible, se informa la imposibilidad de completar la consulta.");
        }
    }

    private async Task<ArandaUser?> ResolveActiveUserAsync(
        string username,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await arandaClient.GetUserByUsernameAsync(username, cancellationToken);
            return user.IsActive ? user : null;
        }
        catch (ArandaApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}