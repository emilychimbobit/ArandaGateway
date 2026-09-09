namespace ArandaGateway.Api.Application.Equipos;

public enum EquipoOperationResultStatus
{
    Success,
    MissingCollaborator,
    NoRecordsFound,
    SourceUnavailable,
    ConfigurationMissing
}

public record EquipoOperationResult<T>(
    EquipoOperationResultStatus Status,
    T? Value = default,
    string? Error = null)
{
    public static EquipoOperationResult<T> Success(T value) =>
        new(EquipoOperationResultStatus.Success, Value: value);

    public static EquipoOperationResult<T> MissingCollaborator() =>
        new(EquipoOperationResultStatus.MissingCollaborator, Error: "Falta la identidad del colaborador.");

    public static EquipoOperationResult<T> NoRecordsFound() =>
        new(EquipoOperationResultStatus.NoRecordsFound, Error: "Sin registros asociados.");

    public static EquipoOperationResult<T> SourceUnavailable(string error) =>
        new(EquipoOperationResultStatus.SourceUnavailable, Error: error);
}