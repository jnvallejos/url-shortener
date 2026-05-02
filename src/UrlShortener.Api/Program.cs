using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Contracts;
using UrlShortener.Api.Endpoints;
using UrlShortener.Application.DependencyInjection;
using UrlShortener.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Missing 'ConnectionStrings:DefaultConnection' in configuration.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "URL Shortener API",
            Version = "v1",
            Description = "Reference URL shortener built with Clean Architecture and TDD.",
            License = new OpenApiLicense { Name = "MIT" }
        };
        return Task.CompletedTask;
    });
});

builder.Services.Configure<RateLimitingOptions>(
    builder.Configuration.GetSection("RateLimiting"));

builder.Services.AddRateLimiter(options =>
{
    var rateLimitConfig = builder.Configuration
        .GetSection("RateLimiting")
        .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

    options.AddPolicy("redirect", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitConfig.PermitLimit,
                Window = rateLimitConfig.Window,
                QueueLimit = rateLimitConfig.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(
                "RateLimit.Exceeded",
                "Too many requests; try again later",
                context.HttpContext.TraceIdentifier),
            ct);
    };
});

var app = builder.Build();

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("URL Shortener API");
        options.WithTheme(ScalarTheme.Default);
    });
}

app.MapShortUrlsEndpoints();
app.MapRedirectEndpoint();

app.Run();

public partial class Program;
