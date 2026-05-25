using System.Text.Json;
using FluentAssertions;
using Throne.Api.Realtime;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Tests.Realtime;

/// <summary>
/// Smoke-проверки для T-12: mapper должен укладывать 4 события
/// (<c>intent.repository_bound</c>, <c>intent.repository_unbound</c>,
/// <c>intent.repository_clone_progress</c>, <c>intent.pr_comment_added</c>)
/// в форму, ожидаемую <c>specs/contracts/realtime/events.yaml</c>. JSON-сериализация
/// идёт теми же опциями, что использует <see cref="RealtimeController"/>,
/// чтобы snake_case-ключи провалидировались на wire-уровне.
/// </summary>
public class IntentRepositoryRealtimeMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact(DisplayName = "intent.repository_bound: payload — полный RepositoryBindingDto")]
    public void Maps_repository_bound_to_full_binding_dto()
    {
        var binding = MakePending();

        var envelope = IntentRepositoryRealtimeMapper.TryMap(new IntentRepositoryBound(binding));

        envelope.Should().NotBeNull();
        envelope!.Name.Should().Be(RealtimeEventNames.IntentRepositoryBound);

        using var json = SerializePayload(envelope.Payload);
        var root = json.RootElement;
        root.GetProperty("id").GetString().Should().Be(binding.Id.Value);
        root.GetProperty("intent_id").GetString().Should().Be(binding.IntentId.Value);
        root.GetProperty("owner").GetString().Should().Be("anthropics");
        root.GetProperty("repo").GetString().Should().Be("throne");
        root.GetProperty("clone_status").GetString().Should().Be("pending");
        root.GetProperty("default_branch").GetString().Should().Be("main");
        root.GetProperty("workspace_path").GetString().Should().Be(binding.WorkspacePath);
    }

    [Fact(DisplayName = "intent.repository_unbound: payload — {intent_id, binding_id}")]
    public void Maps_repository_unbound_to_minimal_payload()
    {
        var binding = MakePending();

        var envelope = IntentRepositoryRealtimeMapper.TryMap(new IntentRepositoryUnbound(binding));

        envelope.Should().NotBeNull();
        envelope!.Name.Should().Be(RealtimeEventNames.IntentRepositoryUnbound);

        using var json = SerializePayload(envelope.Payload);
        var root = json.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("intent_id", "binding_id");
        root.GetProperty("intent_id").GetString().Should().Be(binding.IntentId.Value);
        root.GetProperty("binding_id").GetString().Should().Be(binding.Id.Value);
    }

    [Fact(DisplayName = "intent.repository_clone_progress(ready): error отсутствует в JSON")]
    public void Maps_clone_progress_ready_without_error_key()
    {
        var binding = MakePending();
        binding.MarkCloning(Now.AddSeconds(1));
        binding.MarkReady(Now.AddSeconds(2));

        var envelope = IntentRepositoryRealtimeMapper.TryMap(new IntentRepositoryCloneProgress(binding));

        envelope.Should().NotBeNull();
        envelope!.Name.Should().Be(RealtimeEventNames.IntentRepositoryCloneProgress);

        using var json = SerializePayload(envelope.Payload);
        var root = json.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("intent_id", "binding_id", "status");
        root.GetProperty("status").GetString().Should().Be("ready");
    }

    [Fact(DisplayName = "intent.repository_clone_progress(failed): payload включает error")]
    public void Maps_clone_progress_failed_includes_error()
    {
        var binding = MakePending();
        binding.MarkCloning(Now.AddSeconds(1));
        binding.MarkFailed("auth denied", Now.AddSeconds(2));

        var envelope = IntentRepositoryRealtimeMapper.TryMap(new IntentRepositoryCloneProgress(binding));

        envelope.Should().NotBeNull();
        using var json = SerializePayload(envelope!.Payload);
        var root = json.RootElement;
        root.GetProperty("status").GetString().Should().Be("failed");
        root.GetProperty("error").GetString().Should().Be("auth denied");
    }

    [Fact(DisplayName = "intent.pr_comment_added: payload — {intent_id, binding_id, comment{...}}")]
    public void Maps_pr_comment_added_with_nested_comment_dto()
    {
        var binding = MakePending(prNumber: 42);
        var comment = new PullRequestComment(
            Id: "rc-1",
            AuthorLogin: "octocat",
            Body: "lgtm",
            CreatedAt: Now,
            AuthorAvatarUrl: "https://example.test/a.png",
            HtmlUrl: "https://example.test/pr/42#rc-1",
            Path: "src/x.cs",
            UpdatedAt: Now.AddSeconds(10));

        var envelope = IntentRepositoryRealtimeMapper.TryMap(new IntentPrCommentAdded(binding, comment));

        envelope.Should().NotBeNull();
        envelope!.Name.Should().Be(RealtimeEventNames.IntentPrCommentAdded);

        using var json = SerializePayload(envelope.Payload);
        var root = json.RootElement;
        root.GetProperty("intent_id").GetString().Should().Be(binding.IntentId.Value);
        root.GetProperty("binding_id").GetString().Should().Be(binding.Id.Value);
        var c = root.GetProperty("comment");
        c.GetProperty("id").GetString().Should().Be("rc-1");
        c.GetProperty("binding_id").GetString().Should().Be(binding.Id.Value);
        c.GetProperty("author_login").GetString().Should().Be("octocat");
        c.GetProperty("body").GetString().Should().Be("lgtm");
        c.GetProperty("path").GetString().Should().Be("src/x.cs");
    }

    [Fact(DisplayName = "Чужие доменные события возвращают null")]
    public void Returns_null_for_unrelated_event()
    {
        IntentRepositoryRealtimeMapper.TryMap(new IntentDeleted("x")).Should().BeNull();
    }

    private static IntentRepositoryBinding MakePending(int? prNumber = null) =>
        IntentRepositoryBindingFactory.Create(
            id: BindingId.New(),
            intentId: new IntentId("intent-abc"),
            coordinate: new RepoCoordinate(GitProviderNames.GitHub, "anthropics", "throne"),
            defaultBranch: "main",
            workspacePath: "/tmp/throne/workspace/intent-abc/anthropics__throne",
            pullRequestNumber: prNumber,
            now: Now);

    private static JsonDocument SerializePayload(object payload)
    {
        var json = JsonSerializer.Serialize(payload, PayloadOptions);
        return JsonDocument.Parse(json);
    }
}
