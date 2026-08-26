using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Integrations.Aranda;

public static class ArandaServiceCollectionExtensions
{
    public static IServiceCollection AddArandaIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ArandaOptions>()
            .Bind(configuration.GetSection(ArandaOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.BaseUrl.IsAbsoluteUri,
                "Aranda:BaseUrl must be an absolute URI.")
            .ValidateOnStart();

        services
            .AddHttpClient<IArandaClient, ArandaClient>((serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<ArandaOptions>>()
                    .Value;

                client.BaseAddress = options.BaseUrl;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "X-Authorization",
                    options.ApiKey);
            });

        return services;
    }
}
