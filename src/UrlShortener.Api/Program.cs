using UrlShortener.Api.Endpoints;
using UrlShortener.Application.DependencyInjection;
using UrlShortener.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Missing 'ConnectionStrings:DefaultConnection' in configuration.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

app.MapShortUrlsEndpoints();

app.Run();

public partial class Program;
