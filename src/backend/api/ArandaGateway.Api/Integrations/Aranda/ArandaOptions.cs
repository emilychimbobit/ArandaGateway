using System.ComponentModel.DataAnnotations;

namespace ArandaGateway.Api.Integrations.Aranda;

public sealed class ArandaOptions
{
    public const string SectionName = "Aranda";

    [Required]
    public required Uri BaseUrl { get; init; }

    [Required]
    public required string ApiKey { get; init; }

    /// <summary>
    /// Clave de suscripción de Azure API Management, enviada en
    /// <c>Ocp-Apim-Subscription-Key</c>. Solo es necesaria cuando
    /// <see cref="BaseUrl"/> apunta a APIM y no directo a Aranda; sin ella
    /// APIM responde 401 antes de llegar a Aranda.
    /// </summary>
    public string? SubscriptionKey { get; init; }

    /// <summary>
    /// Cookie de sesión de Aranda (<c>AuthCookieASMS=...</c>), enviada tal cual
    /// en el encabezado <c>Cookie</c>. Aranda exige la cookie además del token
    /// de <see cref="ApiKey"/>: sin ella responde 401 aunque el token sea
    /// válido. Es una credencial de sesión y caduca, así que se configura por
    /// secreto o variable de entorno, nunca en appsettings.json.
    /// </summary>
    public string? AuthCookie { get; init; }

    /// <summary>
    /// Cada cuántos minutos se toca la sesión de Aranda para que no caduque
    /// por inactividad. Debe quedar holgadamente por debajo del tiempo de
    /// expiración de Aranda: se observó una sesión muerta tras unos 10 minutos
    /// sin tráfico, de ahí el valor predeterminado de 5. Con <c>0</c> se
    /// desactiva el latido.
    /// </summary>
    [Range(0, 60)]
    public int SessionKeepAliveMinutes { get; init; } = 5;

    [Range(1, long.MaxValue)]
    public long ProjectId { get; init; }

    [Range(1, long.MaxValue)]
    public long AuthorId { get; init; }

    public long? CategoryId { get; init; }

    public long? ServiceId { get; init; }

    public long? ImpactId { get; init; }

    public long? UrgencyId { get; init; }

    public long? GroupId { get; init; }

    public long? RegistryTypeId { get; init; }

    public long? UnitId { get; init; }

    public long? IncidentModelId { get; init; }

    public long? IncidentInitialStateId { get; init; }

    public long? IncidentCancellationStateId { get; init; }

    public long? ServiceRequestModelId { get; init; }

    public long? ServiceRequestInitialStateId { get; init; }

    public long? ServiceRequestCancellationStateId { get; init; }

    [Range(1, 100)]
    public int SearchPageSize { get; init; } = 50;

    [Range(1, 3_145_728)]
    public long MaxAttachmentBytes { get; init; } = 3_145_728;

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// PARCHE TEMPORAL: usuario fijo por dominio mientras la API de usuarios
    /// de Aranda no esté disponible. Ver <see cref="ArandaUserOverrideOptions"/>.
    /// Con <c>Enabled = false</c> o sin la sección, el gateway vuelve a
    /// resolver el colaborador contra Aranda.
    /// </summary>
    public ArandaUserOverrideOptions? UserOverride { get; init; }
}
