using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

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

                client.BaseAddress = new Uri(
                    $"{options.BaseUrl.AbsoluteUri.TrimEnd('/')}/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "ArandaGateway/1.0");
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue(
                        "application/json"));
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "X-Authorization",
                    options.ApiKey);
            });

        return services;
    }
}
