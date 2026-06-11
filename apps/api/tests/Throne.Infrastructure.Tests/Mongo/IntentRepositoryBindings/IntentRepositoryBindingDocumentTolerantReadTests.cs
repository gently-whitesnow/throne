using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;
using Throne.Infrastructure.Mongo.Repositories;

namespace Throne.Infrastructure.Tests.Mongo.IntentRepositoryBindings;

/// <summary>
/// Regression guard for the document-schema change that added <c>host</c> /
/// <c>project_id</c> (ADR-0032). Pre-existing GitHub bindings were persisted
/// without those fields and with no <c>provider</c>-aware host, so the typed
/// serializer must tolerate their absence and the mapper must backfill
/// <c>github.com</c> — verified here against a hand-built legacy BSON document,
/// not a freshly-written one.
/// </summary>
public class IntentRepositoryBindingDocumentTolerantReadTests
{
    private static BsonDocument LegacyGitHubBinding() => new()
    {
        ["_id"] = "binding-legacy",
        ["intent_id"] = "intent-1",
        ["provider"] = "github",
        // no "host", no "project_id" — fields did not exist when this was written
        ["owner"] = "octocat",
        ["repo"] = "hello-world",
        ["default_branch"] = "main",
        ["workspace_path"] = "/ws/octocat__hello-world",
        ["clone_status"] = "ready",
        ["created_at"] = DateTime.UtcNow,
        ["updated_at"] = DateTime.UtcNow,
        // an unknown field a future/older writer might have left behind
        ["legacy_only_field"] = "ignored",
    };

    [Fact(DisplayName = "Legacy-документ без host/project_id десериализуется (extra-поля игнорируются)")]
    public void Legacy_document_deserializes_with_null_host_and_project_id()
    {
        var doc = BsonSerializer.Deserialize<IntentRepositoryBindingDocument>(LegacyGitHubBinding());

        doc.Host.Should().BeNull();
        doc.ProjectId.Should().BeNull();
        doc.Provider.Should().Be("github");
        doc.Owner.Should().Be("octocat");
    }

    [Fact(DisplayName = "Legacy-документ без suppress_merge_auto_close → флаг читается как false (авто-close сохраняется)")]
    public void Legacy_document_defaults_suppress_merge_auto_close_to_false()
    {
        var doc = BsonSerializer.Deserialize<IntentRepositoryBindingDocument>(LegacyGitHubBinding());

        var binding = IntentRepositoryBindingDocumentMapper.ToDomain(doc);

        doc.SuppressMergeAutoClose.Should().BeFalse();
        binding.State.SuppressMergeAutoClose.Should().BeFalse();
    }

    [Fact(DisplayName = "Маппер бэкфилит host=github.com для legacy GitHub-документа")]
    public void Mapper_backfills_github_host_for_legacy_document()
    {
        var doc = BsonSerializer.Deserialize<IntentRepositoryBindingDocument>(LegacyGitHubBinding());

        var binding = IntentRepositoryBindingDocumentMapper.ToDomain(doc);

        binding.Coordinate.Provider.Should().Be(GitProviderNames.GitHub);
        binding.Coordinate.Host.Should().Be(GitProviderHostDefaults.GitHub);
        binding.Coordinate.ProjectId.Should().BeNull();
        binding.Coordinate.Owner.Should().Be("octocat");
        binding.Coordinate.Repo.Should().Be("hello-world");
    }
}
