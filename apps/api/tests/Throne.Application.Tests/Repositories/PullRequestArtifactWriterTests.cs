using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class PullRequestArtifactWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly BindingId BindingId = new("binding-1");

    [Fact(DisplayName = "IngestAsync берёт PR number из binding и пишет latest artifact")]
    public async Task Ingest_uses_binding_pull_request_number()
    {
        var fixture = new Fixture();
        var binding = Binding(prNumber: 42);
        var artifact = Artifact(prNumber: 42);
        fixture.Bindings.GetByIdAsync(BindingId, Arg.Any<CancellationToken>())
            .Returns(binding);
        fixture.Artifacts.UpsertAsync(
                BindingId, 42, "static_analysis", PullRequestArtifactRenderNames.Markdown,
                "# body", "Static analysis", PullRequestArtifactSourceNames.Static,
                Arg.Any<IReadOnlyList<string>>(), Now, Arg.Any<CancellationToken>())
            .Returns(new WritePullRequestArtifactOutcome(artifact, Created: true));

        var result = await fixture.Writer.IngestAsync(Command(), CancellationToken.None);

        result.Should().BeSameAs(artifact);
    }

    [Fact(DisplayName = "IngestAsync для отсутствующего binding даёт repository_binding.not_found")]
    public async Task Ingest_missing_binding()
    {
        var fixture = new Fixture();
        fixture.Bindings.GetByIdAsync(BindingId, Arg.Any<CancellationToken>())
            .Returns((IntentRepositoryBinding?)null);

        var act = () => fixture.Writer.IngestAsync(Command(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryBindingNotFound);
        await fixture.Artifacts.DidNotReceiveWithAnyArgs()
            .UpsertAsync(default, default, null!, null!, null!, null!, null!, null!, default, default);
    }

    [Fact(DisplayName = "IngestAsync для binding без PR даёт repository_binding.pull_request_not_attached")]
    public async Task Ingest_without_pull_request()
    {
        var fixture = new Fixture();
        fixture.Bindings.GetByIdAsync(BindingId, Arg.Any<CancellationToken>())
            .Returns(Binding(prNumber: null));

        var act = () => fixture.Writer.IngestAsync(Command(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryPullRequestNotAttached);
        await fixture.Artifacts.DidNotReceiveWithAnyArgs()
            .UpsertAsync(default, default, null!, null!, null!, null!, null!, null!, default, default);
    }

    private static WritePullRequestArtifactCommand Command() => new(
        BindingId,
        "static_analysis",
        PullRequestArtifactRenderNames.Markdown,
        "# body",
        "Static analysis",
        PullRequestArtifactSourceNames.Static,
        ["sha:abc"],
        Now);

    private static PullRequestArtifact Artifact(int prNumber) =>
        PullRequestArtifact.Create(
            PullRequestArtifactId.New(),
            BindingId,
            prNumber,
            "static_analysis",
            PullRequestArtifactRenderNames.Markdown,
            "# body",
            "Static analysis",
            PullRequestArtifactSourceNames.Static,
            ["sha:abc"],
            Now);

    private static IntentRepositoryBinding Binding(int? prNumber) =>
        IntentRepositoryBinding.Create(
            BindingId,
            new IntentId("intent-1"),
            new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
            "main",
            "/tmp/ws",
            prNumber,
            Now);

    private sealed class Fixture
    {
        public Fixture()
        {
            Artifacts = Substitute.For<IPullRequestArtifactRepository>();
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            Writer = new PullRequestArtifactWriter(Artifacts, Bindings, new PassthroughUnitOfWork());
        }

        public IPullRequestArtifactRepository Artifacts { get; }
        public IIntentRepositoryBindingRepository Bindings { get; }
        public PullRequestArtifactWriter Writer { get; }
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
            work(ct);
    }
}
