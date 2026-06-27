using FluentAssertions;
using YamlDotNet.RepresentationModel;

namespace Throne.Api.Tests.Contracts;

public class OpenWireKeyContractTests
{
    [Theory(DisplayName = "provider/vendor extension schemas are open strings, not enum")]
    [InlineData("repositories/openapi.yaml", "GitProvider")]
    [InlineData("tags/openapi.yaml", "TagDefaultGitProvider")]
    [InlineData("terminal/openapi.yaml", "TerminalAgentVendor")]
    [InlineData("settings/openapi.yaml", "TerminalAgentVendor")]
    [InlineData("task-trackers/openapi.yaml", "TaskTrackerProvider")]
    public void Extension_key_schema_is_plain_string(string contractPath, string schemaName)
    {
        var schema = Schema(contractPath, schemaName);

        Scalar(schema, "type").Should().Be("string");
        schema.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(k => k.Value)
            .Should().NotContain("enum");
    }

    [Fact(DisplayName = "git provider status is a provider-keyed catalog")]
    public void Git_provider_status_is_catalog_shape()
    {
        var schema = Schema("settings/openapi.yaml", "GitProvidersStatusDto");
        var properties = Map(schema, "properties");

        properties.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(k => k.Value)
            .Should().BeEquivalentTo("providers");
        Scalar(Map(properties, "providers"), "type").Should().Be("array");
    }

    private static YamlMappingNode Schema(string contractPath, string schemaName)
    {
        var path = RepoRoot()
            .Combine("specs")
            .Combine("contracts")
            .Combine(contractPath);
        using var reader = File.OpenText(path);
        var stream = new YamlStream();
        stream.Load(reader);

        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        return Map(Map(Map(root, "components"), "schemas"), schemaName);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var specs = Path.Combine(dir.FullName, "specs", "contracts");
            if (Directory.Exists(specs))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Cannot locate repo root with specs/contracts.");
    }

    private static YamlMappingNode Map(YamlMappingNode node, string key) =>
        (YamlMappingNode)node.Children[new YamlScalarNode(key)];

    private static string? Scalar(YamlMappingNode node, string key) =>
        ((YamlScalarNode)node.Children[new YamlScalarNode(key)]).Value;
}

internal static class ContractPathExtensions
{
    public static string Combine(this string left, string right) =>
        Path.Combine(left, right);
}
