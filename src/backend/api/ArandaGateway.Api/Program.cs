using System.Text.Json.Serialization;
using ArandaGateway.Api.Administration;
using ArandaGateway.Api.Application.Equipos;
using ArandaGateway.Api.Application.Tickets;
using ArandaGateway.Api.Endpoints;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GatewayExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter()));
builder.Services
    .AddOptions<AdminOptions>()
    .Bind(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddArandaIntegration(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCollaborator, HeaderCurrentCollaborator>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IEquipoService, EquipoService>();

var app = builder.Build();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapTicketEndpoints();
app.MapEquiposEndpoints();
app.MapAdminEndpoints();
app.MapGet("/health", () => TypedResults.Ok(new { status = "Healthy" }))
    .WithName("Health")
    .WithTags("Health");

app.Run();

public partial class Program;
