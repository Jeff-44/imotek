using System.Text.Json.Serialization;
using Imotek.API.Configuration;
using Imotek.API.Endpoints;
using Imotek.API.Services;
using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Infrastructure;
using Imotek.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainValidationExceptionHandler>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.AddApiSecurity();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<PropertyService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseApiSecurity();

app.MapHealthEndpoints();
app.MapControllers();

await app.Services.SeedAdminAsync(app.Configuration, app.Logger);

app.Run();

// Exposes the entry point to WebApplicationFactory in Imotek.Tests.
public partial class Program;
