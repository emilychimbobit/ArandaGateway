using ArandaGateway.Api.Application.Tickets;
using ArandaGateway.Api.Endpoints;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddArandaIntegration(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCollaborator, HeaderCurrentCollaborator>();
builder.Services.AddScoped<ITicketService, TicketService>();

var app = builder.Build();

app.MapTicketEndpoints();

app.Run();
