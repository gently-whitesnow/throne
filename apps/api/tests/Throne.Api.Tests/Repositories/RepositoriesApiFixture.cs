using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Api.Tests.Repositories;

/// <summary>
/// Boots <see cref="WebApplicationFactory{TEntryPoint}"/> with a Testcontainers
/// Mongo, a writable workspace under <see cref="System.IO.Path.GetTempPath"/>,
/// a stub <see cref="IGitProvider"/> + matching <see cref="IGitProviderRegistry"/>,
/// and the clone / PR-sync background hosts removed so binding-side effects
/// don't run during HTTP-level tests.
/// </summary>
internal sealed class RepositoriesApiFixture : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _workspaceRoot;

    public RepositoriesApiFixture(MongoFixture mongo, IGitProvider provider)
    {
        ArgumentNullException.ThrowIfNull(mongo);
        ArgumentNullException.ThrowIfNull(provider);

        Provider = provider;
        _workspaceRoot = Path.Combine(Path.GetTempPath(), $"throne-test-ws-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspaceRoot);

        var dbName = $"throne_api_{Guid.NewGuid():N}";
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            RepositoriesTestHostConfigurator.Configure(builder, mongo.ConnectionString, dbName, _workspaceRoot, provider));
        Client = _factory.CreateClient();
    }

    public IGitProvider Provider { get; }

    public HttpClient Client { get; }

    public IServiceProvider Services => _factory.Services;

    public string WorkspaceRoot => _workspaceRoot;

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        try
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // best-effort cleanup; CI agents wipe TMP between runs anyway
        }
    }
}

/// <summary>
/// Static helper extracted from <see cref="RepositoriesApiFixture"/> so the
/// fixture class itself stays under the CA1502 cyclomatic budget. Owns the
/// host-builder wiring + DI rewrites for the integration tests.
/// </summary>
internal static class RepositoriesTestHostConfigurator
{
    public static void Configure(
        IWebHostBuilder builder,
        string mongoConnection,
        string dbName,
        string workspaceRoot,
        IGitProvider provider)
    {
        builder.UseEnvironment("Production");
        builder.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = false;
            o.ValidateOnBuild = false;
        });
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = mongoConnection,
                ["Mongo:Database"] = dbName,
                ["Throne:Workspace:Root"] = workspaceRoot,
            });
        });
        builder.ConfigureTestServices(services =>
        {
            ReplaceGitProvider(services, provider);
            RemoveBackgroundServiceByName(services, "RepositoryCloneService");
            RemoveBackgroundServiceByName(services, "PullRequestSyncService");
        });
    }

    private static void ReplaceGitProvider(IServiceCollection services, IGitProvider provider)
    {
        services.RemoveAll<IGitProvider>();
        services.RemoveAll<IGitProviderRegistry>();
        services.AddSingleton(provider);
        services.AddSingleton<IGitProviderRegistry>(new StubGitProviderRegistry(provider));
    }

    private static void RemoveBackgroundServiceByName(IServiceCollection services, string typeName)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType?.Name == typeName)
            {
                services.RemoveAt(i);
            }
        }
    }
}

internal sealed class StubGitProviderRegistry : IGitProviderRegistry
{
    private readonly IGitProvider _provider;

    public StubGitProviderRegistry(IGitProvider provider)
    {
        _provider = provider;
        AllProviders = [provider];
    }

    public IGitProvider? GetByName(string providerName) =>
        providerName == GitProviderNames.GitHub ? _provider : null;

    public IReadOnlyCollection<IGitProvider> AllProviders { get; }
}

/// <summary>
/// Default <see cref="IGitProvider"/> stub used by the API integration tests.
/// Reports authenticated by default and returns empty result sets so individual
/// tests can override only the calls they care about.
/// </summary>
internal static class TestGitProvider
{
    private static readonly string[] DefaultScopes = { "repo", "read:org" };
    private static readonly IReadOnlyList<GitRepositoryRef> EmptyRefs = Array.Empty<GitRepositoryRef>();

    public static IGitProvider Create()
    {
        var provider = Substitute.For<IGitProvider>();
        provider.ProviderName.Returns(GitProviderNames.GitHub);
        provider.GetAuthStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProviderAuthStatus(
                Provider: GitProviderNames.GitHub,
                IsAuthenticated: true,
                Account: "octocat",
                Host: "github.com")
            {
                Scopes = DefaultScopes,
            }));
        provider.SearchRepositoriesAsync(
                Arg.Any<RepositorySearchScope>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(EmptyRefs));
        provider.ListUserRepositoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(EmptyRefs));
        return provider;
    }

    public static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);
}
