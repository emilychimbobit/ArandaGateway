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
                Projects = [new ArandaCiProjectFilter(arandaOptions.ProjectId)],
                UserId = user.Id
            };

            var response = await arandaClient.GetCisByUserAndProjectsAsync(request, cancellationToken);

            if (response.Content is null || response.Content.Count == 0)
            {
                return EquipoOperationResult<IReadOnlyList<EquipoResponse>>.NoRecordsFound();
            }

            var equipos = response.Content.Select(ci => new EquipoResponse(
                Display(ci.CategoryName, "Sin tipo"),
                Display(ci.Name, "Sin modelo"),
                ResolveCodigo(ci),
                Display(ci.StateName, "Sin estado")
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

    private static string Display(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    /// <summary>
    /// El CI no expone un campo de código; Aranda lo incluye al final del
    /// nombre, después de la categoría (por ejemplo
    /// "LAPTOP MARCOBRE PF47GX2H" con categoría "LAPTOP MARCOBRE").
    /// </summary>
    private static string ResolveCodigo(ArandaCiItem ci)
    {
        var name = ci.Name?.Trim();
        var categoria = ci.CategoryName?.Trim();

        if (!string.IsNullOrEmpty(name) &&
            !string.IsNullOrEmpty(categoria) &&
            name.StartsWith(categoria, StringComparison.OrdinalIgnoreCase))
        {
            var codigo = name[categoria.Length..].Trim();
            if (codigo.Length > 0)
            {
                return codigo;
            }
        }

        return string.IsNullOrWhiteSpace(ci.LicenseNumber)
            ? ci.Id.ToString()
            : ci.LicenseNumber.Trim();
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