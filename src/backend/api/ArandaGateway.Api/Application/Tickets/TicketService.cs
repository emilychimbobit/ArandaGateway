using System.Net;
using System.Text.Encodings.Web;
using ArandaGateway.Api.Contracts.Tickets;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Integrations.Aranda.Models;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Application.Tickets;

public sealed class TicketService(
    ICurrentCollaborator currentCollaborator,
    IArandaClient arandaClient,
    IOptions<ArandaOptions> options) : ITicketService
{
    private static readonly HashSet<string> AllowedCancellationStates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Registrado",
            "Asignado",
            "Registrado/Asignado",
            "En proceso"
        };

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx",
            ".docx",
            ".ppt",
            ".pdf",
            ".png",
            ".jpg"
        };

    private readonly ArandaOptions arandaOptions = options.Value;

    public async Task<TicketOperationResult<CreateTicketResponse>>
        CreateTicketAsync(
            CreateTicketRequest request,
            CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return MissingCollaborator<CreateTicketResponse>();
        }

        if (string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            return Invalid<CreateTicketResponse>(
                "El asunto y la descripción son obligatorios.");
        }

        if (!TryGetTypeConfiguration(request.Type, out var configuration))
        {
            return ConfigurationMissing<CreateTicketResponse>();
        }

        var user = await ResolveActiveUserAsync(
            username,
            cancellationToken);
        if (user is null)
        {
            return NotFoundOrNotOwned<CreateTicketResponse>();
        }

        var created = await arandaClient.CreateTicketAsync(
            new ArandaCreateTicketRequest
            {
                CategoryId = configuration.CategoryId,
                CustomerId = user.Id,
                ApplicantId = user.Id,
                Description = HtmlEncoder.Default.Encode(
                    request.Description.Trim()),
                ItemType = configuration.ItemType,
                ImpactId = configuration.ImpactId,
                UrgencyId = configuration.UrgencyId,
                ModelId = configuration.ModelId,
                ProjectId = arandaOptions.ProjectId,
                RegistryTypeId = configuration.RegistryTypeId,
                UnitId = configuration.UnitId,
                ServiceId = configuration.ServiceId,
                StateId = configuration.InitialStateId,
                AuthorId = arandaOptions.AuthorId,
                GroupId = configuration.GroupId,
                Subject = HtmlEncoder.Default.Encode(
                    request.Subject.Trim())
            },
            cancellationToken);

        return Success(
            new CreateTicketResponse(created.IdByProject, "Creado"));
    }

    public async Task<
        TicketOperationResult<IReadOnlyList<TicketSummaryResponse>>>
        ListOpenTicketsAsync(CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return MissingCollaborator<
                IReadOnlyList<TicketSummaryResponse>>();
        }

        var user = await ResolveActiveUserAsync(
            username,
            cancellationToken);
        if (user is null)
        {
            return NotFoundOrNotOwned<
                IReadOnlyList<TicketSummaryResponse>>();
        }

        var search = await arandaClient.SearchTicketsAsync(
            BuildCollaboratorTicketsSearch(user.Id),
            cancellationToken);

        var tickets = search.Content
            .Where(ticket =>
                (ticket.CustomerId is null ||
                    ticket.CustomerId == user.Id) &&
                !ticket.IsClosed &&
                ticket.IdByProject is not null &&
                ticket.Subject is not null &&
                ticket.StateName is not null &&
                ticket.OpenedDate is not null)
            .Select(ticket => new TicketSummaryResponse(
                ticket.IdByProject!,
                ticket.Subject!,
                ticket.StateName!,
                DateTimeOffset.FromUnixTimeMilliseconds(
                    ticket.OpenedDate!.Value)))
            .ToArray();

        return Success<IReadOnlyList<TicketSummaryResponse>>(tickets);
    }

    public async Task<TicketDetailResult> GetTicketDetailAsync(
        string caseNumber,
        CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return new(TicketDetailResultStatus.MissingCollaborator);
        }

        var ticket = await ResolveOwnedTicketAsync(
            caseNumber,
            username,
            cancellationToken);

        if (ticket is null)
        {
            return new(TicketDetailResultStatus.NotFoundOrNotOwned);
        }

        if (ticket.IdByProject is null ||
            ticket.StateName is null ||
            ticket.ModifiedDate is null)
        {
            throw new ArandaContractException(
                "Aranda returned an incomplete ticket.");
        }

        return new(
            TicketDetailResultStatus.Success,
            new TicketDetailResponse(
                ticket.IdByProject,
                ticket.StateName,
                ticket.GroupName,
                DateTimeOffset.FromUnixTimeMilliseconds(
                    ticket.ModifiedDate.Value),
                null));
    }

    public async Task<TicketOperationResult<CancelTicketResponse>>
        CancelTicketAsync(
            string caseNumber,
            CancelTicketRequest request,
            CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return MissingCollaborator<CancelTicketResponse>();
        }

        if (!request.Confirmed)
        {
            return Invalid<CancelTicketResponse>(
                "La anulación requiere confirmación explícita.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Invalid<CancelTicketResponse>(
                "El motivo de anulación es obligatorio.");
        }

        var ticket = await ResolveOwnedTicketAsync(
            caseNumber,
            username,
            cancellationToken);
        if (ticket is null)
        {
            return NotFoundOrNotOwned<CancelTicketResponse>();
        }

        if (ticket.StateName is null ||
            !AllowedCancellationStates.Contains(ticket.StateName))
        {
            return new(
                TicketOperationResultStatus.InvalidState,
                Error:
                    "El ticket no se encuentra en un estado anulable.");
        }

        var cancellationStateId = ticket.ItemType switch
        {
            1 => arandaOptions.IncidentCancellationStateId,
            4 => arandaOptions.ServiceRequestCancellationStateId,
            _ => null
        };
        if (!IsPositive(cancellationStateId))
        {
            return ConfigurationMissing<CancelTicketResponse>();
        }

        var registryTypeId = IsPositive(ticket.RegistryTypeId)
            ? ticket.RegistryTypeId
            : arandaOptions.RegistryTypeId;
        if (!IsPositive(registryTypeId))
        {
            return ConfigurationMissing<CancelTicketResponse>();
        }

        var update = await arandaClient.UpdateTicketAsync(
            ticket.Id,
            new ArandaUpdateTicketRequest
            {
                CategoryId = ticket.CategoryId,
                ItemType = ticket.ItemType,
                ItemVersion = ticket.ItemVersion,
                ModelId = ticket.ModelId,
                ProjectId = ticket.ProjectId,
                RegistryTypeId = registryTypeId!.Value,
                ServiceId = ticket.ServiceId,
                StateId = cancellationStateId!.Value,
                Commentary = HtmlEncoder.Default.Encode(
                    request.Reason.Trim())
            },
            cancellationToken);

        if (!update.Result)
        {
            throw new ArandaContractException(
                "Aranda did not confirm the ticket cancellation.");
        }

        return Success(
            new CancelTicketResponse(
                ticket.IdByProject ?? caseNumber.Trim(),
                "Anulado"));
    }

    public async Task<TicketOperationResult<UploadAttachmentResponse>>
        UploadAttachmentAsync(
            string caseNumber,
            TicketAttachment attachment,
            CancellationToken cancellationToken)
    {
        if (currentCollaborator.Username is not { } username)
        {
            return MissingCollaborator<UploadAttachmentResponse>();
        }

        var fileName = Path.GetFileName(attachment.FileName);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Any(char.IsControl) ||
            !AllowedExtensions.Contains(extension))
        {
            return Invalid<UploadAttachmentResponse>(
                "El formato del archivo no está permitido.");
        }

        if (attachment.Length is <= 0 ||
            attachment.Length > arandaOptions.MaxAttachmentBytes)
        {
            return Invalid<UploadAttachmentResponse>(
                "El archivo supera el límite configurado.");
        }

        var ticket = await ResolveOwnedTicketAsync(
            caseNumber,
            username,
            cancellationToken);
        if (ticket is null)
        {
            return NotFoundOrNotOwned<UploadAttachmentResponse>();
        }

        var uploadResults = await arandaClient.UploadAttachmentAsync(
            new ArandaAttachmentUpload(
                ticket.Id,
                ticket.ItemType,
                fileName,
                attachment.ContentType,
                attachment.Content,
                attachment.Description),
            cancellationToken);

        var result = uploadResults.SingleOrDefault();
        if (result is null || !result.Result)
        {
            throw new ArandaContractException(
                "Aranda did not confirm the file upload.");
        }

        return Success(
            new UploadAttachmentResponse(result.FileName, true));
    }

    private async Task<ArandaUser?> ResolveActiveUserAsync(
        string username,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await arandaClient.GetUserByUsernameAsync(
                username,
                cancellationToken);
            return user.IsActive ? user : null;
        }
        catch (ArandaApiException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private ArandaSearchTicketsRequest BuildCollaboratorTicketsSearch(
        long customerId) =>
        new()
        {
            Criteria =
            [
                new ArandaSearchCriterion
                {
                    FieldName = "customerId",
                    FieldValue = "customerId",
                    OperatorName = "eq",
                    OperatorValue = "==",
                    Value = customerId,
                    ValueName = customerId,
                    Type = 6
                }
            ],
            PageIndex = 0,
            PageSize = arandaOptions.SearchPageSize,
            Projects =
            [
                new ArandaProjectFilter(arandaOptions.ProjectId)
            ],
            Repository = 3,
            Types =
            [
                new ArandaItemTypeFilter(1),
                new ArandaItemTypeFilter(2),
                new ArandaItemTypeFilter(3),
                new ArandaItemTypeFilter(4)
            ]
        };

    // El consumidor solo conoce el número de caso (idByProject). Aranda
    // consulta el detalle por su identificador interno, así que el caso se
    // resuelve dentro de los tickets del propio colaborador: eso traduce el
    // identificador y confirma la propiedad en un solo paso.
    private async Task<ArandaTicket?> ResolveOwnedTicketAsync(
        string caseNumber,
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caseNumber))
        {
            return null;
        }

        var user = await ResolveActiveUserAsync(
            username,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        var search = await arandaClient.SearchTicketsAsync(
            BuildCollaboratorTicketsSearch(user.Id),
            cancellationToken);

        var match = search.Content.FirstOrDefault(ticket =>
            string.Equals(
                ticket.IdByProject?.Trim(),
                caseNumber.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return null;
        }

        return await GetOwnedTicketOrNullAsync(
            match.Id,
            username,
            cancellationToken);
    }

    private async Task<ArandaTicket?> GetOwnedTicketOrNullAsync(
        long caseNumber,
        string username,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetOwnedTicketAsync(
                caseNumber,
                username,
                cancellationToken);
        }
        catch (ArandaApiException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<ArandaTicket?> GetOwnedTicketAsync(
        long caseNumber,
        string username,
        CancellationToken cancellationToken)
    {
        var ticket = await arandaClient.GetTicketAsync(
            caseNumber,
            cancellationToken);

        return string.Equals(
            ticket.CustomerUserName,
            username,
            StringComparison.OrdinalIgnoreCase)
            ? ticket
            : null;
    }

    private bool TryGetTypeConfiguration(
        TicketKind type,
        out TicketTypeConfiguration configuration)
    {
        var modelId = type switch
        {
            TicketKind.Incident => arandaOptions.IncidentModelId,
            TicketKind.ServiceRequest =>
                arandaOptions.ServiceRequestModelId,
            _ => null
        };
        var initialStateId = type switch
        {
            TicketKind.Incident =>
                arandaOptions.IncidentInitialStateId,
            TicketKind.ServiceRequest =>
                arandaOptions.ServiceRequestInitialStateId,
            _ => null
        };
        var itemType = type switch
        {
            TicketKind.Incident => 1,
            TicketKind.ServiceRequest => 4,
            _ => 0
        };

        if (itemType == 0 ||
            !IsPositive(modelId) ||
            !IsPositive(initialStateId) ||
            !IsPositive(arandaOptions.CategoryId) ||
            !IsPositive(arandaOptions.ServiceId) ||
            !IsPositive(arandaOptions.ImpactId) ||
            !IsPositive(arandaOptions.UrgencyId) ||
            !IsPositive(arandaOptions.GroupId) ||
            !IsPositive(arandaOptions.RegistryTypeId) ||
            !IsPositive(arandaOptions.UnitId))
        {
            configuration = default;
            return false;
        }

        configuration = new(
            itemType,
            modelId!.Value,
            initialStateId!.Value,
            arandaOptions.CategoryId!.Value,
            arandaOptions.ServiceId!.Value,
            arandaOptions.ImpactId!.Value,
            arandaOptions.UrgencyId!.Value,
            arandaOptions.GroupId!.Value,
            arandaOptions.RegistryTypeId!.Value,
            arandaOptions.UnitId!.Value);
        return true;
    }

    private static bool IsPositive(long? value) => value is > 0;

    private static TicketOperationResult<T> Success<T>(T value) =>
        new(TicketOperationResultStatus.Success, value);

    private static TicketOperationResult<T> MissingCollaborator<T>() =>
        new(
            TicketOperationResultStatus.MissingCollaborator,
            Error: "No se pudo identificar al colaborador.");

    private static TicketOperationResult<T> Invalid<T>(string error) =>
        new(TicketOperationResultStatus.InvalidRequest, Error: error);

    private static TicketOperationResult<T> NotFoundOrNotOwned<T>() =>
        new(TicketOperationResultStatus.NotFoundOrNotOwned);

    private static TicketOperationResult<T> ConfigurationMissing<T>() =>
        new(
            TicketOperationResultStatus.ConfigurationMissing,
            Error:
                "La operación requiere configuración adicional de Aranda.");

    private readonly record struct TicketTypeConfiguration(
        long ItemType,
        long ModelId,
        long InitialStateId,
        long CategoryId,
        long ServiceId,
        long ImpactId,
        long UrgencyId,
        long GroupId,
        long RegistryTypeId,
        long UnitId);
}
