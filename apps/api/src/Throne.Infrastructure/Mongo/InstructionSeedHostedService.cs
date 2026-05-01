using Microsoft.Extensions.Hosting;
using Throne.Application.Instructions;

namespace Throne.Infrastructure.Mongo;

internal sealed class InstructionSeedHostedService(EnsureSeedInstructionsHandler handler) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        handler.HandleAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
