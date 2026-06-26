namespace Throne.Application.Git;

/// <summary>
/// Желаемое состояние рабочего дерева сразу после клона (или поверх готового клона).
/// <see cref="PullRequestNumber"/> приоритетнее <see cref="Branch"/>: PR может жить в форке,
/// поэтому его переключают провайдерным CLI (`gh pr checkout` / `glab mr checkout`), а не
/// обычным git-checkout. <see cref="Branch"/> — override дефолтной ветки из биндинга; он
/// несёт плейсхолдер "main", когда оператор ничего не выбрал, поэтому применяется только
/// если ref реально есть на origin и отличается от ветки, на которую встал клон.
/// </summary>
public sealed record CloneCheckout(string? Branch, int? PullRequestNumber)
{
    public static readonly CloneCheckout None = new(null, null);
}
