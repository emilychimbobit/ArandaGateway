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
    /// Interruptor de la sesión por cookie. En <c>false</c> el gateway no envía
    /// <see cref="AuthCookie"/>, no adopta las cookies que devuelve Aranda y no
    /// ejecuta el latido: opera solo con el token de <see cref="ApiKey"/>.
    ///
    /// <para>
    /// Se apagó el 15 de septiembre de 2026 tras comprobar que Aranda responde
    /// <c>200</c> sin cookie, tanto directo como por APIM, y que una cookie
    /// caducada provoca <c>401</c> en peticiones que sin ella funcionan.
    /// </para>
    ///
    /// <para>
    /// Si Aranda vuelve a exigir sesión no hace falta redesplegar: basta
    /// instalar una cookie con <c>PUT /admin/aranda-session</c>, que reactiva
    /// el envío en caliente.
    /// </para>
    /// </summary>
    public bool SessionCookieEnabled { get; init; } = true;

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

    /// <summary>
    /// Nombres de la clasificación fija que acompañan a
    /// <see cref="ServiceId"/>, <see cref="ImpactId"/>,
    /// <see cref="UrgencyId"/>, <see cref="CategoryId"/> y
    /// <see cref="GroupId"/>. Ver <see cref="ArandaClassificationOptions"/>.
    /// </summary>
    public ArandaClassificationOptions Classification { get; init; } = new();

    public long? RegistryTypeId { get; init; }

    public long? UnitId { get; init; }

    public long? IncidentModelId { get; init; }

    public long? IncidentInitialStateId { get; init; }

    public long? IncidentCancellationStateId { get; init; }

    public long? ServiceRequestModelId { get; init; }

    public long? ServiceRequestInitialStateId { get; init; }

    public long? ServiceRequestCancellationStateId { get; init; }

    /// <summary>
    /// Prefijo que se antepone al asunto de todo ticket creado por el gateway,
    /// por ejemplo <c>[PRUEBA BOT]</c>, para que Mesa de Ayuda distinga los
    /// casos de prueba de los reales. Es el interruptor de la marca: en null o
    /// vacío el asunto va tal cual lo escribió el colaborador. Si el asunto ya
    /// empieza con el prefijo no se duplica.
    /// </summary>
    public string? SubjectPrefix { get; init; }

    [Range(1, 100)]
    public int SearchPageSize { get; init; } = 50;

    [Range(1, 3_145_728)]
    public long MaxAttachmentBytes { get; init; } = 3_145_728;

    /// <summary>
    /// Tope de la descripción de un ticket, medida ya codificada. No es un
    /// límite de Aranda: acepta 100 000 caracteres y los devuelve intactos
    /// (medido el 16 de septiembre de 2026). Es política del gateway, porque
    /// <c>POST /api/tickets</c> está publicado en APIM y sin tope aceptaría un
    /// cuerpo de cualquier tamaño. El valor de omisión deja más de diez veces
    /// el máximo del formulario, que son 1500 caracteres.
    /// A diferencia del asunto —400, límite real de Aranda y fijo en
    /// <c>TicketService</c>— este se configura.
    /// </summary>
    [Range(1, 100_000)]
    public int MaxDescriptionLength { get; init; } = 20_000;

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 30;
}
