using ArandaGateway.Api.Application.Tickets;
using ArandaGateway.Api.Endpoints;
using ArandaGateway.Api.Identity;
using ArandaGateway.Api.Integrations.Aranda;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddArandaIntegration(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCollaborator, HeaderCurrentCollaborator>();
builder.Services.AddScoped<ITicketService, TicketService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapTicketEndpoints();

app.Run();
