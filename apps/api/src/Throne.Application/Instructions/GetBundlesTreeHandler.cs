using Throne.Application.Auth;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

public sealed class GetBundlesTreeHandler(
    ISkillManifestProvider manifestProvider,
    IInstructionRepository repository,
    ICurrentUserAccessor currentUser)
{
    public async Task<BundlesTree> HandleAsync(GetBundlesTreeQuery _, CancellationToken ct)
    {
        var manifest = manifestProvider.Current;
        var userByKind = await UserInstructionLoader.LoadAsync(manifest, repository, currentUser.UserId, ct);

        var bundles = manifest.Bundles
            .Select(bundle => new BundleNode(
                bundle.Mode,
                bundle.Includes.Select(inc => BundleEntryBuilder.Build(inc, manifest, userByKind)).ToList()))
            .ToList();

        return new BundlesTree(bundles);
    }
}

internal static class UserInstructionLoader
{
    public static async Task<IReadOnlyDictionary<string, Instruction>> LoadAsync(
        SkillManifest manifest,
        IInstructionRepository repository,
        string userId,
        CancellationToken ct)
    {
        var allUserKinds = manifest.Bundles
            .SelectMany(b => b.Includes)
            .Where(i => string.Equals(i.Scope, InstructionScopeNames.User, StringComparison.Ordinal))
            .Select(i => i.Kind)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (allUserKinds.Length == 0)
        {
            return new Dictionary<string, Instruction>(StringComparer.Ordinal);
        }

        var userInstructions = await repository.GetUserInstructionsByKindsAsync(userId, allUserKinds, ct);

        return userInstructions
            .GroupBy(i => i.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.CreatedAt).First(), StringComparer.Ordinal);
    }
}
