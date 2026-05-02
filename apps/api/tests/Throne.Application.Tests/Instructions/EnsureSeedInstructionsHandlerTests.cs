using FluentAssertions;
using NSubstitute;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Tests.Instructions;

public class EnsureSeedInstructionsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "EnsureSeedInstructions создаёт только отсутствующие seed-инструкции с snapshot v1")]
    public async Task Ensure_creates_missing_seed_instructions()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var existing = Instruction.Create(InstructionId.New(), InstructionKindNames.Common, "custom", Now);
        repo.GetByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([existing]);

        var handler = new EnsureSeedInstructionsHandler(repo, new PassthroughUnitOfWork(), new FakeTimeProvider(Now));

        await handler.HandleAsync(CancellationToken.None);

        await repo.Received(4).CreateAsync(
            Arg.Is<Instruction>(i => i.Kind != InstructionKindNames.Common && i.CurrentVersion == 1),
            Arg.Is<TextVersion>(v =>
                v.OwnerKind == TextVersionOwnerKind.Instruction &&
                v.Kind == TextVersionKind.Create &&
                v.Version == 1 &&
                v.ChangedBy == TextVersionAuthor.System),
            Arg.Any<CancellationToken>());
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
