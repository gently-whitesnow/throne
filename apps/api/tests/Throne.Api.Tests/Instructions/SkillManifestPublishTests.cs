using FluentAssertions;

namespace Throne.Api.Tests.Instructions;

public class SkillManifestPublishTests
{
    [Fact(DisplayName = "Манифест skill-ов копируется рядом с Throne.Api.dll (Content в csproj)")]
    public void Manifest_is_copied_next_to_binary()
    {
        // We assert against the API host's own bin output, not the test bin,
        // because the Content item lives on Throne.Api.csproj specifically.
        var apiBinDir = ResolveApiBinDirectory();
        var manifestPath = Path.Combine(apiBinDir, "specs", "manifest", "throne-skills.yaml");

        File.Exists(manifestPath).Should().BeTrue(
            $"Throne.Api.csproj must include the skill manifest as Content with " +
            $"CopyToOutputDirectory/CopyToPublishDirectory so Docker/CI publish " +
            $"layouts can find it. Expected at: {manifestPath}");
    }

    private static string ResolveApiBinDirectory()
    {
        // Test bin is .../apps/api/tests/Throne.Api.Tests/bin/<Config>/net10.0.
        // We need        .../apps/api/src/Throne.Api/bin/<Config>/net10.0.
        var testBin = AppContext.BaseDirectory;
        var apiBin = testBin
            .Replace(
                Path.Combine("tests", "Throne.Api.Tests", "bin"),
                Path.Combine("src", "Throne.Api", "bin"),
                StringComparison.Ordinal);

        Directory.Exists(apiBin).Should().BeTrue(
            $"Could not derive Throne.Api bin path from test bin '{testBin}'.");
        return apiBin;
    }
}
