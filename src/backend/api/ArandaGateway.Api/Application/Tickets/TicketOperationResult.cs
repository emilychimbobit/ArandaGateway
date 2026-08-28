namespace ArandaGateway.Api.Application.Tickets;

public sealed record TicketOperationResult<T>(
    TicketOperationResultStatus Status,
    T? Value = default,
    string? Error = null);

public enum TicketOperationResultStatus
{
    Success,
    MissingCollaborator,
    InvalidRequest,
    NotFoundOrNotOwned,
    InvalidState,
    ConfigurationMissing
}
