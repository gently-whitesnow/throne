using FluentAssertions;
using NSubstitute;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;

namespace Throne.Application.Tests.Intents;

public class CreateIntentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateIntent вставляет Intent и snapshot v1 через repository")]
    public async Task CreateIntent_persists_intent_and_v1_snapshot()
    {
        var repo = Substitute.For<IIntentRepository>();
        var tagRepo = Substitute.For<ITagRepository>();
        var existing = Tag.Create(TagId.New(), "throne", Now);
        tagRepo.EnsureByNameAsync("throne", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<EnsureTagOutcome>(new EnsureTagOutcome.Existed(existing)));

        repo.CreateAsync(
                Arg.Any<Intent>(),
                Arg.Any<TextVersion>(),
                Arg.Any<IntentStatusChange>(),
                Arg.Any<IReadOnlyList<Tag>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(
                new CreateIntentOutcome(call.Arg<Intent>(), call.Arg<IReadOnlyList<Tag>>())));

        var uow = new PassthroughUnitOfWork();
        var clock = new FakeTimeProvider(Now);
        var handler = new CreateIntentHandler(repo, tagRepo, uow, new TestCurrentUserAccessor(), clock);

        var intent = await handler.HandleAsync(new CreateIntentCommand("hello world", ["throne"], TextVersionAuthor.Agent), CancellationToken.None);

        intent.Text.Should().Be("hello world");
        intent.Status.Should().Be(IntentStatusNames.Draft);
        intent.CurrentVersion.Should().Be(1);
        intent.TagIds.Should().Equal(existing.Id);
        intent.CreatedAt.Should().Be(Now);
        intent.UpdatedAt.Should().Be(Now);

        await repo.Received(1).CreateAsync(
            Arg.Is<Intent>(i =>
                i.Text == "hello world" &&
                i.Status == IntentStatusNames.Draft &&
                i.CurrentVersion == 1),
            Arg.Is<TextVersion>(v =>
                v.Version == 1 &&
                v.Kind == TextVersionKind.Create &&
                v.OwnerKind == TextVersionOwnerKind.Intent &&
                v.Snapshot == "hello world"),
            Arg.Is<IntentStatusChange>(c =>
                c.FromStatus == IntentStatusNames.Draft &&
                c.ToStatus == IntentStatusNames.Draft &&
                c.CreatedBy == IntentTrainingAuthor.Agent),
            Arg.Any<IReadOnlyList<Tag>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateIntent поднимает TagCreated, когда тег ранее не существовал")]
    public async Task CreateIntent_emits_tag_created_for_new_tags()
    {
        var repo = Substitute.For<IIntentRepository>();
        var tagRepo = Substitute.For<ITagRepository>();
        var newTag = Tag.Create(TagId.New(), "throne", Now);
        tagRepo.EnsureByNameAsync("throne", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<EnsureTagOutcome>(new EnsureTagOutcome.Created(newTag)));

        repo.CreateAsync(
                Arg.Any<Intent>(),
                Arg.Any<TextVersion>(),
                Arg.Any<IntentStatusChange>(),
                Arg.Any<IReadOnlyList<Tag>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(
                new CreateIntentOutcome(call.Arg<Intent>(), call.Arg<IReadOnlyList<Tag>>())));

        var uow = new PassthroughUnitOfWork();
        var clock = new FakeTimeProvider(Now);
        var handler = new CreateIntentHandler(repo, tagRepo, uow, new TestCurrentUserAccessor(), clock);

        var intent = await handler.HandleAsync(
            new CreateIntentCommand("hello world", ["throne"], TextVersionAuthor.Agent),
            CancellationToken.None);

        intent.TagIds.Should().Equal(newTag.Id);
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
