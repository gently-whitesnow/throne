namespace Throne.Domain.Repositories;

/// <summary>
/// Wire-format constants for <see cref="IntentRepositoryBinding.CloneStatus"/>.
/// Transitions (see ADR-0024 §5):
/// <code>
/// pending  → cloning  → ready
///                   ↘ failed
/// ready    → broken                  (404 upstream при polling)
/// </code>
/// </summary>
public static class CloneStatusNames
{
    public const string Pending = "pending";
    public const string Cloning = "cloning";
    public const string Ready = "ready";
    public const string Failed = "failed";
    public const string Broken = "broken";

    public static bool IsKnown(string value) =>
        value is Pending or Cloning or Ready or Failed or Broken;
}
