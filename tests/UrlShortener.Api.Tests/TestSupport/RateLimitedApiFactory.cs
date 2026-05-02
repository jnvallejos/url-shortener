using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace UrlShortener.Api.Tests.TestSupport;

public sealed class RateLimitedApiFactory : ApiWebApplicationFactory
{
    public int PermitLimit { get; init; } = 3;
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(2);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = PermitLimit.ToString(),
                ["RateLimiting:Window"]      = Window.ToString(),
                ["RateLimiting:QueueLimit"]  = "0"
            });
        });
        base.ConfigureWebHost(builder);
    }
}
