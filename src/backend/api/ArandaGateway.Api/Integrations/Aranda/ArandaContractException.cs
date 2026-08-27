namespace ArandaGateway.Api.Integrations.Aranda;

public sealed class ArandaContractException(string message)
    : Exception(message);
