using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Throne.Application.Manifest;

namespace Throne.Infrastructure.Manifest;

public sealed class YamlFileUserPromptSeedProvider : IUserPromptSeedProvider
{
    public YamlFileUserPromptSeedProvider(IOptions<UserPromptSeedOptions> options, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(env);

        var configuredPath = options.Value.Path;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new SkillManifestException("Throne:UserPromptSeed:Path is empty.");
        }

        var resolved = ManifestFileResolver.ResolveExisting(configuredPath, env.ContentRootPath)
            ?? throw new SkillManifestException(
                $"User prompt seed not found. Tried '{configuredPath}' relative to content root '{env.ContentRootPath}'.");

        var yaml = File.ReadAllText(resolved);
        Current = UserPromptSeedParser.Parse(yaml);
    }

    public UserPromptSeed Current { get; }
}
