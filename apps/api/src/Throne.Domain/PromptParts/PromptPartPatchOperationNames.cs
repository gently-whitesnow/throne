namespace Throne.Domain.PromptParts;

public static class PromptPartPatchOperationNames
{
    public const string ReplaceText = "replace_text";
    public const string Create = "create";
    public const string SetRoles = "set_roles";
    public const string Delete = "delete";

    public static readonly IReadOnlyList<string> All = [ReplaceText, Create, SetRoles, Delete];

    public static bool IsKnown(string operation) => All.Contains(operation, StringComparer.Ordinal);
}
