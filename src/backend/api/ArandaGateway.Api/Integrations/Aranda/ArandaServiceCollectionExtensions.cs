using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace ArandaGateway.Api.Integrations.Aranda;

public static class ArandaServiceCollectionExtensions
{
    public const string SubscriptionKeyHeaderName = "Ocp-Apim-Subscription-Key";

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

                // Cuando la salida va por Azure API Management y no directo a
                // Aranda, APIM exige su propia clave de suscripción y responde
                // 401 sin ella. Va aparte de X-Authorization, que es la
                // credencial de Aranda.
                if (!string.IsNullOrWhiteSpace(options.SubscriptionKey))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(
                        SubscriptionKeyHeaderName,
                        options.SubscriptionKey);
                }

                // Aranda exige la cookie de sesión ademas del token de
                // X-Authorization; sin ella responde 401 aunque el token sea
                // valido.
                if (!string.IsNullOrWhiteSpace(options.AuthCookie))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(
                        "Cookie",
                        options.AuthCookie);
                }
            })
            // El manejo automatico de cookies pisaria el encabezado Cookie que
            // se fija arriba; la sesion de Aranda se envia explicitamente.
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { UseCookies = false });

        return services;
    }
}
