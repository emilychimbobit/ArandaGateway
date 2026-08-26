namespace ArandaGateway.Api.Integrations.Aranda.Models;

public sealed record ArandaSearchTicketsRequest
{
    public required IReadOnlyList<ArandaSearchCriterion> Criteria { get; init; }

    public int Level { get; init; }

    public string OrderField { get; init; } = "openedDate";

    public string OrderType { get; init; } = "desc";

    public int PageIndex { get; init; }

    public int PageSize { get; init; } = 50;

    public required IReadOnlyList<ArandaProjectFilter> Projects { get; init; }

    public int Repository { get; init; } = 1;

    public required IReadOnlyList<ArandaItemTypeFilter> Types { get; init; }

    public bool Validate { get; init; } = true;

    public int ViewId { get; init; } = -6;
}

public sealed record ArandaSearchCriterion(
    string FieldName,
    string OperatorName,
    string OperatorValue,
    object Value);

public sealed record ArandaProjectFilter(long Project);

public sealed record ArandaItemTypeFilter(long ItemType);
