namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// Сценарные ожидания и освобождение клонов поверх <see cref="CloneInFlightTracker"/>.
/// Вынесены отдельно, чтобы предикаты/сообщения не раздували сложность трекера.
/// </summary>
internal static class CloneInFlightWaits
{
    public static Task WaitForInFlightAsync(this CloneInFlightTracker tracker, int expected, TimeSpan budget) =>
        tracker.WaitForAsync(
            s => s.InFlight >= expected,
            budget,
            () => $"runner должен был поднять минимум {expected} клонов в параллель (in-flight={tracker.InFlight}).");

    public static Task WaitForCompletedAsync(this CloneInFlightTracker tracker, int expected, TimeSpan budget) =>
        tracker.WaitForAsync(
            s => s.Completed >= expected,
            budget,
            () => $"runner должен был завершить минимум {expected} клонов (completed={tracker.Completed}).");

    public static Task WaitForAllCompletedAsync(this CloneInFlightTracker tracker, TimeSpan budget) =>
        tracker.WaitForAsync(
            s => s.InFlight == 0,
            budget,
            () => $"runner должен был завершить все клоны (in-flight={tracker.InFlight}).");

    public static async Task ReleaseOneAsync(this CloneInFlightTracker tracker, TimeSpan budget)
    {
        await tracker.WaitForAsync(s => s.HasGate, budget, () => "Нет ожидающих клонов для release (timeout).");
        tracker.TryRelease();
    }

    public static void ReleaseRemaining(this CloneInFlightTracker tracker)
    {
        while (tracker.TryRelease())
        {
        }
    }
}
