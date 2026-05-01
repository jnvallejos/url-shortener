using FluentAssertions;
using UrlShortener.Domain.ShortUrls;
using UrlShortener.Infrastructure.Persistence.Repositories;
using UrlShortener.Infrastructure.Tests.TestSupport;

namespace UrlShortener.Infrastructure.Tests.Persistence;

public class EfShortUrlRepositoryTests
{
    private const string ValidCode = "abc1234";
    private const string ValidUrl = "https://example.com/path";

    private static ShortUrl ActiveShortUrl(string code = ValidCode) =>
        ShortUrl.Create(
            ShortCode.Create(code),
            OriginalUrl.Create(ValidUrl));

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsShortUrl()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new EfShortUrlRepository(ctx);
            var fetched = await sut.GetByIdAsync(entity.Id, CancellationToken.None);

            fetched.Should().NotBeNull();
            fetched!.Id.Should().Be(entity.Id);
        }
    }

    [Fact]
    public async Task GetByIdAsync_WithMissingId_ReturnsNull()
    {
        using var fixture = new SqliteInMemoryFixture();

        await using var ctx = fixture.CreateContext();
        var sut = new EfShortUrlRepository(ctx);

        var fetched = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        fetched.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_WithExistingCode_ReturnsShortUrl()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new EfShortUrlRepository(ctx);
            var fetched = await sut.GetByCodeAsync(ShortCode.Create(ValidCode), CancellationToken.None);

            fetched.Should().NotBeNull();
            fetched!.ShortCode.Should().Be(entity.ShortCode);
        }
    }

    [Fact]
    public async Task GetByCodeAsync_WithMissingCode_ReturnsNull()
    {
        using var fixture = new SqliteInMemoryFixture();

        await using var ctx = fixture.CreateContext();
        var sut = new EfShortUrlRepository(ctx);

        var fetched = await sut.GetByCodeAsync(ShortCode.Create("missing0"[..7]), CancellationToken.None);

        fetched.Should().BeNull();
    }

    [Fact]
    public async Task ExistsByCodeAsync_WhenExists_ReturnsTrue()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new EfShortUrlRepository(ctx);
            var exists = await sut.ExistsByCodeAsync(ShortCode.Create(ValidCode), CancellationToken.None);

            exists.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExistsByCodeAsync_WhenMissing_ReturnsFalse()
    {
        using var fixture = new SqliteInMemoryFixture();

        await using var ctx = fixture.CreateContext();
        var sut = new EfShortUrlRepository(ctx);

        var exists = await sut.ExistsByCodeAsync(ShortCode.Create(ValidCode), CancellationToken.None);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_AfterSaveChanges_PersistsTheEntity()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new EfShortUrlRepository(ctx);
            await sut.AddAsync(entity, CancellationToken.None);
            await sut.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ShortUrls.FindAsync(entity.Id);
            fetched.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task UpdateAsync_OnTrackedEntity_DoesNotThrow()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new EfShortUrlRepository(ctx);
            var loaded = await sut.GetByCodeAsync(ShortCode.Create(ValidCode), CancellationToken.None);

            Func<Task> act = () => sut.UpdateAsync(loaded!, CancellationToken.None);

            await act.Should().NotThrowAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_AfterMutation_PersistsChangesOnSave()
    {
        using var fixture = new SqliteInMemoryFixture();
        var entity = ActiveShortUrl();

        await using (var ctx = fixture.CreateContext())
        {
            ctx.ShortUrls.Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var sut = new EfShortUrlRepository(ctx);
            var loaded = await sut.GetByCodeAsync(ShortCode.Create(ValidCode), CancellationToken.None);
            loaded!.Disable();
            await sut.UpdateAsync(loaded, CancellationToken.None);
            await sut.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = fixture.CreateContext())
        {
            var fetched = await ctx.ShortUrls.FindAsync(entity.Id);
            fetched.Should().NotBeNull();
            fetched!.IsEnabled.Should().BeFalse();
        }
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoChanges_ReturnsZero()
    {
        using var fixture = new SqliteInMemoryFixture();

        await using var ctx = fixture.CreateContext();
        var sut = new EfShortUrlRepository(ctx);

        await sut.SaveChangesAsync(CancellationToken.None);

        ctx.ChangeTracker.Entries().Should().BeEmpty();
    }
}
