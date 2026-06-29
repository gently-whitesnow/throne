using FluentAssertions;

namespace Throne.Api.Tests.Manifest;

public class SkillManifestPublishTests
{
    [Fact(DisplayName = "Манифест skill-ов копируется рядом с Throne.Api.dll (Content в csproj)")]
    public void Manifest_is_copied_next_to_binary()
    {
        // We assert against the API host's own bin output, not the test bin,
        // because the Content item lives on Throne.Api.csproj specifically.
        var apiBinDir = ResolveApiBinDirectory();
        var manifestPath = Path.Combine(apiBinDir, "specs", "manifest", "throne-system-prompt-parts.yaml");

        File.Exists(manifestPath).Should().BeTrue(
            $"Throne.Api.csproj must include the skill manifest as Content with " +
            $"CopyToOutputDirectory/CopyToPublishDirectory so Docker/CI publish " +
            $"layouts can find it. Expected at: {manifestPath}");
    }

    [Fact(DisplayName = "User-prompt seed-манифест копируется рядом с Throne.Api.dll (Content в csproj)")]
    public void User_prompt_seed_is_copied_next_to_binary()
    {
        var apiBinDir = ResolveApiBinDirectory();
        var seedPath = Path.Combine(apiBinDir, "specs", "manifest", "throne-user-prompt-seed-parts.yaml");

        File.Exists(seedPath).Should().BeTrue(
            $"Throne.Api.csproj must include the user-prompt seed manifest as Content with " +
            $"CopyToOutputDirectory/CopyToPublishDirectory so the boot seeder finds it in " +
            $"Docker/CI publish layouts. Expected at: {seedPath}");
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
