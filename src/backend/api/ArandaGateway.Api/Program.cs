using System.Text.Json.Serialization;
using ArandaGateway.Api.Application.Tickets;
using ArandaGateway.Api.Authentication;
using ArandaGateway.Api.Endpoints;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Observability;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        ApiKeyAuthenticationHandler.SchemeName,
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthenticationHandler.HeaderName,
            Description = "API key enviada por Azure API Management."
        });
    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecuritySchemeReference(
                    ApiKeyAuthenticationHandler.SchemeName,
                    document,
                    null)
            ] = []
        });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GatewayExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter()));
builder.Services.AddGatewayAuthentication(builder.Configuration);
builder.Services.AddArandaIntegration(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCollaborator, HeaderCurrentCollaborator>();
builder.Services.AddScoped<ITicketService, TicketService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapTicketEndpoints();
app.MapGet("/health", () => TypedResults.Ok(new { status = "Healthy" }))
    .WithName("Health")
    .WithTags("Health");

app.Run();

public partial class Program;
