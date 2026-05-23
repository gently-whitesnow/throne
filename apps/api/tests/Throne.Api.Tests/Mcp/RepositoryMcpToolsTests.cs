using FluentAssertions;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Throne.Api.Mcp.Tools;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Api.Tests.Mcp;

/// <summary>
/// MCP-уровневые тесты T-13: проекция <c>get_intent.repositories</c> и фильтр
/// <c>since</c> у <c>list_intent_pr_comments</c>. Контракты тестов:
/// <list type="bullet">
///   <item><c>get_intent</c> отдаёт текст в Content и компактные refs (включая
///         repositories[]) в audit-канале (ADR-0003 §8.1).</item>
///   <item><c>list_intent_pr_comments</c> мерджит комменты по всем bindings,
///         сортирует по created_at ASC, фильтрует по since включительно.</item>
///   <item>Bindings без attached PR не дают комментов, даже если в store есть
///         записи на binding_id (защита от косвенной утечки).</item>
/// </list>
/// </summary>
public class RepositoryMcpToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "get_intent.repositories — пустой массив, когда у интента нет bindings")]
    public async Task GetIntent_returns_empty_repositories_when_none_bound()
    {
        var intentId = IntentId.New();
        var fixture = new GetIntentFixture(intentId);

        var payload = await fixture.Tools.GetIntent(intentId.Value, CancellationToken.None);

        payload.Wire.IsError.Should().BeFalse();
        var text = ReadText(payload.Wire);
        text.Should().NotContain("===== repositories");
        payload.AuditSummary.Should().NotBeNull();
        payload.AuditSummary!.Should().ContainKey("repositories");
    }

    [Fact(DisplayName = "get_intent.repositories содержит каждый binding с workspace_path и pull_request_number")]
    public async Task GetIntent_returns_repositories_when_bindings_exist()
    {
        var intentId = IntentId.New();
        var fixture = new GetIntentFixture(intentId);
        var binding = NewBinding(intentId, owner: "octo", repo: "hello", cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 42);
        fixture.SeedBindings([binding]);

        var payload = await fixture.Tools.GetIntent(intentId.Value, CancellationToken.None);

        var text = ReadText(payload.Wire);
        text.Should().Contain("===== repositories (1) =====");
        text.Should().Contain($"binding_id={binding.Id.Value}");
        text.Should().Contain("octo/hello");
        text.Should().Contain("clone_status=ready");
        text.Should().Contain("pull_request_number=42");
        text.Should().Contain($"workspace_path={binding.WorkspacePath}");
    }

    [Fact(DisplayName = "list_intent_pr_comments возвращает пустой список, когда у интента нет bindings")]
    public async Task ListPrComments_returns_empty_when_no_bindings()
    {
        var fixture = new PrCommentsFixture(intentBindings: []);

        var result = await fixture.Tools.ListIntentPrComments("intent-x", since: null, CancellationToken.None);

        result.Items.Should().BeEmpty();
        await fixture.Comments.DidNotReceiveWithAnyArgs()
            .ListByBindingAsync(default!, default);
    }

    [Fact(DisplayName = "list_intent_pr_comments игнорирует binding без attached PR (защита от косвенной утечки)")]
    public async Task ListPrComments_skips_bindings_without_pr()
    {
        var intentId = IntentId.New();
        var bindingWithoutPr = NewBinding(intentId, pullRequestNumber: null, cloneStatus: CloneStatusNames.Ready);
        var fixture = new PrCommentsFixture(intentBindings: [bindingWithoutPr]);

        var result = await fixture.Tools.ListIntentPrComments(intentId.Value, since: null, CancellationToken.None);

        result.Items.Should().BeEmpty();
        await fixture.Comments.DidNotReceive()
            .ListByBindingAsync(bindingWithoutPr.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "list_intent_pr_comments мерджит и сортирует комменты со всех bindings по created_at ASC")]
    public async Task ListPrComments_merges_across_bindings_sorted_by_created_at()
    {
        var intentId = IntentId.New();
        var bindingA = NewBinding(intentId, owner: "octo", repo: "alpha", cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 1);
        var bindingB = NewBinding(intentId, owner: "octo", repo: "beta", cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 2);
        var fixture = new PrCommentsFixture(intentBindings: [bindingA, bindingB]);

        // Out-of-order across bindings — merge should put them by created_at ascending.
        fixture.SeedComments(bindingA, [
            NewComment(bindingA, intentId, upstreamId: "a2", createdAt: Now.AddMinutes(20)),
            NewComment(bindingA, intentId, upstreamId: "a1", createdAt: Now),
        ]);
        fixture.SeedComments(bindingB, [
            NewComment(bindingB, intentId, upstreamId: "b1", createdAt: Now.AddMinutes(10)),
            NewComment(bindingB, intentId, upstreamId: "b2", createdAt: Now.AddMinutes(30)),
        ]);

        var result = await fixture.Tools.ListIntentPrComments(intentId.Value, since: null, CancellationToken.None);

        result.Items.Select(c => c.Id).Should().Equal("a1", "b1", "a2", "b2");
    }

    [Fact(DisplayName = "list_intent_pr_comments фильтрует по since включительно (created_at >= since)")]
    public async Task ListPrComments_since_filter_is_inclusive()
    {
        var intentId = IntentId.New();
        var binding = NewBinding(intentId, cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 7);
        var fixture = new PrCommentsFixture(intentBindings: [binding]);
        var pivot = Now.AddMinutes(15);
        fixture.SeedComments(binding, [
            NewComment(binding, intentId, upstreamId: "before", createdAt: Now),
            NewComment(binding, intentId, upstreamId: "boundary", createdAt: pivot),
            NewComment(binding, intentId, upstreamId: "after", createdAt: Now.AddMinutes(30)),
        ]);

        var result = await fixture.Tools.ListIntentPrComments(intentId.Value, since: pivot, CancellationToken.None);

        result.Items.Select(c => c.Id).Should().Equal("boundary", "after");
    }

    [Fact(DisplayName = "list_intent_pr_comments проецирует binding_id, body и optional поля (html_url/path/updated_at)")]
    public async Task ListPrComments_projects_optional_fields()
    {
        var intentId = IntentId.New();
        var binding = NewBinding(intentId, cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 7);
        var fixture = new PrCommentsFixture(intentBindings: [binding]);
        fixture.SeedComments(binding, [
            new PullRequestCommentRecord(
                BindingId: binding.Id,
                IntentId: intentId,
                UpstreamId: "u1",
                AuthorLogin: "alice",
                Body: "lgtm",
                CreatedAt: Now,
                ObservedAt: Now,
                AuthorAvatarUrl: "https://example.test/a.png",
                HtmlUrl: "https://example.test/pr/1#c1",
                Path: "src/foo.cs",
                UpdatedAt: Now.AddMinutes(1)),
        ]);

        var result = await fixture.Tools.ListIntentPrComments(intentId.Value, since: null, CancellationToken.None);

        var only = result.Items.Should().ContainSingle().Subject;
        only.Id.Should().Be("u1");
        only.BindingId.Should().Be(binding.Id.Value);
        only.AuthorLogin.Should().Be("alice");
        only.Body.Should().Be("lgtm");
        only.HtmlUrl.Should().Be("https://example.test/pr/1#c1");
        only.Path.Should().Be("src/foo.cs");
        only.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    private static PullRequestCommentRecord NewComment(
        IntentRepositoryBinding binding,
        IntentId intentId,
        string upstreamId,
        DateTimeOffset createdAt) =>
        new(
            BindingId: binding.Id,
            IntentId: intentId,
            UpstreamId: upstreamId,
            AuthorLogin: "alice",
            Body: $"comment {upstreamId}",
            CreatedAt: createdAt,
            ObservedAt: createdAt);

    private static IntentRepositoryBinding NewBinding(
        IntentId intentId,
        string owner = "octo",
        string repo = "hello",
        string cloneStatus = CloneStatusNames.Pending,
        int? pullRequestNumber = null)
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: intentId,
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, owner, repo),
            WorkspacePath: $"/tmp/throne-workspaces/intents/{intentId.Value}/{owner}__{repo}",
            DefaultBranch: "main",
            CloneStatus: cloneStatus,
            CloneError: null,
            PullRequestNumber: pullRequestNumber,
            PullRequestState: pullRequestNumber is null ? null : PullRequestStateNames.Open,
            ReviewCommentsEtag: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now);
        return IntentRepositoryBindingFactory.Restore(snapshot);
    }

    private static string ReadText(CallToolResult result) =>
        result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;

    private sealed class GetIntentFixture
    {
        private readonly IIntentRepositoryBindingReader _bindings = Substitute.For<IIntentRepositoryBindingReader>();

        public GetIntentFixture(IntentId intentId)
        {
            var intentRepo = Substitute.For<IIntentRepository>();
            intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
                .Returns(IntentFactory.Restore(intentId, "user-1", "body", IntentStatusNames.Work, 1, [], Now, Now));

            var attachments = Substitute.For<IIntentAttachmentRepository>();
            attachments.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
                .Returns(new List<Throne.Application.Intents.IntentAttachment>());

            var linkRepo = Substitute.For<IIntentLinkRepository>();
            linkRepo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
                .Returns([]);

            var tagRepo = Substitute.For<ITagRepository>();
            tagRepo.ListAllAsync(Arg.Any<CancellationToken>()).Returns([]);

            _bindings.ListByIntentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([]));

            Tools = new IntentTools(
                create: null!,
                get: new GetIntentHandler(intentRepo),
                getInstructionBundle: null!,
                listIntents: null!,
                moveIntentHandler: null!,
                linkRepository: linkRepo,
                attachments: attachments,
                tagRefs: new IntentToolTagRefs(tagRepo),
                repositoryBindings: _bindings);
        }

        public IntentTools Tools { get; }

        public void SeedBindings(IReadOnlyList<IntentRepositoryBinding> bindings) =>
            _bindings.ListByIntentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(bindings));
    }

    private sealed class PrCommentsFixture
    {
        private readonly Dictionary<string, IReadOnlyList<PullRequestCommentRecord>> _seeded = new(StringComparer.Ordinal);

        public PrCommentsFixture(IReadOnlyList<IntentRepositoryBinding> intentBindings)
        {
            var reader = Substitute.For<IIntentRepositoryBindingReader>();
            reader.ListByIntentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(intentBindings));
            Comments = Substitute.For<IPullRequestCommentRepository>();
            Comments.ListByBindingAsync(Arg.Any<BindingId>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var id = ci.Arg<BindingId>().Value;
                    return Task.FromResult(_seeded.TryGetValue(id, out var v)
                        ? v
                        : (IReadOnlyList<PullRequestCommentRecord>)[]);
                });
            Tools = new RepositoryMcpTools(reader, Comments);
        }

        public IPullRequestCommentRepository Comments { get; }
        public RepositoryMcpTools Tools { get; }

        public void SeedComments(IntentRepositoryBinding binding, IReadOnlyList<PullRequestCommentRecord> records) =>
            _seeded[binding.Id.Value] = records;
    }
}
