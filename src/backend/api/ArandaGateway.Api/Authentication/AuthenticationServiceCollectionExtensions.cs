using Microsoft.AspNetCore.Authentication;

namespace ArandaGateway.Api.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GatewayOptions>()
            .Bind(configuration.GetSection(GatewayOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<
                AuthenticationSchemeOptions,
                ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                _ => { });
        services.AddAuthorization();

        return services;
    }
}
