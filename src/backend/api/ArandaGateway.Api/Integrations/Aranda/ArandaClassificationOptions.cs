using System.ComponentModel.DataAnnotations;

namespace ArandaGateway.Api.Integrations.Aranda;

/// <summary>
/// Nombres legibles de la clasificación fija del REQ_04, la que el DEF manda
/// aplicar a todo ticket creado por el bot. Los IDs técnicos viven en
/// <see cref="ArandaOptions"/>; acá van las etiquetas que el agente muestra en
/// el resumen de confirmación, para que no las tenga quemadas en el topic y se
/// desincronicen si alguien cambia la configuración del gateway.
///
/// <para>
/// Si se cambia un ID en <see cref="ArandaOptions"/> hay que mover su nombre
/// acá en el mismo despliegue: nada valida que un par (ID, nombre) coincida con
/// el catálogo de Aranda.
/// </para>
/// </summary>
public sealed class ArandaClassificationOptions
{
    [Required]
    public string Service { get; init; } = "Por categorizar";

    [Required]
    public string Impact { get; init; } = "Bajo";

    [Required]
    public string Urgency { get; init; } = "Bajo";

    [Required]
    public string Category { get; init; } = "Ticket creado por bot";

    [Required]
    public string Group { get; init; } = "Mesa de Ayuda";
}
