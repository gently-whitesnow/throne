using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Throne.Application.Instructions.Manifest;

namespace Throne.Infrastructure.Manifest;

public sealed class YamlFileSkillManifestProvider : ISkillManifestProvider
{
    public YamlFileSkillManifestProvider(IOptions<SkillManifestOptions> options, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(env);

        var configuredPath = options.Value.Path;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new SkillManifestException("Throne:SkillManifest:Path is empty.");
        }

        var resolved = ResolveExisting(configuredPath, env.ContentRootPath)
            ?? throw new SkillManifestException(
                $"Skill manifest not found. Tried '{configuredPath}' relative to content root '{env.ContentRootPath}'.");

        var yaml = File.ReadAllText(resolved);
        Current = SkillManifestParser.Parse(yaml);
    }

    public SkillManifest Current { get; }

    private static string? ResolveExisting(string path, string contentRoot)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            return path;
        }

        var candidates = new[]
        {
            Path.Combine(contentRoot, path),
            Path.Combine(AppContext.BaseDirectory, path),
            FindWalkingUp(AppContext.BaseDirectory, path),
            FindWalkingUp(contentRoot, path),
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindWalkingUp(string startDir, string relative)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
