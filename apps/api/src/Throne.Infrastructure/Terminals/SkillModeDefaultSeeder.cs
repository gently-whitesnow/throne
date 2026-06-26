using Microsoft.Extensions.Hosting;
using Throne.Application.Ports;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

internal sealed class SkillModeDefaultSeeder(
    ISessionSkillCatalog catalog,
    ISkillModeDefaultStore store) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal Task RunAsync(CancellationToken ct) =>
        store.UpsertMissingAsync(SkillModeDefaultSeeds.Build(catalog), ct);
}
