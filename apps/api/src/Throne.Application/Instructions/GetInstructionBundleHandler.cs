using Throne.Application.Instructions.Manifest;

namespace Throne.Application.Instructions;

public sealed class GetInstructionBundleHandler(
    ISkillManifestProvider manifestProvider,
    IntentStatusAutoTransition autoTransition,
    UserBundleEntries userEntries)
{
    public async Task<InstructionBundle> HandleAsync(GetInstructionBundleQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var manifest = manifestProvider.Current;
        var bundle = BundleResolver.ResolveOrThrow(manifest, query.Mode);

        var intentId = string.IsNullOrWhiteSpace(query.IntentId) ? null : query.IntentId;
        await autoTransition.RunAsync(intentId, query.Mode, ct);

        var (systemEntries, missing) = SystemBundleEntries.Build(bundle, manifest);
        var userBuilt = await userEntries.BuildAsync(bundle, ct);

        var ordered = systemEntries.Concat(userBuilt).ToArray();
        return new InstructionBundle(query.Mode, intentId, ordered, missing);
    }

    public static string SyntheticSystemInstructionId(string kind) => $"system:{kind}";
}
