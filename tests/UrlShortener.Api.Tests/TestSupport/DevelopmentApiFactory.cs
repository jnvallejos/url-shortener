using Microsoft.AspNetCore.Hosting;

namespace UrlShortener.Api.Tests.TestSupport;

public sealed class DevelopmentApiFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Development");
    }
}
